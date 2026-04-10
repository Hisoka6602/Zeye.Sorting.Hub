using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

/// <summary>
/// Parcel 摘要视图接口。
/// 抽象 Parcel 聚合根与 ParcelSummaryReadModel 的公共可读属性，
/// 供 Application 层映射器统一消费，消除影分身映射代码。
/// </summary>
public interface IParcelSummaryView {
    /// <summary>包裹数据库主键 Id。</summary>
    long Id { get; }

    /// <summary>创建时间。</summary>
    DateTime CreatedTime { get; }

    /// <summary>修改时间。</summary>
    DateTime ModifyTime { get; }

    /// <summary>修改 IP。</summary>
    string ModifyIp { get; }

    /// <summary>包裹时间戳。</summary>
    long ParcelTimestamp { get; }

    /// <summary>包裹类型。</summary>
    ParcelType Type { get; }

    /// <summary>包裹状态。</summary>
    ParcelStatus Status { get; }

    /// <summary>包裹异常类型（可为空）。</summary>
    ParcelExceptionType? ExceptionType { get; }

    /// <summary>NoRead 类型。</summary>
    NoReadType NoReadType { get; }

    /// <summary>小车编号（可为空）。</summary>
    int? SorterCarrierId { get; }

    /// <summary>三段码（可为空）。</summary>
    string? SegmentCodes { get; }

    /// <summary>生命周期（毫秒，可为空）。</summary>
    long? LifecycleMilliseconds { get; }

    /// <summary>目标格口 Id。</summary>
    long TargetChuteId { get; }

    /// <summary>实际格口 Id。</summary>
    long ActualChuteId { get; }

    /// <summary>包裹主条码。</summary>
    string BarCodes { get; }

    /// <summary>重量。</summary>
    decimal Weight { get; }

    /// <summary>外部接口访问状态。</summary>
    ApiRequestStatus RequestStatus { get; }

    /// <summary>集包号。</summary>
    string BagCode { get; }

    /// <summary>工作台名称。</summary>
    string WorkstationName { get; }

    /// <summary>是否叠包。</summary>
    bool IsSticking { get; }

    /// <summary>长度。</summary>
    decimal Length { get; }

    /// <summary>宽度。</summary>
    decimal Width { get; }

    /// <summary>高度。</summary>
    decimal Height { get; }

    /// <summary>体积。</summary>
    decimal Volume { get; }

    /// <summary>扫码时间。</summary>
    DateTime ScannedTime { get; }

    /// <summary>落格时间。</summary>
    DateTime DischargeTime { get; }

    /// <summary>包裹完结时间（可为空）。</summary>
    DateTime? CompletedTime { get; }

    /// <summary>是否有图片。</summary>
    bool HasImages { get; }

    /// <summary>是否有视频。</summary>
    bool HasVideos { get; }

    /// <summary>包裹坐标。</summary>
    string Coordinate { get; }
}
