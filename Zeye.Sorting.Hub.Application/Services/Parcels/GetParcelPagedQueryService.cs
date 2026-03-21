using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 分页查询 Parcel 列表应用服务。
/// </summary>
public sealed class GetParcelPagedQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化分页查询 Parcel 列表应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public GetParcelPagedQueryService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行分页查询。
    /// </summary>
    /// <param name="request">列表查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页列表响应。</returns>
    public async Task<ParcelListResponse> ExecuteAsync(ParcelListRequest request, CancellationToken cancellationToken) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        ValidateRequest(request);

        try {
            // 步骤 1：将合同层请求映射为仓储过滤模型，避免 Host 直接感知 Domain 查询参数。
            var filter = BuildFilter(request);
            var pageRequest = new PageRequest {
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
            // 步骤 2：调用仓储获取分页读模型。
            var pageResult = await _parcelRepository.GetPagedAsync(filter, pageRequest, cancellationToken);
            // 步骤 3：统一在应用层完成 DTO 映射并返回 Contracts 响应。
            var items = pageResult.Items
                .Select(ParcelContractMapper.ToListItem)
                .ToArray();
            return new ParcelListResponse {
                Items = items,
                PageNumber = pageResult.PageNumber,
                PageSize = pageResult.PageSize,
                TotalCount = pageResult.TotalCount
            };
        }
        catch (Exception ex) {
            Logger.Error(
                ex,
                "分页查询 Parcel 列表失败，PageNumber={0}, PageSize={1}, BarCodeKeyword={2}, BagCode={3}, WorkstationName={4}, Status={5}, ActualChuteId={6}, TargetChuteId={7}, ScannedTimeStart={8}, ScannedTimeEnd={9}",
                request.PageNumber,
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
    /// 校验列表查询请求参数。
    /// </summary>
    /// <param name="request">列表查询请求。</param>
    private static void ValidateRequest(ParcelListRequest request) {
        if (request.PageNumber <= 0) {
            Logger.Warn("分页查询 Parcel 列表参数非法，PageNumber={0}", request.PageNumber);
            throw new ArgumentOutOfRangeException(nameof(request.PageNumber), "页码必须大于 0。");
        }

        if (request.PageSize <= 0) {
            Logger.Warn("分页查询 Parcel 列表参数非法，PageSize={0}", request.PageSize);
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), "页大小必须大于 0。");
        }

        if (request.ScannedTimeStart.HasValue && request.ScannedTimeEnd.HasValue && request.ScannedTimeEnd.Value < request.ScannedTimeStart.Value) {
            Logger.Warn(
                "分页查询 Parcel 列表参数非法，ScannedTimeStart={0}, ScannedTimeEnd={1}",
                request.ScannedTimeStart,
                request.ScannedTimeEnd);
            throw new ArgumentException("扫码结束时间不能早于开始时间。", nameof(request));
        }

        if (request.Status.HasValue && !Enum.IsDefined(typeof(ParcelStatus), request.Status.Value)) {
            Logger.Warn("分页查询 Parcel 列表参数非法，Status={0}", request.Status);
            throw new ArgumentOutOfRangeException(nameof(request.Status), "包裹状态无效。");
        }

        if (request.ExceptionType.HasValue && !Enum.IsDefined(typeof(ParcelExceptionType), request.ExceptionType.Value)) {
            Logger.Warn("分页查询 Parcel 列表参数非法，ExceptionType={0}", request.ExceptionType);
            throw new ArgumentOutOfRangeException(nameof(request.ExceptionType), "包裹异常类型无效。");
        }
    }

    /// <summary>
    /// 将合同请求映射为仓储过滤模型。
    /// </summary>
    /// <param name="request">列表查询请求。</param>
    /// <returns>仓储过滤模型。</returns>
    private static ParcelQueryFilter BuildFilter(ParcelListRequest request) {
        return new ParcelQueryFilter {
            BarCodeKeyword = request.BarCodeKeyword,
            BagCode = request.BagCode,
            WorkstationName = request.WorkstationName,
            Status = request.Status.HasValue ? (ParcelStatus?)request.Status.Value : null,
            ExceptionType = request.ExceptionType.HasValue ? (ParcelExceptionType?)request.ExceptionType.Value : null,
            ActualChuteId = request.ActualChuteId,
            TargetChuteId = request.TargetChuteId,
            ScannedTimeStart = request.ScannedTimeStart,
            ScannedTimeEnd = request.ScannedTimeEnd
        };
    }
}
