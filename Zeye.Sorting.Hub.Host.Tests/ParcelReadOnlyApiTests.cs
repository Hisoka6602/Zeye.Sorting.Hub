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
    /// 验证场景：邻近查询参数异常返回合理错误。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithInvalidBeforeCount_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?scannedTime=2026-03-20T10:00:00&beforeCount=-1&afterCount=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("前向查询条数不能小于 0", body, StringComparison.Ordinal);
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
/// Parcel 仓储测试替身（仅覆盖只读查询）。
/// </summary>
internal sealed class FakeParcelRepository : IParcelRepository {
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
    /// 详情查询返回固定包裹。
    /// </summary>
    public Task<Parcel?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        if (id != 1) {
            return Task.FromResult<Parcel?>(null);
        }

        var scannedTime = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        var parcel = Parcel.Create(
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
        parcel.Id = 1;
        return Task.FromResult<Parcel?>(parcel);
    }

    /// <summary>
    /// 邻近查询返回固定记录。
    /// </summary>
    public Task<IReadOnlyList<ParcelSummaryReadModel>> GetAdjacentByScannedTimeAsync(DateTime scannedTime, int beforeCount, int afterCount, CancellationToken cancellationToken) {
        var items = new List<ParcelSummaryReadModel> {
            CreateSummary(2, "BC-ADJ-1", "BAG-ADJ", scannedTime.AddSeconds(-2)),
            CreateSummary(3, "BC-ADJ-2", "BAG-ADJ", scannedTime.AddSeconds(2))
        };
        return Task.FromResult<IReadOnlyList<ParcelSummaryReadModel>>(items);
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
    /// 新增（测试未使用）。
    /// </summary>
    public Task<RepositoryResult> AddAsync(Parcel parcel, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 更新（测试未使用）。
    /// </summary>
    public Task<RepositoryResult> UpdateAsync(Parcel parcel, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 删除（测试未使用）。
    /// </summary>
    public Task<RepositoryResult> RemoveAsync(Parcel parcel, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 过期清理（测试未使用）。
    /// </summary>
    public Task<RepositoryResult<DangerousBatchActionResult>> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
    }

    /// <summary>
    /// 批量新增（测试未使用）。
    /// </summary>
    public Task<RepositoryResult> AddRangeAsync(IReadOnlyCollection<Parcel> parcels, CancellationToken cancellationToken) {
        throw new NotSupportedException("测试替身未实现该方法。");
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
