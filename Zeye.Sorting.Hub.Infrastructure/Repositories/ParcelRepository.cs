using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

/// <summary>
/// Parcel 仓储第一阶段实现。
/// </summary>
public sealed class ParcelRepository : RepositoryBase<Parcel, SortingHubDbContext>, IParcelRepository {
    /// <summary>实际分表路由，独立单元测试可省略以使用基础表模型。</summary>
    private readonly ParcelPartitionStore? _partitions;
    /// <summary>是否对长窗口启用有界分表并行读取。</summary>
    private readonly bool _readFanoutEnabled;
    /// <summary>分表并行读取的连接数上限。</summary>
    private readonly int _readFanoutConcurrency;
    /// <summary>超过该分表数时改用单条数据库合并查询。</summary>
    private readonly int _readFanoutMaxPartitions;
    /// <summary>
    /// NLog 日志器（静态，无需 DI 注入；日志来源类名为 ParcelRepository）。
    /// </summary>
    private static readonly ILogger NLogLogger = LogManager.GetCurrentClassLogger();
    /// <summary>
    /// 空配置（用于保持默认值读取语义）。
    /// </summary>
    private static readonly IConfiguration EmptyConfiguration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>())
        .Build();

    /// <summary>
    /// 过期清理动作名（用于结构化审计）。
    /// </summary>
    private const string RemoveExpiredActionName = "ParcelRepository.RemoveExpired";

    /// <summary>
    /// 物理删除补偿边界说明。
    /// </summary>
    private const string RemoveExpiredCompensationBoundary = ParcelCleanupAudit.SummaryCompensationBoundary;
    /// <summary>不包含凭据的永久治理记录序列化选项。</summary>
    private static readonly JsonSerializerOptions CleanupJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 过期清理隔离器开关配置键。
    /// </summary>
    internal const string RemoveExpiredEnableGuardConfigKey = ParcelCleanupIsolationPolicy.EnableGuardConfigKey;

    /// <summary>
    /// 过期清理允许执行危险动作配置键。
    /// </summary>
    internal const string RemoveExpiredAllowExecutionConfigKey = ParcelCleanupIsolationPolicy.AllowExecutionConfigKey;

    /// <summary>
    /// 过期清理 dry-run 配置键。
    /// </summary>
    internal const string RemoveExpiredDryRunConfigKey = ParcelCleanupIsolationPolicy.DryRunConfigKey;

    /// <summary>
    /// 过期数据分批删除批次大小。
    /// </summary>
    private const int ExpiredDeleteBatchSize = 1000;

    /// <summary>
    /// 单次调用过期删除最大条数保护阈值。
    /// </summary>
    private const int MaxExpiredDeleteCountPerCall = 10000;
    /// <summary>
    /// 包裹主键冲突错误消息。
    /// </summary>
    internal const string DuplicateParcelIdErrorMessage = "包裹 Id 已存在。";

    /// <summary>
    /// 由配置计算的清理隔离决策。
    /// </summary>
    private readonly ActionIsolationDecision _removeExpiredDecision;

    /// <summary>
    /// 创建 ParcelRepository。
    /// </summary>
    public ParcelRepository(
        IDbContextFactory<SortingHubDbContext> contextFactory)
        : this(contextFactory, EmptyConfiguration) {
    }

    /// <summary>
    /// 创建 ParcelRepository（带配置的危险动作隔离能力）。
    /// </summary>
    public ParcelRepository(
        IDbContextFactory<SortingHubDbContext> contextFactory,
        IConfiguration? configuration,
        ParcelPartitionStore? partitions = null)
        : base(contextFactory, NLogLogger) {
        _partitions = partitions;
        var effectiveConfiguration = configuration ?? EmptyConfiguration;
        _readFanoutEnabled = AutoTuningConfigurationReader.GetBoolOrDefault(effectiveConfiguration,
            "Persistence:Sharding:ReadFanout:Enabled", true);
        _readFanoutConcurrency = Math.Clamp(AutoTuningConfigurationReader.GetPositiveIntOrDefault(effectiveConfiguration,
            "Persistence:Sharding:ReadFanout:MaxConcurrency", 4), 1, 8);
        _readFanoutMaxPartitions = Math.Clamp(AutoTuningConfigurationReader.GetPositiveIntOrDefault(effectiveConfiguration,
            "Persistence:Sharding:ReadFanout:MaxPartitions", 12), 1, 32);
        // 页面清理默认执行；HTTP 入口必须再次验证当前用户的权限和密码。
        _removeExpiredDecision = ParcelCleanupIsolationPolicy.Evaluate(effectiveConfiguration);
    }

    /// <summary>
    /// 新增包裹聚合。
    /// </summary>
    public override async Task<RepositoryResult> AddAsync(Parcel parcel, CancellationToken cancellationToken) {
        if (parcel is null) {
            return RepositoryResult.Fail("实体不能为空");
        }

        try {
            var suffix = string.Empty;
            if (_partitions is not null) {
                await using var lookup = await ContextFactory.CreateDbContextAsync(cancellationToken);
                if (await lookup.Set<ParcelLocation>().AnyAsync(x => x.Id == parcel.Id, cancellationToken) || await lookup.Set<Parcel>().AnyAsync(x => x.Id == parcel.Id, cancellationToken))
                    return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
                var period = _partitions.Resolve(parcel.CreatedTime);
                await _partitions.EnsureCreatedAsync(period, cancellationToken);
                suffix = period.Suffix;
            }
            await using var db = _partitions is null ? await ContextFactory.CreateDbContextAsync(cancellationToken) : await _partitions.CreateContextAsync(suffix, cancellationToken);
            await db.Set<Parcel>().AddAsync(parcel, cancellationToken);
            if (_partitions is not null) db.Add(new ParcelLocation { Id = parcel.Id, Suffix = suffix, CreatedTime = parcel.CreatedTime });
            await db.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (OperationCanceledException ex) {
            Logger.Warn(ex, "新增包裹操作被取消，Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail("操作已取消");
        }
        catch (DbUpdateException ex) when (DuplicateKeyExceptionDetector.IsDuplicateKeyException(ex)) {
            Logger.Warn(ex, "新增包裹主键冲突，Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        catch (DbUpdateException ex) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.Message) || DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.InnerException?.Message)) {
            Logger.Warn(ex, "新增包裹主键冲突（DbUpdateException 回退分支），Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        // InMemory Provider(当前测试基线 .NET8 + EFCore.InMemory 9.x) 在主键冲突场景下通常抛出 InvalidOperationException，
        // 且无稳定错误码，仅有消息文本。该分支仅为测试基础设施兼容兜底；真实数据库优先走上方错误码分支。
        catch (InvalidOperationException ex) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.Message)) {
            Logger.Warn(ex, "新增包裹主键冲突（提供器回退分支），Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        catch (Exception ex) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.Message) || DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.InnerException?.Message)) {
            Logger.Warn(ex, "新增包裹主键冲突（通用回退分支），Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        catch (Exception ex) {
            Logger.Error(ex, "新增包裹失败，Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail("新增包裹失败");
        }
    }

    /// <summary>
    /// 根据主键获取包裹完整聚合详情（包含值对象与集合）。
    /// </summary>
    public async Task<Parcel?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        if (id <= 0) {
            return null;
        }

        try {
            await using var db = await CreateContextForIdAsync(id, cancellationToken);
            var parcel = await BuildDetailQuery(db)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (parcel?.SourceInstanceId is not null) parcel.LoadProcessingRecords(await db.Set<ParcelProcessingRecord>().AsNoTracking().Where(x => x.ParcelId == id).OrderBy(x => x.OccurredAt).ThenBy(x => x.AttemptNumber).ThenBy(x => x.RecordId).ToListAsync(cancellationToken));
            return parcel;
        }
        catch (Exception ex) {
            Logger.Error(ex, "根据 Id 查询包裹详情失败，Id={ParcelId}", id);
            throw;
        }
    }

    /// <summary>统一完整聚合查询，详情与更新复用同一关系覆盖清单。</summary>
    private static IQueryable<Parcel> BuildDetailQuery(SortingHubDbContext db) => db.Set<Parcel>().AsNoTracking()
                .Include(x => x.BagInfo)
                .Include(x => x.VolumeInfo)
                .Include(x => x.ChuteInfo)
                .Include(x => x.SorterCarrierInfo)
                .Include(x => x.DeviceInfo)
                .Include(x => x.GrayDetectorInfo)
                .Include(x => x.StickingParcelInfo)
                .Include(x => x.ParcelPositionInfo)
                .Include(x => x.BarCodeInfos)
                .Include(x => x.WeightInfos)
                .Include(x => x.ApiRequests)
                .Include(x => x.CommandInfos)
                .Include(x => x.ImageInfos)
                .Include(x => x.VideoInfos)
                .AsSplitQuery();

    /// <summary>
    /// 按过滤条件执行分页查询（返回摘要读模型）。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetPagedAsync(
        ParcelQueryFilter filter,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        if (filter is null) {
            throw new ArgumentNullException(nameof(filter));
        }

        if (pageRequest is null) {
            throw new ArgumentNullException(nameof(pageRequest));
        }

        try {
            ValidateQueryFilter(filter);
            var upperBound = (long)pageRequest.NormalizePageNumber() * pageRequest.NormalizePageSize();
            return _partitions is not null && _readFanoutEnabled && PreferReadFanout(filter) && upperBound <= MaxPartitionTopRows
                ? ExecuteAdaptivePageQueryAsync(filter, pageRequest, (int)upperBound, cancellationToken)
                : ExecutePagedQueryAsync((db, query) => ApplyFilter(query, filter, db.Database.ProviderName), pageRequest, cancellationToken);
        }
        catch (ValidationException ex) {
            Logger.Warn(
                ex,
                "分页查询包裹摘要参数校验失败，Filter={@Filter}, PageNumber={PageNumber}, PageSize={PageSize}",
                filter,
                pageRequest.PageNumber,
                pageRequest.PageSize);
            throw;
        }
    }

    /// <summary>
    /// 按过滤条件执行游标分页查询（返回摘要读模型）。
    /// </summary>
    public Task<CursorPageResult<ParcelSummaryReadModel>> GetCursorPagedAsync(
        ParcelQueryFilter filter,
        CursorPageRequest pageRequest,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(pageRequest);

        try {
            ValidateQueryFilter(filter);
            return _partitions is null || !_readFanoutEnabled || !PreferReadFanout(filter)
                ? ExecuteCursorQueryAsync(
                    (db, query) => ApplyFilter(query, filter, db.Database.ProviderName)
                        .ApplyCursorCondition(pageRequest), pageRequest, cancellationToken)
                : ExecuteAdaptiveCursorQueryAsync(filter, pageRequest, cancellationToken);
        }
        catch (ValidationException ex) {
            Logger.Warn(
                ex,
                "游标分页查询包裹摘要参数校验失败，Filter={@Filter}, PageSize={PageSize}, LastScannedTimeLocal={LastScannedTimeLocal}, LastId={LastId}",
                filter,
                pageRequest.PageSize,
                pageRequest.LastScannedTimeLocal,
                pageRequest.LastId);
            throw;
        }
    }

    /// <summary>
    /// 按集包号与扫码时间范围分页查询包裹摘要。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByBagCodeAsync(
        string bagCode,
        DateTime scannedTimeStart,
        DateTime scannedTimeEnd,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        var filter = BuildRequiredTimeRangeFilter(scannedTimeStart, scannedTimeEnd) with { BagCode = bagCode };
        return GetPagedAsync(filter, pageRequest, cancellationToken);
    }

    /// <summary>
    /// 按工作台与扫码时间范围分页查询包裹摘要。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByWorkstationNameAsync(
        string workstationName,
        DateTime scannedTimeStart,
        DateTime scannedTimeEnd,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        var filter = BuildRequiredTimeRangeFilter(scannedTimeStart, scannedTimeEnd) with { WorkstationName = workstationName };
        return GetPagedAsync(filter, pageRequest, cancellationToken);
    }

    /// <summary>
    /// 按包裹状态与扫码时间范围分页查询包裹摘要。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByStatusAsync(
        ParcelStatus status,
        DateTime scannedTimeStart,
        DateTime scannedTimeEnd,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        var filter = BuildRequiredTimeRangeFilter(scannedTimeStart, scannedTimeEnd) with { Status = status };
        return GetPagedAsync(filter, pageRequest, cancellationToken);
    }

    /// <summary>
    /// 按实际/目标格口与扫码时间范围分页查询包裹摘要。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByChuteAsync(
        long? actualChuteId,
        long? targetChuteId,
        DateTime scannedTimeStart,
        DateTime scannedTimeEnd,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        if (!actualChuteId.HasValue && !targetChuteId.HasValue) {
            Logger.Warn("按格口查询参数非法：actualChuteId 与 targetChuteId 同时为空。");
            throw new ArgumentException("actualChuteId 与 targetChuteId 至少提供一个格口 Id。");
        }

        var filter = BuildRequiredTimeRangeFilter(scannedTimeStart, scannedTimeEnd) with {
            ActualChuteId = actualChuteId,
            TargetChuteId = targetChuteId
        };

        return GetPagedAsync(filter, pageRequest, cancellationToken);
    }

    /// <summary>
    /// 按包裹 Id 查询前后邻近记录（稳定顺序：ScannedTime, Id）。
    /// </summary>
    public async Task<RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>> GetAdjacentByIdAsync(
        long id,
        int beforeCount,
        int afterCount,
        CancellationToken cancellationToken) {
        if (id <= 0) {
            return RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>.Fail("包裹 Id 必须大于 0。");
        }

        var normalizedBeforeCount = NormalizeAdjacentCount(beforeCount);
        var normalizedAfterCount = NormalizeAdjacentCount(afterCount);

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = await BuildPartitionQueryAsync(db, cancellationToken);
            var anchor = await query
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.ScannedTime })
                .FirstOrDefaultAsync(cancellationToken);
            if (anchor is null) {
                return RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>.Fail($"未找到 Id 为 {id} 的资源。");
            }

            var beforeItems = await query
                .Where(x => x.Id != anchor.Id
                            && (x.ScannedTime < anchor.ScannedTime
                                || (x.ScannedTime == anchor.ScannedTime && x.Id < anchor.Id)))
                .OrderByDescending(x => x.ScannedTime)
                .ThenByDescending(x => x.Id)
                .Take(normalizedBeforeCount)
                .Select(SelectSummaryExpression)
                .ToListAsync(cancellationToken);

            beforeItems.Reverse();

            var afterItems = await query
                .Where(x => x.Id != anchor.Id
                            && (x.ScannedTime > anchor.ScannedTime
                                || (x.ScannedTime == anchor.ScannedTime && x.Id > anchor.Id)))
                .OrderBy(x => x.ScannedTime)
                .ThenBy(x => x.Id)
                .Take(normalizedAfterCount)
                .Select(SelectSummaryExpression)
                .ToListAsync(cancellationToken);

            return RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>.Success([.. beforeItems, .. afterItems]);
        }
        catch (Exception ex) {
            Logger.Error(ex,
                "按包裹 Id 查询邻近记录失败，Id={ParcelId}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                id,
                beforeCount,
                afterCount);
            throw;
        }
    }

    /// <summary>
    /// 按创建时间清理过期包裹（危险动作：受隔离器开关、dry-run 与审计约束）。
    /// </summary>
    public async Task<RepositoryResult<DangerousBatchActionResult>> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken, ParcelCleanupOperator? auditOperator = null) {
        ParcelCleanupAudit? audit = null;
        var auditSaved = false;
        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            // 隔离器仍可被部署明确设置为阻断或演练。
            var isolationDecision = _removeExpiredDecision;

            // 步骤 2：先统计计划处理量（遵循单次上限），用于阻断/dry-run/执行三种分支统一审计。
            var plannedCount = await CountPlannedExpiredAsync(db, createdBefore, cancellationToken);
            audit = new ParcelCleanupAudit {
                Id = Guid.NewGuid().ToString("N"),
                Operator = auditOperator ?? new("system", "system", "系统内部操作", "", ""),
                CreatedBefore = createdBefore, PlannedCount = plannedCount, StorageFormat = ParcelCleanupAudit.SummaryStorageFormat,
                Decision = isolationDecision switch { ActionIsolationDecision.BlockedByGuard => "blocked", ActionIsolationDecision.DryRunOnly => "dry-run", _ => "execute" },
                CompensationBoundary = RemoveExpiredCompensationBoundary
            };
            db.Add(new ManagedDocument { Key = ParcelCleanupAudit.Prefix + audit.Id, Json = JsonSerializer.Serialize(audit, CleanupJsonOptions), Revision = 1, ModifiedAt = DateTime.Now });
            // 永久记录无法保存时立即终止，不能发生没有记录的删除。
            await db.SaveChangesAsync(cancellationToken);
            auditSaved = true;

            if (isolationDecision == ActionIsolationDecision.BlockedByGuard) {
                EmitRemoveExpiredAuditLog(
                    createdBefore,
                    plannedCount,
                    executedCount: 0,
                    dryRun: false,
                    blockedByGuard: true,
                    reason: "blocked-by-guard");
                await CompleteCleanupAuditAsync(audit with { Status = "completed", CompletedAtLocal = DateTime.Now }, cancellationToken);
                return RepositoryResult<DangerousBatchActionResult>.Success(BuildDangerousBatchActionResult(
                    isolationDecision,
                    plannedCount,
                    executedCount: 0) with { CleanupRecordId = audit.Id });
            }

            if (isolationDecision == ActionIsolationDecision.DryRunOnly) {
                EmitRemoveExpiredAuditLog(
                    createdBefore,
                    plannedCount,
                    executedCount: 0,
                    dryRun: true,
                    blockedByGuard: false,
                    reason: "dry-run");
                await CompleteCleanupAuditAsync(audit with { Status = "completed", CompletedAtLocal = DateTime.Now }, cancellationToken);
                return RepositoryResult<DangerousBatchActionResult>.Success(BuildDangerousBatchActionResult(
                    isolationDecision,
                    plannedCount,
                    executedCount: 0) with { CleanupRecordId = audit.Id });
            }

            // 每批删除、精简提交凭据及准确的进度计数共用数据库事务。
            var suffixes = _partitions is null ? new[] { string.Empty } : (await _partitions.GetReadSuffixesAsync(cancellationToken)).Reverse().ToArray();
            foreach (var suffix in suffixes) {
              await using var physical = _partitions is null ? await ContextFactory.CreateDbContextAsync(cancellationToken) : await _partitions.CreateContextAsync(suffix, cancellationToken);
              while (audit.ExecutedCount < MaxExpiredDeleteCountPerCall) {
                var remainingDeleteBudget = MaxExpiredDeleteCountPerCall - audit.ExecutedCount;
                var currentBatchSize = Math.Min(remainingDeleteBudget, ExpiredDeleteBatchSize);
                var originalCount = audit.ExecutedCount;
                audit = await DeleteAuditedExpiredBatchAsync(physical, audit, createdBefore, currentBatchSize, cancellationToken);
                if (audit.ExecutedCount == originalCount) break;
              }
              if (audit.ExecutedCount >= MaxExpiredDeleteCountPerCall) break;
            }

            // 步骤 5：如果触达上限则记录告警，防止误调用造成大范围清理。
            if (audit.ExecutedCount >= MaxExpiredDeleteCountPerCall) {
                Logger.Warn(
                    "删除过期包裹触达单次上限，TotalDeleted={TotalDeleted}, CreatedBefore={CreatedBefore}, Limit={DeleteLimit}",
                    audit.ExecutedCount,
                    createdBefore,
                    MaxExpiredDeleteCountPerCall);
            }

            EmitRemoveExpiredAuditLog(
                createdBefore,
                plannedCount,
                executedCount: audit.ExecutedCount,
                dryRun: false,
                blockedByGuard: false,
                reason: "executed");
            await CompleteCleanupAuditAsync(audit with { Status = "completed", CompletedAtLocal = DateTime.Now }, cancellationToken);
            return RepositoryResult<DangerousBatchActionResult>.Success(BuildDangerousBatchActionResult(
                isolationDecision,
                plannedCount,
                executedCount: audit.ExecutedCount) with { CleanupRecordId = audit.Id });
        }
        catch (OperationCanceledException ex) {
            if (auditSaved) await TryFailCleanupAuditAsync(audit!, "cancelled");
            Logger.Warn(ex, "删除过期包裹操作被取消，CreatedBefore={CreatedBefore}", createdBefore);
            EmitRemoveExpiredAuditLog(
                createdBefore,
                plannedCount: 0,
                executedCount: 0,
                dryRun: false,
                blockedByGuard: false,
                reason: "cancelled");
            return RepositoryResult<DangerousBatchActionResult>.Fail("操作已取消");
        }
        catch (Exception ex) {
            if (auditSaved) await TryFailCleanupAuditAsync(audit!, "failed");
            Logger.Error(ex, "删除过期包裹失败，CreatedBefore={CreatedBefore}", createdBefore);
            EmitRemoveExpiredAuditLog(
                createdBefore,
                plannedCount: 0,
                executedCount: 0,
                dryRun: false,
                blockedByGuard: false,
                reason: $"failed:{ex.Message}");
            return RepositoryResult<DangerousBatchActionResult>.Fail("删除过期包裹失败");
        }
    }

    /// <summary>受执行策略保护的原子批次，重试前检查固定批次凭据，避免提交响应丢失后重复删除。</summary>
    private static async Task<ParcelCleanupAudit> DeleteAuditedExpiredBatchAsync(SortingHubDbContext physical, ParcelCleanupAudit audit, DateTime createdBefore, int batchSize, CancellationToken ct) {
        var batchKey = ParcelCleanupAudit.BatchPrefix(audit.Id) + (audit.BatchCount + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        return await physical.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            physical.ChangeTracker.Clear();
            await using var transaction = physical.Database.IsRelational() ? await physical.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            var header = await physical.Set<ManagedDocument>().AsTracking().SingleAsync(x => x.Key == ParcelCleanupAudit.Prefix + audit.Id, ct);
            if (await physical.Set<ManagedDocument>().AnyAsync(x => x.Key == batchKey, ct))
                return JsonSerializer.Deserialize<ParcelCleanupAudit>(header.Json, CleanupJsonOptions)!;
            // 仅将有界主键集合用于本批删除，不复制条码、工作台或来源身份到清理历史。
            var ids = await physical.Set<Parcel>().Where(x => x.CreatedTime < createdBefore).OrderBy(x => x.CreatedTime).ThenBy(x => x.Id).Take(batchSize)
                .Select(x => x.Id).ToArrayAsync(ct);
            if (ids.Length == 0) return audit;
            int deleted;
            if (physical.Database.IsRelational()) deleted = await physical.Set<Parcel>().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
            else {
                var parcels = await physical.Set<Parcel>().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
                physical.RemoveRange(parcels); deleted = parcels.Count;
            }
            if (deleted != ids.Length) throw new InvalidOperationException("删除数量与批次计划不一致，本批次已回滚。");
            var updated = audit with { ExecutedCount = audit.ExecutedCount + deleted, BatchCount = audit.BatchCount + 1 };
            var receipt = new ParcelCleanupBatchAudit { DeletedCount = deleted, PartitionSuffix = physical.ParcelPartitionSuffix, CommittedAtLocal = DateTime.Now };
            physical.Add(new ManagedDocument { Key = batchKey, Json = JsonSerializer.Serialize(receipt, CleanupJsonOptions), Revision = 1, ModifiedAt = receipt.CommittedAtLocal });
            header.Json = JsonSerializer.Serialize(updated, CleanupJsonOptions); header.Revision++; header.ModifiedAt = DateTime.Now;
            await physical.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return updated;
        });
    }

    /// <summary>完成操作记录，保留已提交批次的精确计数。</summary>
    private async Task CompleteCleanupAuditAsync(ParcelCleanupAudit audit, CancellationToken ct) {
        await using var db = await ContextFactory.CreateDbContextAsync(ct);
        var header = await db.Set<ManagedDocument>().AsTracking().SingleAsync(x => x.Key == ParcelCleanupAudit.Prefix + audit.Id, ct);
        var committed = JsonSerializer.Deserialize<ParcelCleanupAudit>(header.Json, CleanupJsonOptions)!;
        header.Json = JsonSerializer.Serialize(committed with { Status = audit.Status, CompletedAtLocal = audit.CompletedAtLocal, ErrorMessage = audit.ErrorMessage }, CleanupJsonOptions);
        header.Revision++; header.ModifiedAt = DateTime.Now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>取消或异常后用独立有界令牌标记结果；绝不把已提交删除数量覆盖为零。</summary>
    private async Task TryFailCleanupAuditAsync(ParcelCleanupAudit audit, string status) {
        try {
            using var finalization = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await CompleteCleanupAuditAsync(audit with { Status = status, CompletedAtLocal = DateTime.Now,
                ErrorMessage = status == "cancelled" ? "操作已取消，请以操作记录中的实际删除数量为准。" : "清理执行失败，已提交批次的数量已记录，未提交批次已回滚。" }, finalization.Token);
        } catch (Exception ex) { Logger.Error(ex, "永久清理记录状态更新失败，CleanupRecordId={CleanupRecordId}", audit.Id); }
    }

    /// <summary>
    /// 统计过期清理计划量（受单次上限保护）。
    /// </summary>
    private async Task<int> CountPlannedExpiredAsync(
        SortingHubDbContext db,
        DateTime createdBefore,
        CancellationToken cancellationToken) {
        // 步骤 1：在数据库侧对有界子查询执行计数，避免把主键集合传回进程。
        return await (await BuildPartitionQueryAsync(db, cancellationToken))
            .Where(x => x.CreatedTime < createdBefore)
            .Take(MaxExpiredDeleteCountPerCall)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 构建过期清理危险动作结果。
    /// </summary>
    private static DangerousBatchActionResult BuildDangerousBatchActionResult(
        ActionIsolationDecision decision,
        int plannedCount,
        int executedCount) {
        return new DangerousBatchActionResult {
            ActionName = RemoveExpiredActionName,
            Decision = decision,
            PlannedCount = plannedCount,
            ExecutedCount = executedCount,
            IsDryRun = decision == ActionIsolationDecision.DryRunOnly,
            IsBlockedByGuard = decision == ActionIsolationDecision.BlockedByGuard,
            CompensationBoundary = RemoveExpiredCompensationBoundary
        };
    }

    /// <summary>
    /// 输出过期清理动作结构化审计日志。
    /// </summary>
    private void EmitRemoveExpiredAuditLog(
        DateTime createdBefore,
        int plannedCount,
        int executedCount,
        bool dryRun,
        bool blockedByGuard,
        string reason) {
        Logger.Info(
            "仓储危险动作审计：ActionName={ActionName}, CreatedBefore={CreatedBefore}, PlannedCount={PlannedCount}, ExecutedCount={ExecutedCount}, DryRun={DryRun}, BlockedByGuard={BlockedByGuard}, CompensationBoundary={CompensationBoundary}, Reason={Reason}",
            RemoveExpiredActionName,
            createdBefore,
            plannedCount,
            executedCount,
            dryRun,
            blockedByGuard,
            RemoveExpiredCompensationBoundary,
            reason);
    }

    /// <summary>
    /// 构建必填时间范围过滤参数。
    /// </summary>
    private static ParcelQueryFilter BuildRequiredTimeRangeFilter(DateTime scannedTimeStart, DateTime scannedTimeEnd) {
        return new ParcelQueryFilter {
            ScannedTimeStart = scannedTimeStart,
            ScannedTimeEnd = scannedTimeEnd
        };
    }

    /// <summary>
    /// 执行分页查询。
    /// </summary>
    private async Task<PageResult<ParcelSummaryReadModel>> ExecutePagedQueryAsync(
        Func<SortingHubDbContext, IQueryable<Parcel>, IQueryable<Parcel>> queryBuilder,
        PageRequest pageRequest,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? suffixes = null) {
        var pageNumber = pageRequest.NormalizePageNumber();
        var pageSize = pageRequest.NormalizePageSize();

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var source = suffixes is null ? await BuildPartitionQueryAsync(db, cancellationToken)
                : ParcelPartitionQueryBuilder.BuildFromSuffixes<Parcel>(db, suffixes);
            var query = queryBuilder(db, source);

            var totalCount = pageRequest.IncludeTotalCount
                ? await query.LongCountAsync(cancellationToken)
                : 0L;
            var items = await query
                .OrderByDescending(x => x.ScannedTime)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(SelectSummaryExpression)
                .ToListAsync(cancellationToken);

            return new PageResult<ParcelSummaryReadModel> {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex) {
            Logger.Error(ex, "分页查询包裹失败，PageNumber={PageNumber}, PageSize={PageSize}", pageNumber, pageSize);
            throw;
        }
    }

    /// <summary>依据目录中实际分表数选择查询路径，避免按日分表过多时产生大量网络往返。</summary>
    private async Task<PageResult<ParcelSummaryReadModel>> ExecuteAdaptivePageQueryAsync(
        ParcelQueryFilter filter, PageRequest request, int topRows, CancellationToken cancellationToken) {
        var suffixes = await _partitions!.GetReadSuffixesAsync(cancellationToken);
        if (suffixes.Count > _readFanoutMaxPartitions)
            return await ExecutePagedQueryAsync((db, query) => ApplyFilter(query, filter, db.Database.ProviderName),
                request, cancellationToken, suffixes);
        try {
            return await ExecutePartitionPageQueryAsync(filter, request, topRows, suffixes, cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "有界分表分页查询失败，PageNumber={PageNumber}, PageSize={PageSize}, PartitionCount={PartitionCount}",
                request.PageNumber, request.PageSize, suffixes.Count);
            throw;
        }
    }

    /// <summary>每个物理周期只读取全局分页所需的前 K 行，再稳定合并；深页沿用数据库合并查询。</summary>
    private async Task<PageResult<ParcelSummaryReadModel>> ExecutePartitionPageQueryAsync(
        ParcelQueryFilter filter, PageRequest request, int topRows, IReadOnlyList<string> suffixes,
        CancellationToken cancellationToken) {
        var pageNumber = request.NormalizePageNumber();
        var pageSize = request.NormalizePageSize();
        var results = await ReadPartitionTopRowsAsync(filter, null, topRows, request.IncludeTotalCount, suffixes, cancellationToken);
        return new PageResult<ParcelSummaryReadModel> {
            Items = MergeTopRows(results, (pageNumber - 1) * pageSize, pageSize),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = request.IncludeTotalCount ? results.Sum(result => result.Count) : 0L
        };
    }

    /// <summary>依据目录中实际分表数选择游标查询路径。</summary>
    private async Task<CursorPageResult<ParcelSummaryReadModel>> ExecuteAdaptiveCursorQueryAsync(
        ParcelQueryFilter filter, CursorPageRequest request, CancellationToken cancellationToken) {
        var suffixes = await _partitions!.GetReadSuffixesAsync(cancellationToken);
        if (suffixes.Count > _readFanoutMaxPartitions)
            return await ExecuteCursorQueryAsync((db, query) => ApplyFilter(query, filter, db.Database.ProviderName)
                .ApplyCursorCondition(request), request, cancellationToken, suffixes);
        try {
            return await ExecutePartitionCursorQueryAsync(filter, request, suffixes, cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "有界分表游标查询失败，PageSize={PageSize}, PartitionCount={PartitionCount}",
                request.PageSize, suffixes.Count);
            throw;
        }
    }

    /// <summary>游标只从各周期读取 pageSize+1 行，避免数据库先物化所有历史周期再全局排序。</summary>
    private async Task<CursorPageResult<ParcelSummaryReadModel>> ExecutePartitionCursorQueryAsync(
        ParcelQueryFilter filter, CursorPageRequest request, IReadOnlyList<string> suffixes,
        CancellationToken cancellationToken) {
        var pageSize = request.NormalizePageSize();
        var results = await ReadPartitionTopRowsAsync(filter, request, pageSize + 1, false, suffixes, cancellationToken);
        var merged = MergeTopRows(results, 0, pageSize + 1);
        var hasMore = merged.Length > pageSize;
        var items = hasMore ? merged[..pageSize] : merged;
        var last = hasMore ? items[^1] : null;
        return new CursorPageResult<ParcelSummaryReadModel> {
            Items = items,
            PageSize = pageSize,
            HasMore = hasMore,
            NextScannedTimeLocal = last?.ScannedTime,
            NextId = last?.Id
        };
    }

    /// <summary>并行数固定受限，防止按日分表过多时耗尽数据库连接池。</summary>
    private async Task<(IReadOnlyList<ParcelSummaryReadModel> Items, long Count)[]> ReadPartitionTopRowsAsync(
        ParcelQueryFilter filter, CursorPageRequest? cursor, int topRows, bool includeCount,
        IReadOnlyList<string> suffixes, CancellationToken cancellationToken) {
        var results = new (IReadOnlyList<ParcelSummaryReadModel> Items, long Count)[suffixes.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, suffixes.Count),
            new ParallelOptions { MaxDegreeOfParallelism = _readFanoutConcurrency, CancellationToken = cancellationToken },
            async (index, token) => {
                await using var db = await ContextFactory.CreateDbContextAsync(token);
                var query = ApplyFilter(ParcelPartitionQueryBuilder.BuildSingle<Parcel>(db, suffixes[index]),
                    filter, db.Database.ProviderName);
                if (cursor is not null) query = query.ApplyCursorCondition(cursor);
                var count = includeCount ? await query.LongCountAsync(token) : 0L;
                var items = await query.OrderByDescending(x => x.ScannedTime).ThenByDescending(x => x.Id)
                    .Take(topRows).Select(SelectSummaryExpression).ToListAsync(token);
                results[index] = (items, count);
            });
        return results;
    }

    /// <summary>按扫码时间和主键稳定合并各分表的有界候选行。</summary>
    private static ParcelSummaryReadModel[] MergeTopRows(
        (IReadOnlyList<ParcelSummaryReadModel> Items, long Count)[] results, int skip, int take) =>
        results.SelectMany(result => result.Items)
            .OrderByDescending(item => item.ScannedTime).ThenByDescending(item => item.Id)
            .Skip(skip).Take(take).ToArray();

    /// <summary>短扫码窗口的单条数据库查询更快；缺少边界或长窗口才走有界分表读取。</summary>
    private static bool PreferReadFanout(ParcelQueryFilter filter) =>
        string.IsNullOrWhiteSpace(filter.BarCodeKeyword)
        && (!filter.ScannedTimeStart.HasValue || !filter.ScannedTimeEnd.HasValue
            || filter.ScannedTimeEnd.Value - filter.ScannedTimeStart.Value > TimeSpan.FromDays(2));

    /// <summary>单个分表允许提取的最大候选行数，深页改用原有数据库查询。</summary>
    private const int MaxPartitionTopRows = 2000;

    /// <summary>
    /// 执行游标分页查询。
    /// </summary>
    /// <param name="queryBuilder">查询构建器。</param>
    /// <param name="pageRequest">游标分页参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>游标分页结果。</returns>
    private async Task<CursorPageResult<ParcelSummaryReadModel>> ExecuteCursorQueryAsync(
        Func<SortingHubDbContext, IQueryable<Parcel>, IQueryable<Parcel>> queryBuilder,
        CursorPageRequest pageRequest,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? suffixes = null) {
        var pageSize = pageRequest.NormalizePageSize();

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var source = suffixes is null ? await BuildPartitionQueryAsync(db, cancellationToken)
                : ParcelPartitionQueryBuilder.BuildFromSuffixes<Parcel>(db, suffixes);
            var query = queryBuilder(db, source);
            var items = await query
                .OrderByDescending(x => x.ScannedTime)
                .ThenByDescending(x => x.Id)
                .Take(pageSize + 1)
                .Select(SelectSummaryExpression)
                .ToListAsync(cancellationToken);

            var hasMore = items.Count > pageSize;
            if (hasMore) {
                items.RemoveAt(pageSize);
            }

            var pageItems = items.ToArray();
            var nextItem = hasMore && pageItems.Length > 0
                ? pageItems[^1]
                : null;

            return new CursorPageResult<ParcelSummaryReadModel> {
                Items = pageItems,
                PageSize = pageSize,
                HasMore = hasMore,
                NextScannedTimeLocal = nextItem?.ScannedTime,
                NextId = nextItem?.Id
            };
        }
        catch (Exception ex) {
            Logger.Error(
                ex,
                "游标分页查询包裹失败，PageSize={PageSize}, LastScannedTimeLocal={LastScannedTimeLocal}, LastId={LastId}",
                pageSize,
                pageRequest.LastScannedTimeLocal,
                pageRequest.LastId);
            throw;
        }
    }

    /// <summary>
    /// 应用过滤条件。
    /// </summary>
    /// <param name="query">基础查询。</param>
    /// <param name="filter">过滤参数。</param>
    /// <param name="providerName">当前数据库提供器名称。</param>
    private static IQueryable<Parcel> ApplyFilter(IQueryable<Parcel> query, ParcelQueryFilter filter, string? providerName) {
        if (!string.IsNullOrWhiteSpace(filter.BarCodeKeyword)) {
            var barCodeKeyword = filter.BarCodeKeyword.Trim();
            // 分表和基础表使用一致的子串检索语义。
            query = query.Where(x => x.BarCodes.Contains(barCodeKeyword));
        }

        if (!string.IsNullOrWhiteSpace(filter.BagCode)) {
            var bagCode = filter.BagCode.Trim();
            query = query.Where(x => x.BagCode == bagCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkstationName)) {
            var workstationName = filter.WorkstationName.Trim();
            query = query.Where(x => x.WorkstationName == workstationName);
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceInstanceId)) {
            var source = filter.SourceInstanceId.Trim();
            if (source.Length > 96) throw new ArgumentException("来源实例编码不能超过96字符。", nameof(filter));
            query = providerName switch {
                DbProviderNames.MySql => query.Where(x => EF.Functions.Collate(x.SourceInstanceId!.Trim(), "utf8mb4_bin") == source),
                DbProviderNames.SqlServer => query.Where(x => EF.Functions.Collate(x.SourceInstanceId!.Trim(), "Latin1_General_100_BIN2") == source),
                _ => query.Where(x => x.SourceInstanceId != null && x.SourceInstanceId.Trim() == source)
            };
        }

        if (filter.Status.HasValue) {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (filter.ExceptionType.HasValue) {
            query = query.Where(x => x.ExceptionType == filter.ExceptionType.Value);
        }

        if (filter.ActualChuteId.HasValue) {
            query = query.Where(x => x.ActualChuteId == filter.ActualChuteId.Value);
        }

        if (filter.TargetChuteId.HasValue) {
            query = query.Where(x => x.TargetChuteId == filter.TargetChuteId.Value);
        }

        if (filter.ScannedTimeStart.HasValue) {
            query = query.Where(x => x.ScannedTime >= filter.ScannedTimeStart.Value);
        }

        if (filter.ScannedTimeEnd.HasValue) {
            query = query.Where(x => x.ScannedTime <= filter.ScannedTimeEnd.Value);
        }

        return query;
    }

    /// <summary>在数据库内合并全部已登记的包裹分表，分页与排序只执行一次；表名全部来自受校验目录。</summary>
    private async Task<IQueryable<Parcel>> BuildPartitionQueryAsync(SortingHubDbContext db, CancellationToken cancellationToken) {
        if (_partitions is null) return Query(db);
        return await ParcelPartitionQueryBuilder.BuildAsync<Parcel>(db, _partitions, cancellationToken);
    }

    /// <summary>使用全局定位创建读取或修改指定包裹的上下文。</summary>
    private async Task<SortingHubDbContext> CreateContextForIdAsync(long id, CancellationToken cancellationToken) => _partitions is null
        ? await ContextFactory.CreateDbContextAsync(cancellationToken)
        : await _partitions.CreateContextAsync(await _partitions.LocateAsync(id, cancellationToken), cancellationToken);

    /// <summary>按固定定位更新聚合，后续阶段不会重新计算分表。</summary>
    public override async Task<RepositoryResult> UpdateAsync(Parcel parcel, CancellationToken cancellationToken) {
        try {
            await using var db = await CreateContextForIdAsync(parcel.Id, cancellationToken);
            var stored = await BuildDetailQuery(db).AsTracking().SingleOrDefaultAsync(x => x.Id == parcel.Id, cancellationToken);
            if (stored is null) return RepositoryResult.Fail("包裹不存在。");
            // 步骤1：更新已跟踪主表，保留查询返回对象中不存在的EF影子主键。
            db.Entry(stored).CurrentValues.SetValues(parcel);
            // 步骤2：记录集合按领域值追加，读取后再次更新不会把已有明细当作新记录插入。
            foreach (var value in parcel.BarCodeInfos.Except(stored.BarCodeInfos)) stored.AddBarCodeInfo(value);
            foreach (var value in parcel.WeightInfos.Except(stored.WeightInfos)) stored.AddWeightInfo(value);
            foreach (var value in parcel.ApiRequests.Except(stored.ApiRequests)) stored.AddApiRequest(value);
            foreach (var value in parcel.CommandInfos.Except(stored.CommandInfos)) stored.AddCommandInfo(value);
            foreach (var value in parcel.ImageInfos.Except(stored.ImageInfos)) stored.AddImageInfo(value);
            foreach (var value in parcel.VideoInfos.Except(stored.VideoInfos)) stored.AddVideoInfo(value);
            if (parcel.VolumeInfo is not null && !Equals(parcel.VolumeInfo, stored.VolumeInfo)) stored.SetVolumeInfo(parcel.VolumeInfo);
            if (parcel.ChuteInfo is not null && !Equals(parcel.ChuteInfo, stored.ChuteInfo)) stored.SetChuteInfo(parcel.ChuteInfo);
            if (parcel.SorterCarrierInfo is not null && !Equals(parcel.SorterCarrierInfo, stored.SorterCarrierInfo)) stored.SetSorterCarrierInfo(parcel.SorterCarrierInfo);
            if (parcel.DeviceInfo is not null && !Equals(parcel.DeviceInfo, stored.DeviceInfo)) stored.SetDeviceInfo(parcel.DeviceInfo);
            if (parcel.GrayDetectorInfo is not null && !Equals(parcel.GrayDetectorInfo, stored.GrayDetectorInfo)) stored.SetGrayDetectorInfo(parcel.GrayDetectorInfo);
            if (parcel.StickingParcelInfo is not null && !Equals(parcel.StickingParcelInfo, stored.StickingParcelInfo)) stored.SetStickingParcelInfo(parcel.StickingParcelInfo);
            if (parcel.ParcelPositionInfo is not null && !Equals(parcel.ParcelPositionInfo, stored.ParcelPositionInfo)) stored.SetParcelPositionInfo(parcel.ParcelPositionInfo);
            if (parcel.BagInfo is not null && !Equals(parcel.BagInfo, stored.BagInfo)) {
                var bag = await db.Set<Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects.BagInfo>().AsTracking().SingleOrDefaultAsync(x => x.BagCode == parcel.BagInfo.BagCode, cancellationToken);
                if (bag is not null) db.Entry(bag).CurrentValues.SetValues(parcel.BagInfo);
                stored.SetBagInfo(bag ?? parcel.BagInfo);
            }
            await db.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (Exception ex) { Logger.Error(ex, "更新包裹失败，Id={Id}", parcel.Id); return RepositoryResult.Fail("更新包裹失败"); }
    }

    /// <summary>在包裹原始分表删除聚合，保留全局身份与追加事实审计。</summary>
    public override async Task<RepositoryResult> RemoveAsync(Parcel parcel, CancellationToken cancellationToken) {
        try {
            await using var db = await CreateContextForIdAsync(parcel.Id, cancellationToken);
            db.Remove(parcel);
            await db.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (Exception ex) { Logger.Error(ex, "删除包裹失败，Id={Id}", parcel.Id); return RepositoryResult.Fail("删除包裹失败"); }
    }

    /// <summary>按首次入库周期批量保存，所有分表与全局索引共用一个数据库事务。</summary>
    public override async Task<RepositoryResult> AddRangeAsync(IReadOnlyCollection<Parcel> parcels, CancellationToken cancellationToken) {
        if (_partitions is null) return await base.AddRangeAsync(parcels, cancellationToken);
        if (parcels is null || parcels.Count == 0) return RepositoryResult.Fail("实体集合不能为空");
        try {
            var ids = parcels.Select(x => x.Id).ToArray();
            if (ids.Distinct().Count() != ids.Length) return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
            await using var lookup = await ContextFactory.CreateDbContextAsync(cancellationToken);
            if (await lookup.Set<Parcel>().AnyAsync(x => ids.Contains(x.Id), cancellationToken) || await lookup.Set<ParcelLocation>().AnyAsync(x => ids.Contains(x.Id), cancellationToken))
                return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
            var groups = parcels.GroupBy(x => _partitions.Resolve(x.CreatedTime).Suffix).ToArray();
            foreach (var group in groups) await _partitions.EnsureCreatedAsync(_partitions.Resolve(group.First().CreatedTime), cancellationToken);
            await using var template = await ContextFactory.CreateDbContextAsync(cancellationToken);
            return await template.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                await using var owner = await _partitions.CreateContextAsync(groups[0].Key, cancellationToken);
                await using var transaction = await owner.Database.BeginTransactionAsync(cancellationToken);
                foreach (var group in groups) {
                    await using var db = await _partitions.CreateContextAsync(group.Key, cancellationToken);
                    db.Database.SetDbConnection(owner.Database.GetDbConnection(), contextOwnsConnection: false);
                    await db.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
                    db.AddRange(group);
                    db.AddRange(group.Select(x => new ParcelLocation { Id = x.Id, Suffix = group.Key, CreatedTime = x.CreatedTime }));
                    await db.SaveChangesAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                return RepositoryResult.Success();
            });
        }
        catch (Exception ex) { Logger.Error(ex, "批量保存分表包裹失败"); return RepositoryResult.Fail("批量新增包裹失败"); }
    }

    /// <summary>校验查询过滤参数。</summary>
    private static void ValidateQueryFilter(ParcelQueryFilter filter) {
        var validationContext = new ValidationContext(filter);
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(filter, validationContext, validationResults, validateAllProperties: true)) {
            return;
        }

        var errorMessage = string.Join("; ", validationResults.Select(static x => x.ErrorMessage));
        throw new ValidationException(string.IsNullOrWhiteSpace(errorMessage) ? "查询参数校验失败" : errorMessage);
    }

    /// <summary>
    /// 归一化邻近查询条数，限制单侧最大返回量。
    /// 上限以 <see cref="IParcelRepository.MaxAdjacentCountPerSide"/> 为唯一权威来源。
    /// </summary>
    private static int NormalizeAdjacentCount(int count) {
        if (count <= 0) {
            return 0;
        }

        return Math.Min(count, IParcelRepository.MaxAdjacentCountPerSide);
    }

    /// <summary>
    /// Parcel 摘要投影表达式。
    /// </summary>
    private static readonly Expression<Func<Parcel, ParcelSummaryReadModel>> SelectSummaryExpression = x => new ParcelSummaryReadModel {
        SourceInstanceId = x.SourceInstanceId,
        SourceRunId = x.SourceRunId,
        SourceParcelId = x.SourceParcelId,
        DetectedTime = x.DetectedTime,
        MeasurementTime = x.MeasurementTime,
        TargetChuteCode = x.TargetChuteCode,
        ActualChuteCode = x.ActualChuteCode,
        TaskCode = x.TaskCode,
        VolumetricWeightGrams = x.VolumetricWeightGrams,
        IsFallbackChuteAssigned = x.IsFallbackChuteAssigned,
        IsRoutingBlocked = x.IsRoutingBlocked,
        SourceExceptionCode = x.SourceExceptionCode,
        Id = x.Id,
        CreatedTime = x.CreatedTime,
        ModifyTime = x.ModifyTime,
        ModifyIp = x.ModifyIp,
        ParcelTimestamp = x.ParcelTimestamp,
        Type = x.Type,
        Status = x.Status,
        ExceptionType = x.ExceptionType,
        NoReadType = x.NoReadType,
        SorterCarrierId = x.SorterCarrierId,
        SegmentCodes = x.SegmentCodes,
        LifecycleMilliseconds = x.LifecycleMilliseconds,
        TargetChuteId = x.TargetChuteId,
        ActualChuteId = x.ActualChuteId,
        BarCodes = x.BarCodes,
        Weight = x.Weight,
        RequestStatus = x.RequestStatus,
        BagCode = x.BagCode,
        WorkstationName = x.WorkstationName,
        IsSticking = x.IsSticking,
        Length = x.Length,
        Width = x.Width,
        Height = x.Height,
        Volume = x.Volume,
        ScannedTime = x.ScannedTime,
        DischargeTime = x.DischargeTime,
        CompletedTime = x.CompletedTime,
        HasImages = x.HasImages,
        HasVideos = x.HasVideos,
        Coordinate = x.Coordinate
    };
}
}
