using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 游标分页查询 Parcel 列表应用服务。
/// </summary>
public sealed class GetParcelCursorPagedQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化游标分页查询 Parcel 列表应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public GetParcelCursorPagedQueryService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行游标分页查询。
    /// </summary>
    /// <param name="request">查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>游标分页响应。</returns>
    public async Task<ParcelCursorListResponse> ExecuteAsync(ParcelCursorListRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        if (!ParcelCursorToken.TryDecode(request.Cursor, out var cursorToken)) {
            Logger.Warn("游标分页查询参数非法，Cursor={Cursor}", request.Cursor);
            throw new ArgumentException("cursor 无效。", nameof(request));
        }

        try {
            // 步骤 1：统一收口过滤条件与默认时间范围，避免 Host 和仓储重复处理业务约束。
            var filter = ParcelQueryRequestMapper.BuildFilter(request);
            var cursorPageRequest = new CursorPageRequest {
                PageSize = request.PageSize,
                LastScannedTimeLocal = cursorToken?.LastScannedTimeLocal,
                LastId = cursorToken?.LastId
            };

            // 步骤 2：调用仓储执行稳定排序的游标分页查询。
            var pageResult = await _parcelRepository.GetCursorPagedAsync(filter, cursorPageRequest, cancellationToken);

            // 步骤 3：将读模型映射为 Contracts 响应，并在应用层统一编码下一页游标。
            var items = pageResult.Items
                .Select(ParcelContractMapper.ToListItem)
                .ToArray();
            var nextCursor = pageResult.HasMore && pageResult.NextScannedTimeLocal.HasValue && pageResult.NextId.HasValue
                ? new ParcelCursorToken {
                    LastScannedTimeLocal = pageResult.NextScannedTimeLocal.Value,
                    LastId = pageResult.NextId.Value
                }.Encode()
                : null;

            return new ParcelCursorListResponse {
                Items = items,
                PageSize = pageResult.PageSize,
                HasMore = pageResult.HasMore,
                NextCursor = nextCursor
            };
        }
        catch (Exception ex) {
            Logger.Error(
                ex,
                "游标分页查询 Parcel 列表失败，Cursor={Cursor}, PageSize={PageSize}, BarCodeKeyword={BarCodeKeyword}, BagCode={BagCode}, WorkstationName={WorkstationName}, Status={Status}, ActualChuteId={ActualChuteId}, TargetChuteId={TargetChuteId}, ScannedTimeStart={ScannedTimeStart}, ScannedTimeEnd={ScannedTimeEnd}",
                request.Cursor,
                request.PageSize,
                request.BarCodeKeyword,
                request.BagCode,
                request.WorkstationName,
                request.Status,
                request.ActualChuteId,
                request.TargetChuteId,
                request.ScannedTimeStart,
                request.ScannedTimeEnd);
            throw;
        }
    }

    /// <summary>
    /// 校验游标分页查询参数。
    /// </summary>
    /// <param name="request">查询请求。</param>
    private static void ValidateRequest(ParcelCursorListRequest request) {
        Guard.ThrowIfZeroOrNegative(request.PageSize, nameof(request.PageSize), "页大小必须大于 0。", "游标分页查询 Parcel 列表");

        if (request.ScannedTimeStart.HasValue && request.ScannedTimeEnd.HasValue && request.ScannedTimeEnd.Value < request.ScannedTimeStart.Value) {
            Logger.Warn(
                "游标分页查询 Parcel 列表参数非法，ScannedTimeStart={ScannedTimeStart}, ScannedTimeEnd={ScannedTimeEnd}",
                request.ScannedTimeStart,
                request.ScannedTimeEnd);
            throw new ArgumentException("扫码结束时间不能早于开始时间。", nameof(request));
        }

        EnumGuard.ThrowIfUndefined<ParcelStatus>(request.Status, nameof(request.Status), "包裹状态无效。", "游标分页查询 Parcel 列表");
        EnumGuard.ThrowIfUndefined<ParcelExceptionType>(request.ExceptionType, nameof(request.ExceptionType), "包裹异常类型无效。", "游标分页查询 Parcel 列表");
    }
}
