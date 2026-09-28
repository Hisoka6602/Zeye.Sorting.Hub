namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// Parcel 合同映射参数。
/// </summary>
internal readonly record struct ParcelResponseArgs {
    /// <summary>来源服务或设备实例标识。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源编号会话，设备编号重置后更换。</summary>
    public string? SourceRunId { get; init; }
    /// <summary>来源设备包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>首次分拣机检测本地时间。</summary>
    public DateTime? DetectedTime { get; init; }
    /// <summary>最新有效DWS测量本地时间。</summary>
    public DateTime? MeasurementTime { get; init; }
    /// <summary>目标格口原始编码。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口原始编码。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>外部路由任务编码。</summary>
    public string? TaskCode { get; init; }
    /// <summary>体积重量，单位克。</summary>
    public decimal? VolumetricWeightGrams { get; init; }
    /// <summary>是否使用兜底格口，未知时为空。</summary>
    public bool? IsFallbackChuteAssigned { get; init; }
    /// <summary>是否阻断分拣路由，未知时为空。</summary>
    public bool? IsRoutingBlocked { get; init; }
    /// <summary>设备异常原始编码。</summary>
    public string? SourceExceptionCode { get; init; }

    /// <summary>
    /// 包裹 Id。
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public required DateTime CreatedTime { get; init; }

    /// <summary>
    /// 修改时间。
    /// </summary>
    public required DateTime ModifyTime { get; init; }

    /// <summary>
    /// 修改 IP。
    /// </summary>
    public required string ModifyIp { get; init; }

    /// <summary>
    /// 包裹时间戳。
    /// </summary>
    public required long ParcelTimestamp { get; init; }

    /// <summary>
    /// 包裹类型。
    /// </summary>
    public required int Type { get; init; }

    /// <summary>
    /// 包裹状态。
    /// </summary>
    public required int Status { get; init; }

    /// <summary>
    /// 包裹异常类型。
    /// </summary>
    public required int? ExceptionType { get; init; }

    /// <summary>
    /// NoRead 类型。
    /// </summary>
    public required int NoReadType { get; init; }

    /// <summary>
    /// 小车编号。
    /// </summary>
    public required long? SorterCarrierId { get; init; }

    /// <summary>
    /// 三段码。
    /// </summary>
    public required string? SegmentCodes { get; init; }

    /// <summary>
    /// 生命周期（毫秒）。
    /// </summary>
    public required long? LifecycleMilliseconds { get; init; }

    /// <summary>
    /// 目标格口 Id。
    /// </summary>
    public required long? TargetChuteId { get; init; }

    /// <summary>
    /// 实际格口 Id。
    /// </summary>
    public required long? ActualChuteId { get; init; }

    /// <summary>
    /// 包裹主条码。
    /// </summary>
    public required string BarCodes { get; init; }

    /// <summary>
    /// 重量。
    /// </summary>
    public required decimal? Weight { get; init; }

    /// <summary>
    /// 外部接口访问状态。
    /// </summary>
    public required int RequestStatus { get; init; }

    /// <summary>
    /// 集包号。
    /// </summary>
    public required string BagCode { get; init; }

    /// <summary>
    /// 工作台名称。
    /// </summary>
    public required string WorkstationName { get; init; }

    /// <summary>
    /// 是否叠包。
    /// </summary>
    public required bool IsSticking { get; init; }

    /// <summary>
    /// 长度。
    /// </summary>
    public required decimal? Length { get; init; }

    /// <summary>
    /// 宽度。
    /// </summary>
    public required decimal? Width { get; init; }

    /// <summary>
    /// 高度。
    /// </summary>
    public required decimal? Height { get; init; }

    /// <summary>
    /// 体积。
    /// </summary>
    public required decimal? Volume { get; init; }

    /// <summary>
    /// 扫码时间。
    /// </summary>
    public required DateTime ScannedTime { get; init; }

    /// <summary>
    /// 落格时间。
    /// </summary>
    public required DateTime? DischargeTime { get; init; }

    /// <summary>
    /// 包裹完结时间。
    /// </summary>
    public required DateTime? CompletedTime { get; init; }

    /// <summary>
    /// 是否有图片。
    /// </summary>
    public required bool HasImages { get; init; }

    /// <summary>
    /// 是否有视频。
    /// </summary>
    public required bool HasVideos { get; init; }

    /// <summary>
    /// 包裹坐标。
    /// </summary>
    public required string Coordinate { get; init; }
}
