using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
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

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

/// <summary>
/// Parcel 仓储第一阶段实现。
/// </summary>
public sealed class ParcelRepository : RepositoryBase<Parcel, SortingHubDbContext>, IParcelRepository {
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
    private const string RemoveExpiredCompensationBoundary = "当前为物理删除，仓库未内建自动回滚脚本；采用默认阻断 + dry-run + 审计 + 显式开关的保守治理边界。";

    /// <summary>
    /// 过期清理隔离器开关配置键。
    /// </summary>
    internal const string RemoveExpiredEnableGuardConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:EnableGuard";

    /// <summary>
    /// 过期清理允许执行危险动作配置键。
    /// </summary>
    internal const string RemoveExpiredAllowExecutionConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:AllowDangerousActionExecution";

    /// <summary>
    /// 过期清理 dry-run 配置键。
    /// </summary>
    internal const string RemoveExpiredDryRunConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:DryRun";

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
    /// 是否启用过期清理危险动作守卫。
    /// </summary>
    private readonly bool _removeExpiredEnableGuard;

    /// <summary>
    /// 是否允许执行过期清理危险动作。
    /// </summary>
    private readonly bool _removeExpiredAllowDangerousActionExecution;

    /// <summary>
    /// 是否启用过期清理 dry-run。
    /// </summary>
    private readonly bool _removeExpiredDryRun;

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
        IConfiguration? configuration)
        : base(contextFactory, NLogLogger) {
        var effectiveConfiguration = configuration ?? EmptyConfiguration;
        // 步骤 1：守卫开关默认开启（保守默认值，避免危险动作默认放开）。
        _removeExpiredEnableGuard = AutoTuningConfigurationHelper.GetBoolOrDefault(
            effectiveConfiguration,
            RemoveExpiredEnableGuardConfigKey,
            true);
        // 步骤 2：危险动作执行默认关闭（仅显式放开时才允许真实删除）。
        _removeExpiredAllowDangerousActionExecution = AutoTuningConfigurationHelper.GetBoolOrDefault(
            effectiveConfiguration,
            RemoveExpiredAllowExecutionConfigKey,
            false);
        // 步骤 3：dry-run 默认开启，确保未显式放开前只审计不落地。
        _removeExpiredDryRun = AutoTuningConfigurationHelper.GetBoolOrDefault(
            effectiveConfiguration,
            RemoveExpiredDryRunConfigKey,
            true);
    }

    /// <summary>
    /// 新增包裹聚合。
    /// </summary>
    public override async Task<RepositoryResult> AddAsync(Parcel parcel, CancellationToken cancellationToken) {
        if (parcel is null) {
            return RepositoryResult.Fail("实体不能为空");
        }

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            await db.Set<Parcel>().AddAsync(parcel, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (OperationCanceledException ex) {
            Logger.Warn(ex, "新增包裹操作被取消，Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail("操作已取消");
        }
        catch (DbUpdateException ex) when (IsDuplicatePrimaryKeyException(ex)) {
            Logger.Warn(ex, "新增包裹主键冲突，Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        catch (DbUpdateException ex) when (ContainsDuplicateKeyMessage(ex.Message) || ContainsDuplicateKeyMessage(ex.InnerException?.Message)) {
            Logger.Warn(ex, "新增包裹主键冲突（DbUpdateException 回退分支），Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        // InMemory Provider(当前测试基线 .NET8 + EFCore.InMemory 9.x) 在主键冲突场景下通常抛出 InvalidOperationException，
        // 且无稳定错误码，仅有消息文本。该分支仅为测试基础设施兼容兜底；真实数据库优先走上方错误码分支。
        catch (InvalidOperationException ex) when (ContainsDuplicateKeyMessage(ex.Message)) {
            Logger.Warn(ex, "新增包裹主键冲突（提供器回退分支），Id={ParcelId}", parcel.Id);
            return RepositoryResult.Fail(DuplicateParcelIdErrorMessage, RepositoryErrorCodes.ParcelIdConflict);
        }
        catch (Exception ex) when (ContainsDuplicateKeyMessage(ex.Message) || ContainsDuplicateKeyMessage(ex.InnerException?.Message)) {
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
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            return await Query(db)
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
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "根据 Id 查询包裹详情失败，Id={ParcelId}", id);
            throw;
        }
    }

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
            return ExecutePagedQueryAsync((db, query) => ApplyFilter(query, filter, db.Database.ProviderName), pageRequest, cancellationToken);
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
            var query = Query(db);
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
    public async Task<RepositoryResult<DangerousBatchActionResult>> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken) {
        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            // 步骤 1：先受控评估本次动作决策（守卫优先于 dry-run，默认将先阻断危险动作，避免误删）。
            var isolationDecision = ActionIsolationPolicy.Evaluate(
                _removeExpiredEnableGuard,
                _removeExpiredAllowDangerousActionExecution,
                _removeExpiredDryRun,
                dangerousAction: true,
                isRollback: false);

            // 步骤 2：先统计计划处理量（遵循单次上限），用于阻断/dry-run/执行三种分支统一审计。
            var plannedCount = await CountPlannedExpiredAsync(db, createdBefore, cancellationToken);

            if (isolationDecision == ActionIsolationDecision.BlockedByGuard) {
                EmitRemoveExpiredAuditLog(
                    createdBefore,
                    plannedCount,
                    executedCount: 0,
                    dryRun: false,
                    blockedByGuard: true,
                    reason: "blocked-by-guard");
                return RepositoryResult<DangerousBatchActionResult>.Success(BuildDangerousBatchActionResult(
                    isolationDecision,
                    plannedCount,
                    executedCount: 0));
            }

            if (isolationDecision == ActionIsolationDecision.DryRunOnly) {
                EmitRemoveExpiredAuditLog(
                    createdBefore,
                    plannedCount,
                    executedCount: 0,
                    dryRun: true,
                    blockedByGuard: false,
                    reason: "dry-run");
                return RepositoryResult<DangerousBatchActionResult>.Success(BuildDangerousBatchActionResult(
                    isolationDecision,
                    plannedCount,
                    executedCount: 0));
            }

            var totalDeleted = 0;

            // 步骤 3：按批次真实删除，保留单次上限保护，避免长事务与大批量误删风险。
            while (totalDeleted < MaxExpiredDeleteCountPerCall) {
                var remainingDeleteBudget = MaxExpiredDeleteCountPerCall - totalDeleted;
                var currentBatchSize = Math.Min(remainingDeleteBudget, ExpiredDeleteBatchSize);

                var expiredBatch = await db.Set<Parcel>()
                    .Where(x => x.CreatedTime < createdBefore)
                    .Take(currentBatchSize)
                    .ToListAsync(cancellationToken);

                if (expiredBatch.Count == 0) {
                    break;
                }

                // 步骤 4：通过 EF 跟踪删除当前批次并立即提交，缩短事务占用时间。
                db.Set<Parcel>().RemoveRange(expiredBatch);
                await db.SaveChangesAsync(cancellationToken);
                totalDeleted += expiredBatch.Count;
            }

            // 步骤 5：如果触达上限则记录告警，防止误调用造成大范围清理。
            if (totalDeleted >= MaxExpiredDeleteCountPerCall) {
                Logger.Warn(
                    "删除过期包裹触达单次上限，TotalDeleted={TotalDeleted}, CreatedBefore={CreatedBefore}, Limit={DeleteLimit}",
                    totalDeleted,
                    createdBefore,
                    MaxExpiredDeleteCountPerCall);
            }

            EmitRemoveExpiredAuditLog(
                createdBefore,
                plannedCount,
                executedCount: totalDeleted,
                dryRun: false,
                blockedByGuard: false,
                reason: "executed");
            return RepositoryResult<DangerousBatchActionResult>.Success(BuildDangerousBatchActionResult(
                isolationDecision,
                plannedCount,
                executedCount: totalDeleted));
        }
        catch (OperationCanceledException ex) {
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

    /// <summary>
    /// 统计过期清理计划量（受单次上限保护）。
    /// </summary>
    private static async Task<int> CountPlannedExpiredAsync(
        SortingHubDbContext db,
        DateTime createdBefore,
        CancellationToken cancellationToken) {
        // 步骤 1：在数据库侧按单次上限读取主键集合，确保统计开销具备明确上界。
        var plannedIds = await db.Set<Parcel>()
            .AsNoTracking()
            .Where(x => x.CreatedTime < createdBefore)
            .Select(x => x.Id)
            .Take(MaxExpiredDeleteCountPerCall)
            .ToListAsync(cancellationToken);
        // 步骤 2：计划处理量即为上界化读取后的候选计数。
        return plannedIds.Count;
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
        CancellationToken cancellationToken) {
        var pageNumber = pageRequest.NormalizePageNumber();
        var pageSize = pageRequest.NormalizePageSize();

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = queryBuilder(db, Query(db));

            var totalCount = await query.LongCountAsync(cancellationToken);
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

    /// <summary>
    /// 应用过滤条件。
    /// </summary>
    /// <param name="query">基础查询。</param>
    /// <param name="filter">过滤参数。</param>
    /// <param name="providerName">当前数据库提供器名称。</param>
    private static IQueryable<Parcel> ApplyFilter(IQueryable<Parcel> query, ParcelQueryFilter filter, string? providerName) {
        if (!string.IsNullOrWhiteSpace(filter.BarCodeKeyword)) {
            var barCodeKeyword = filter.BarCodeKeyword.Trim();
            // 步骤 1：MySQL 优先使用 MATCH...AGAINST(Boolean Mode)；其他 Provider 回退到 Contains 子串匹配。
            query = string.Equals(providerName, DbProviderNames.MySql, StringComparison.Ordinal)
                ? query.Where(x => EF.Functions.IsMatch(x.BarCodes, barCodeKeyword, MySqlMatchSearchMode.Boolean))
                : query.Where(x => x.BarCodes.Contains(barCodeKeyword));
        }

        if (!string.IsNullOrWhiteSpace(filter.BagCode)) {
            var bagCode = filter.BagCode.Trim();
            query = query.Where(x => x.BagCode == bagCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkstationName)) {
            var workstationName = filter.WorkstationName.Trim();
            query = query.Where(x => x.WorkstationName == workstationName);
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

    /// <summary>
    /// 校验查询过滤参数。
    /// </summary>
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
    /// 判断是否为主键唯一约束冲突异常。
    /// </summary>
    /// <param name="exception">数据库更新异常。</param>
    /// <returns>是否为主键冲突。</returns>
    private static bool IsDuplicatePrimaryKeyException(DbUpdateException exception) {
        if (exception.InnerException is MySqlException mySqlException) {
            return mySqlException.Number == 1062;
        }

        if (exception.InnerException is SqlException sqlException) {
            return sqlException.Number == 2627 || sqlException.Number == 2601;
        }

        return false;
    }

    /// <summary>
    /// 判断异常消息是否包含“重复键”语义。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <returns>是否包含重复键语义。</returns>
    private static bool ContainsDuplicateKeyMessage(string? message) {
        if (string.IsNullOrWhiteSpace(message)) {
            return false;
        }

        return message.Contains("same key", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("已存在", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parcel 摘要投影表达式。
    /// </summary>
    private static readonly Expression<Func<Parcel, ParcelSummaryReadModel>> SelectSummaryExpression = x => new ParcelSummaryReadModel {
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
