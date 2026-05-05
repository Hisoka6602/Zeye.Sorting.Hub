using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// Parcel 查询请求映射器。
/// </summary>
internal static class ParcelQueryRequestMapper {
    /// <summary>
    /// 默认时间窗口小时数。
    /// </summary>
    private const int DefaultTimeRangeHours = 24;

    /// <summary>
    /// 将普通分页查询请求映射为仓储过滤模型。
    /// </summary>
    /// <param name="request">普通分页请求。</param>
    /// <returns>仓储过滤模型。</returns>
    public static ParcelQueryFilter BuildFilter(ParcelListRequest request) {
        var (scannedTimeStart, scannedTimeEnd) = NormalizeTimeRange(request.ScannedTimeStart, request.ScannedTimeEnd);
        return new ParcelQueryFilter {
            BarCodeKeyword = request.BarCodeKeyword,
            BagCode = request.BagCode,
            WorkstationName = request.WorkstationName,
            Status = request.Status.HasValue ? (ParcelStatus?)request.Status.Value : null,
            ExceptionType = request.ExceptionType.HasValue ? (ParcelExceptionType?)request.ExceptionType.Value : null,
            ActualChuteId = request.ActualChuteId,
            TargetChuteId = request.TargetChuteId,
            ScannedTimeStart = scannedTimeStart,
            ScannedTimeEnd = scannedTimeEnd
        };
    }

    /// <summary>
    /// 将游标查询请求映射为仓储过滤模型。
    /// </summary>
    /// <param name="request">游标查询请求。</param>
    /// <returns>仓储过滤模型。</returns>
    public static ParcelQueryFilter BuildFilter(ParcelCursorListRequest request) {
        var (scannedTimeStart, scannedTimeEnd) = NormalizeTimeRange(request.ScannedTimeStart, request.ScannedTimeEnd);
        return new ParcelQueryFilter {
            BarCodeKeyword = request.BarCodeKeyword,
            BagCode = request.BagCode,
            WorkstationName = request.WorkstationName,
            Status = request.Status.HasValue ? (ParcelStatus?)request.Status.Value : null,
            ExceptionType = request.ExceptionType.HasValue ? (ParcelExceptionType?)request.ExceptionType.Value : null,
            ActualChuteId = request.ActualChuteId,
            TargetChuteId = request.TargetChuteId,
            ScannedTimeStart = scannedTimeStart,
            ScannedTimeEnd = scannedTimeEnd
        };
    }

    /// <summary>
    /// 归一化查询时间范围；当请求未指定时间范围时，默认限制为最近 24 小时。
    /// </summary>
    /// <param name="scannedTimeStart">原始开始时间。</param>
    /// <param name="scannedTimeEnd">原始结束时间。</param>
    /// <returns>归一化后的时间范围。</returns>
    private static (DateTime? ScannedTimeStart, DateTime? ScannedTimeEnd) NormalizeTimeRange(DateTime? scannedTimeStart, DateTime? scannedTimeEnd) {
        if (scannedTimeStart.HasValue || scannedTimeEnd.HasValue) {
            return (scannedTimeStart, scannedTimeEnd);
        }

        var scannedTimeEndLocal = DateTime.Now;
        var scannedTimeStartLocal = scannedTimeEndLocal.AddHours(-DefaultTimeRangeHours);
        return (scannedTimeStartLocal, scannedTimeEndLocal);
    }
}
