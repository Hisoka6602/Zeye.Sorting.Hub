using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Host;
using ParcelListResponse = Zeye.Sorting.Hub.Contracts.Models.Parcels.ParcelListResponse;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 只读 API 端点回归测试。
/// </summary>
public sealed class ParcelReadOnlyApiTests {
    /// <summary>
    /// 邻近查询稳定排序测试锚点 Id。
    /// </summary>
    private const long StableOrderAnchorId = 11;

    /// <summary>
    /// 验证场景：正常获取列表。
    /// </summary>
    [Fact]
    public async Task GetParcels_ShouldReturnPagedList() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels?pageNumber=1&pageSize=10&bagCode=BAG-LIST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ParcelListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.PageNumber);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(1, payload.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal("BAG-LIST", payload.Items[0].BagCode);
    }

    /// <summary>
    /// 验证场景：正常获取详情。
    /// </summary>
    [Fact]
    public async Task GetParcelById_ShouldReturnDetail() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("BC-DETAIL-1", payload.RootElement.GetProperty("barCodes").GetString());
    }

    /// <summary>
    /// 验证场景：详情不存在返回 404。
    /// </summary>
    [Fact]
    public async Task GetParcelById_WhenNotFound_ShouldReturnNotFound() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：邻近查询 id 缺失返回 400。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithMissingId_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?beforeCount=1&afterCount=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id 为必填参数", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：列表查询拒绝 UTC/offset 时间参数。
    /// </summary>
    [Fact]
    public async Task GetParcels_WithUtcOrOffsetTime_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var utcResponse = await client.GetAsync("/api/parcels?scannedTimeStart=2026-03-20T10:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, utcResponse.StatusCode);

        var offsetResponse = await client.GetAsync("/api/parcels?scannedTimeEnd=2026-03-20T10:00:00+08:00");
        Assert.Equal(HttpStatusCode.BadRequest, offsetResponse.StatusCode);
    }

    /// <summary>
    /// 验证场景：邻近查询 id 非法返回 400。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithInvalidId_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?id=0&beforeCount=1&afterCount=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("包裹 Id 必须大于 0", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：邻近查询锚点不存在返回 404。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WhenAnchorNotFound_ShouldReturnNotFound() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?id=999&beforeCount=2&afterCount=2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：邻近查询按 id 正常返回前后记录。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithValidId_ShouldReturnAdjacentItems() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/parcels/adjacent?id=2&beforeCount=1&afterCount=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.RootElement.GetProperty("beforeCount").GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("afterCount").GetInt32());
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal(3, items[1].GetProperty("id").GetInt64());
    }

    /// <summary>
    /// 验证场景：同一 ScannedTime 下按 Id 保持稳定排序。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithSameScannedTime_ShouldKeepStableOrder() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync($"/api/parcels/adjacent?id={StableOrderAnchorId}&beforeCount=1&afterCount=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        var ids = payload.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToArray();
        Assert.Equal([10L, 12L, 13L], ids);
    }

    /// <summary>
    /// 构建测试用 WebApplication。
    /// </summary>
    /// <returns>已启动的测试应用。</returns>
    private static async Task<WebApplication> BuildTestAppAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddScoped<IParcelRepository, FakeParcelRepository>();
        builder.Services.AddScoped<GetParcelPagedQueryService>();
        builder.Services.AddScoped<GetParcelByIdQueryService>();
        builder.Services.AddScoped<GetAdjacentParcelsQueryService>();

        var app = builder.Build();
        app.MapParcelReadOnlyApis();
        await app.StartAsync();
        return app;
    }
}

/// <summary>
/// Parcel 仓储测试替身（支持读写操作，RemoveExpiredAsync 行为可通过属性控制）。
/// </summary>
internal sealed class FakeParcelRepository : IParcelRepository {
    /// <summary>
    /// 邻近查询稳定排序测试锚点 Id。
    /// </summary>
    private const long StableOrderAnchorId = 11;

    /// <summary>
    /// 详情查询预置样本 Id（未写入 _store，仅用于只读详情与更新路径兼容）。
    /// </summary>
    private const long DetailQuerySampleId = 1;
    /// <summary>
    /// 内存存储（用于写操作测试）。
    /// </summary>
    private readonly Dictionary<long, Parcel> _store = new();

    /// <summary>
    /// 清理治理接口决策（控制 RemoveExpiredAsync 返回的三态决策，默认 Execute）。
    /// </summary>
    public ActionIsolationDecision CleanupDecision { get; set; } = ActionIsolationDecision.Execute;

    /// <summary>
    /// 当设置为 true 时，AddAsync 返回失败结果以模拟仓储写入异常场景。
    /// </summary>
    public bool ShouldFailOnAdd { get; set; } = false;

    /// <summary>
    /// 清理计划量（用于 RemoveExpiredAsync 测试断言）。
    /// </summary>
    public int CleanupPlannedCount { get; set; } = 5;

    /// <summary>
    /// 清理执行量（Execute 决策时生效，blocked/dry-run 返回 0）。
    /// </summary>
    public int CleanupExecutedCount { get; set; } = 5;

    /// <summary>
    /// 列表查询返回固定分页结果。
    /// </summary>
    public Task<PageResult<ParcelSummaryReadModel>> GetPagedAsync(ParcelQueryFilter filter, PageRequest pageRequest, CancellationToken cancellationToken) {
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
    /// 详情查询优先从内存存储返回包裹，未命中时对 id==1 返回固定样本包裹。
    /// </summary>
    public Task<Parcel?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        // 先从内存存储中查找，支持 Add/Update/Remove 操作写入后的读取、更新、删除验证
        if (_store.TryGetValue(id, out var storedParcel)) {
            return Task.FromResult<Parcel?>(storedParcel);
        }

        // 若内存存储未命中，仅对 id==1 返回固定样本包裹，其余返回 null
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
            return Task.FromResult(RepositoryResult.Fail("包裹 Id 已存在。"));
        }

        _store[parcel.Id] = parcel;
        return Task.FromResult(RepositoryResult.Success());
    }

    /// <summary>
    /// 更新包裹（覆盖内存存储中对应记录）。
    /// </summary>
    public Task<RepositoryResult> UpdateAsync(Parcel parcel, CancellationToken cancellationToken) {
        // 说明：DetailQuerySampleId 为详情查询预置样本（未放入 _store），允许更新以覆盖管理端更新接口测试路径。
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
                return Task.FromResult(RepositoryResult.Fail("包裹 Id 已存在。"));
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
