using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 仓储测试替身（支持读写操作，RemoveExpiredAsync 行为可通过属性控制）。
/// </summary>
internal sealed class FakeParcelRepository : IParcelRepository {
    /// <summary>
    /// 邻近查询稳定排序测试锚点 Id。
    /// </summary>
    private const long StableOrderAnchorId = 11;

    /// <summary>
    /// 详情查询预置样本 Id（未写入内存存储，仅用于只读详情与更新路径兼容）。
    /// </summary>
    private const long DetailQuerySampleId = 1;

    /// <summary>
    /// 收集测试过程中的包裹实体写入状态，用于 Add/Update/Remove 路径断言。
    /// </summary>
    private readonly Dictionary<long, Parcel> _store = [];

    /// <summary>
    /// 控制 RemoveExpiredAsync 返回 blocked/dry-run/execute 三态决策，用于清理治理接口断言。
    /// </summary>
    public ActionIsolationDecision CleanupDecision { get; set; } = ActionIsolationDecision.Execute;

    /// <summary>
    /// 控制 AddAsync 是否模拟写入失败，用于验证上层失败分支处理。
    /// </summary>
    public bool ShouldFailOnAdd { get; set; }

    /// <summary>
    /// 指定清理计划数量，用于断言返回结果中的 PlannedCount。
    /// </summary>
    public int CleanupPlannedCount { get; set; } = 5;

    /// <summary>
    /// 指定 execute 决策下的执行数量，用于断言返回结果中的 ExecutedCount。
    /// </summary>
    public int CleanupExecutedCount { get; set; } = 5;

    /// <summary>
    /// 最近一次普通分页查询过滤参数。
    /// </summary>
    public ParcelQueryFilter? LastPagedFilter { get; private set; }

    /// <summary>
    /// 最近一次游标分页查询过滤参数。
    /// </summary>
    public ParcelQueryFilter? LastCursorFilter { get; private set; }

    /// <summary>
    /// 最近一次游标分页请求参数。
    /// </summary>
    public CursorPageRequest? LastCursorPageRequest { get; private set; }

    /// <summary>
    /// 列表查询返回固定分页结果。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetPagedAsync(ParcelQueryFilter filter, PageRequest pageRequest, CancellationToken cancellationToken) {
        LastPagedFilter = filter;
        var scannedTime = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        var page = new PageResult<ParcelSummaryReadModel> {
            Items = [CreateSummary(1, "BC-LIST-1", "BAG-LIST", scannedTime)],
            PageNumber = pageRequest.PageNumber,
            PageSize = pageRequest.PageSize,
            TotalCount = 1
        };
        return Task.FromResult(page);
    }

    /// <summary>
    /// 游标分页返回固定两页结果，并记录最近一次游标查询参数。
    /// </summary>
    public Task<CursorPageResult<ParcelSummaryReadModel>> GetCursorPagedAsync(ParcelQueryFilter filter, CursorPageRequest pageRequest, CancellationToken cancellationToken) {
        LastCursorFilter = filter;
        LastCursorPageRequest = pageRequest;

        var baseTime = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        var firstPageItems = new[] {
            CreateSummary(4, "BC-CURSOR-4", "BAG-CURSOR", baseTime.AddMinutes(4)),
            CreateSummary(3, "BC-CURSOR-3", "BAG-CURSOR", baseTime.AddMinutes(3))
        };
        var secondPageItems = new[] {
            CreateSummary(2, "BC-CURSOR-2", "BAG-CURSOR", baseTime.AddMinutes(2)),
            CreateSummary(1, "BC-CURSOR-1", "BAG-CURSOR", baseTime.AddMinutes(1))
        };

        var result = pageRequest.LastId switch {
            3 => new CursorPageResult<ParcelSummaryReadModel> {
                Items = secondPageItems,
                PageSize = pageRequest.NormalizePageSize(),
                HasMore = false,
                NextScannedTimeLocal = null,
                NextId = null
            },
            _ => new CursorPageResult<ParcelSummaryReadModel> {
                Items = firstPageItems,
                PageSize = pageRequest.NormalizePageSize(),
                HasMore = true,
                NextScannedTimeLocal = firstPageItems[^1].ScannedTime,
                NextId = firstPageItems[^1].Id
            }
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// 详情查询优先从内存存储返回包裹，未命中时对 id==1 返回固定样本包裹。
    /// </summary>
    public Task<Parcel?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        if (_store.TryGetValue(id, out var storedParcel)) {
            return Task.FromResult<Parcel?>(storedParcel);
        }

        if (id != DetailQuerySampleId) {
            return Task.FromResult<Parcel?>(null);
        }

        var scannedTime = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        var parcel = Parcel.Create(
            id: DetailQuerySampleId,
            parcelTimestamp: scannedTime.Ticks,
            type: ParcelType.Normal,
            barCodes: "BC-DETAIL-1",
            weight: 1.2m,
            workstationName: "WS-DETAIL",
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddSeconds(3),
            targetChuteId: 801,
            actualChuteId: 802,
            requestStatus: ApiRequestStatus.Success,
            bagCode: "BAG-DETAIL",
            isSticking: false,
            length: 10,
            width: 20,
            height: 30,
            volume: 6000,
            hasImages: true,
            hasVideos: false,
            coordinate: "x:1,y:2");
        return Task.FromResult<Parcel?>(parcel);
    }

    /// <summary>
    /// 邻近查询按 id 返回固定记录。
    /// </summary>
    public Task<RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>> GetAdjacentByIdAsync(long id, int beforeCount, int afterCount, CancellationToken cancellationToken) {
        var baseTime = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        if (id == 999) {
            return Task.FromResult(RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>.Fail("未找到 Id 为 999 的资源。"));
        }

        if (id == StableOrderAnchorId) {
            IReadOnlyList<ParcelSummaryReadModel> stableItems = [
                CreateSummary(10, "BC-SAME-10", "BAG-ADJ", baseTime),
                CreateSummary(12, "BC-SAME-12", "BAG-ADJ", baseTime),
                CreateSummary(13, "BC-SAME-13", "BAG-ADJ", baseTime.AddMinutes(1))
            ];
            return Task.FromResult(RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>.Success(stableItems));
        }

        IReadOnlyList<ParcelSummaryReadModel> items = [
            CreateSummary(1, "BC-ADJ-1", "BAG-ADJ", baseTime.AddSeconds(-2)),
            CreateSummary(3, "BC-ADJ-2", "BAG-ADJ", baseTime.AddSeconds(2))
        ];
        return Task.FromResult(RepositoryResult<IReadOnlyList<ParcelSummaryReadModel>>.Success(items));
    }

    /// <summary>
    /// 按集包号查询（测试未使用）。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByBagCodeAsync(string bagCode, DateTime scannedTimeStart, DateTime scannedTimeEnd, PageRequest pageRequest, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 按工作台查询（测试未使用）。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByWorkstationNameAsync(string workstationName, DateTime scannedTimeStart, DateTime scannedTimeEnd, PageRequest pageRequest, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 按状态查询（测试未使用）。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByStatusAsync(ParcelStatus status, DateTime scannedTimeStart, DateTime scannedTimeEnd, PageRequest pageRequest, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 按格口查询（测试未使用）。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetByChuteAsync(long? actualChuteId, long? targetChuteId, DateTime scannedTimeStart, DateTime scannedTimeEnd, PageRequest pageRequest, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 新增包裹（使用外部传入 Id；若重复则返回冲突错误；若 ShouldFailOnAdd=true 则返回失败结果）。
    /// </summary>
    public Task<RepositoryResult> AddAsync(Parcel parcel, CancellationToken cancellationToken) {
        if (ShouldFailOnAdd) {
            return Task.FromResult(RepositoryResult.Fail("模拟仓储写入失败：数据库不可用。"));
        }

        if (_store.ContainsKey(parcel.Id)) {
            return Task.FromResult(RepositoryResult.Fail("包裹 Id 已存在。", RepositoryErrorCodes.ParcelIdConflict));
        }

        _store[parcel.Id] = parcel;
        return Task.FromResult(RepositoryResult.Success());
    }

    /// <summary>
    /// 更新包裹（覆盖内存存储中对应记录）。
    /// </summary>
    public Task<RepositoryResult> UpdateAsync(Parcel parcel, CancellationToken cancellationToken) {
        if (!_store.ContainsKey(parcel.Id) && parcel.Id != DetailQuerySampleId) {
            return Task.FromResult(RepositoryResult.Fail("目标包裹不存在。"));
        }

        _store[parcel.Id] = parcel;
        return Task.FromResult(RepositoryResult.Success());
    }

    /// <summary>
    /// 删除包裹（移除内存存储中对应记录）。
    /// </summary>
    public Task<RepositoryResult> RemoveAsync(Parcel parcel, CancellationToken cancellationToken) {
        _store.Remove(parcel.Id);
        return Task.FromResult(RepositoryResult.Success());
    }

    /// <summary>
    /// 过期清理（由 CleanupDecision 属性控制返回三态决策，不依赖真实隔离器）。
    /// </summary>
    public Task<RepositoryResult<DangerousBatchActionResult>> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken) {
        var executedCount = CleanupDecision == ActionIsolationDecision.Execute ? CleanupExecutedCount : 0;
        var result = new DangerousBatchActionResult {
            ActionName = "remove-expired-parcels",
            Decision = CleanupDecision,
            PlannedCount = CleanupPlannedCount,
            ExecutedCount = executedCount,
            IsDryRun = CleanupDecision == ActionIsolationDecision.DryRunOnly,
            IsBlockedByGuard = CleanupDecision == ActionIsolationDecision.BlockedByGuard,
            CompensationBoundary = "此操作不可逆，回滚需从备份恢复。"
        };
        return Task.FromResult(RepositoryResult<DangerousBatchActionResult>.Success(result));
    }

    /// <summary>
    /// 批量新增（使用外部传入 Id，重复时返回冲突错误）。
    /// </summary>
    public Task<RepositoryResult> AddRangeAsync(IReadOnlyCollection<Parcel> parcels, CancellationToken cancellationToken) {
        foreach (var parcel in parcels) {
            if (_store.ContainsKey(parcel.Id)) {
                return Task.FromResult(RepositoryResult.Fail("包裹 Id 已存在。", RepositoryErrorCodes.ParcelIdConflict));
            }

            _store[parcel.Id] = parcel;
        }

        return Task.FromResult(RepositoryResult.Success());
    }

    /// <summary>
    /// 创建摘要读模型。
    /// </summary>
    /// <param name="id">包裹 Id。</param>
    /// <param name="barCodes">条码。</param>
    /// <param name="bagCode">集包号。</param>
    /// <param name="scannedTime">扫码时间。</param>
    /// <returns>摘要读模型。</returns>
    private static ParcelSummaryReadModel CreateSummary(long id, string barCodes, string bagCode, DateTime scannedTime) {
        return new ParcelSummaryReadModel {
            Id = id,
            CreatedTime = scannedTime,
            ModifyTime = scannedTime,
            ModifyIp = "127.0.0.1",
            ParcelTimestamp = scannedTime.Ticks,
            Type = ParcelType.Normal,
            Status = ParcelStatus.Pending,
            ExceptionType = null,
            NoReadType = NoReadType.None,
            SorterCarrierId = 1,
            SegmentCodes = null,
            LifecycleMilliseconds = 1000,
            TargetChuteId = 101,
            ActualChuteId = 102,
            BarCodes = barCodes,
            Weight = 1.3m,
            RequestStatus = ApiRequestStatus.Success,
            BagCode = bagCode,
            WorkstationName = "WS-TEST",
            IsSticking = false,
            Length = 11,
            Width = 22,
            Height = 33,
            Volume = 7986,
            ScannedTime = scannedTime,
            DischargeTime = scannedTime.AddSeconds(2),
            CompletedTime = null,
            HasImages = true,
            HasVideos = false,
            Coordinate = "x:1,y:1"
        };
    }
}
