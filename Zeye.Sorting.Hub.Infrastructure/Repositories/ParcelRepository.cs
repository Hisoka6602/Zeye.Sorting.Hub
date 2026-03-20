using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

/// <summary>
/// Parcel 仓储第一阶段实现。
/// </summary>
public sealed class ParcelRepository : RepositoryBase<Parcel, SortingHubDbContext>, IParcelRepository {
    /// <summary>
    /// 邻近查询最大返回条数（单侧）。
    /// </summary>
    private const int MaxAdjacentCountPerSide = 200;

    /// <summary>
    /// 过期数据分批删除批次大小。
    /// </summary>
    private const int ExpiredDeleteBatchSize = 1000;

    /// <summary>
    /// 单次调用过期删除最大条数保护阈值。
    /// </summary>
    private const int MaxExpiredDeleteCountPerCall = 10000;

    /// <summary>
    /// 创建 ParcelRepository。
    /// </summary>
    public ParcelRepository(
        IDbContextFactory<SortingHubDbContext> contextFactory,
        ILogger<ParcelRepository> logger)
        : base(contextFactory, logger) {
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
            Logger.LogError(ex, "根据 Id 查询包裹详情失败，Id={ParcelId}", id);
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
            return ExecutePagedQueryAsync(query => ApplyFilter(query, filter), pageRequest, cancellationToken);
        }
        catch (ValidationException ex) {
            Logger.LogWarning(
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
        var filter = BuildRequiredTimeRangeFilter(scannedTimeStart, scannedTimeEnd) with {
            ActualChuteId = actualChuteId,
            TargetChuteId = targetChuteId
        };

        return GetPagedAsync(filter, pageRequest, cancellationToken);
    }

    /// <summary>
    /// 按扫描时间查询前后邻近记录（时间顺序）。
    /// </summary>
    public async Task<IReadOnlyList<ParcelSummaryReadModel>> GetAdjacentByScannedTimeAsync(
        DateTime scannedTime,
        int beforeCount,
        int afterCount,
        CancellationToken cancellationToken) {
        var normalizedBeforeCount = NormalizeAdjacentCount(beforeCount);
        var normalizedAfterCount = NormalizeAdjacentCount(afterCount);

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = Query(db);

            var beforeItems = await query
                .Where(x => x.ScannedTime < scannedTime)
                .OrderByDescending(x => x.ScannedTime)
                .ThenByDescending(x => x.Id)
                .Take(normalizedBeforeCount)
                .Select(SelectSummaryExpression)
                .ToListAsync(cancellationToken);

            beforeItems.Reverse();

            var afterItems = await query
                .Where(x => x.ScannedTime > scannedTime)
                .OrderBy(x => x.ScannedTime)
                .ThenBy(x => x.Id)
                .Take(normalizedAfterCount)
                .Select(SelectSummaryExpression)
                .ToListAsync(cancellationToken);

            return [.. beforeItems, .. afterItems];
        }
        catch (Exception ex) {
            Logger.LogError(ex,
                "按扫描时间查询邻近记录失败，ScannedTime={ScannedTime}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                scannedTime,
                beforeCount,
                afterCount);
            throw;
        }
    }

    /// <summary>
    /// 以仓储契约方式新增包裹聚合。
    /// </summary>
    async Task IParcelRepository.AddAsync(Parcel parcel, CancellationToken cancellationToken) {
        var result = await base.AddAsync(parcel, cancellationToken);
        if (result.IsSuccess) {
            return;
        }

        Logger.LogError("新增包裹失败，原因={ErrorMessage}", result.ErrorMessage);
        throw new InvalidOperationException(result.ErrorMessage ?? "新增包裹失败");
    }

    /// <summary>
    /// 以仓储契约方式更新包裹聚合。
    /// </summary>
    async Task IParcelRepository.UpdateAsync(Parcel parcel, CancellationToken cancellationToken) {
        var result = await base.UpdateAsync(parcel, cancellationToken);
        if (result.IsSuccess) {
            return;
        }

        Logger.LogError("更新包裹失败，原因={ErrorMessage}", result.ErrorMessage);
        throw new InvalidOperationException(result.ErrorMessage ?? "更新包裹失败");
    }

    /// <summary>
    /// 以仓储契约方式删除包裹聚合。
    /// </summary>
    async Task IParcelRepository.RemoveAsync(Parcel parcel, CancellationToken cancellationToken) {
        var result = await base.RemoveAsync(parcel, cancellationToken);
        if (result.IsSuccess) {
            return;
        }

        Logger.LogError("删除包裹失败，原因={ErrorMessage}", result.ErrorMessage);
        throw new InvalidOperationException(result.ErrorMessage ?? "删除包裹失败");
    }

    /// <summary>
    /// 按创建时间删除过期包裹，返回删除条数。
    /// </summary>
    public async Task<int> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken) {
        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var totalDeleted = 0;

            // 步骤 1：循环按批次拉取待删除记录，避免一次性加载过大数据集。
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

                // 步骤 2：通过 EF 跟踪删除当前批次并立即提交，缩短事务占用时间。
                db.Set<Parcel>().RemoveRange(expiredBatch);
                await db.SaveChangesAsync(cancellationToken);
                totalDeleted += expiredBatch.Count;
            }

            // 步骤 3：如果触达上限则记录告警，防止误调用造成大范围清理。
            if (totalDeleted >= MaxExpiredDeleteCountPerCall) {
                Logger.LogWarning(
                    "删除过期包裹触达单次上限，TotalDeleted={TotalDeleted}, CreatedBefore={CreatedBefore}, Limit={DeleteLimit}",
                    totalDeleted,
                    createdBefore,
                    MaxExpiredDeleteCountPerCall);
            }

            return totalDeleted;
        }
        catch (Exception ex) {
            Logger.LogError(ex, "删除过期包裹失败，CreatedBefore={CreatedBefore}", createdBefore);
            throw;
        }
    }

    /// <summary>
    /// 以仓储契约方式批量新增包裹聚合。
    /// </summary>
    async Task IParcelRepository.AddRangeAsync(IReadOnlyCollection<Parcel> parcels, CancellationToken cancellationToken) {
        var result = await base.AddRangeAsync(parcels, cancellationToken);
        if (result.IsSuccess) {
            return;
        }

        Logger.LogError("批量新增包裹失败，原因={ErrorMessage}", result.ErrorMessage);
        throw new InvalidOperationException(result.ErrorMessage ?? "批量新增包裹失败");
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
        Func<IQueryable<Parcel>, IQueryable<Parcel>> queryBuilder,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        var pageNumber = pageRequest.NormalizePageNumber();
        var pageSize = pageRequest.NormalizePageSize();

        try {
            await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = queryBuilder(Query(db));

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
            Logger.LogError(ex, "分页查询包裹失败，PageNumber={PageNumber}, PageSize={PageSize}", pageNumber, pageSize);
            throw;
        }
    }

    /// <summary>
    /// 应用过滤条件。
    /// </summary>
    private static IQueryable<Parcel> ApplyFilter(IQueryable<Parcel> query, ParcelQueryFilter filter) {
        if (!string.IsNullOrWhiteSpace(filter.BarCodeKeyword)) {
            var barCodeKeyword = filter.BarCodeKeyword.Trim();
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

        if (filter.Status.HasValue) {
            query = query.Where(x => x.Status == filter.Status.Value);
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
    /// </summary>
    private static int NormalizeAdjacentCount(int count) {
        if (count <= 0) {
            return 0;
        }

        return count > MaxAdjacentCountPerSide ? MaxAdjacentCountPerSide : count;
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
