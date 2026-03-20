using NLog;
using Zeye.Sorting.Hub.Contracts.Enums.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 管理端更新包裹状态应用服务（仅支持领域允许的状态转换操作）。
/// </summary>
public sealed class UpdateParcelStatusCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化管理端更新包裹状态应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public UpdateParcelStatusCommandService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行包裹状态更新。
    /// </summary>
    /// <param name="parcelId">目标包裹 Id（必须大于 0）。</param>
    /// <param name="request">更新请求合同。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新后的包裹详情；包裹不存在时返回 null。</returns>
    public async Task<ParcelDetailResponse?> ExecuteAsync(long parcelId, ParcelUpdateRequest request, CancellationToken cancellationToken) {
        if (parcelId <= 0) {
            Logger.Warn("更新包裹状态参数非法，ParcelId={ParcelId}", parcelId);
            throw new ArgumentOutOfRangeException(nameof(parcelId), "包裹 Id 必须大于 0。");
        }

        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        // 步骤 1：解析操作类型枚举，拒绝无效操作码。
        if (!Enum.IsDefined(typeof(ParcelUpdateOperation), request.Operation)) {
            Logger.Warn("更新包裹状态参数非法，Operation={Operation}", request.Operation);
            throw new ArgumentOutOfRangeException(nameof(request.Operation), "操作类型无效，请参阅 ParcelUpdateOperation 枚举定义。");
        }

        var operation = (ParcelUpdateOperation)request.Operation;

        // 步骤 2：按操作类型验证必须提供的字段。
        ValidateOperationFields(operation, request);

        try {
            // 步骤 3：加载目标包裹聚合根；不存在则返回 null，由 Host 层返回 404。
            var parcel = await _parcelRepository.GetByIdAsync(parcelId, cancellationToken);
            if (parcel is null) {
                return null;
            }

            // 步骤 4：按操作类型调用领域方法完成状态转换，保持领域不变量。
            switch (operation) {
                case ParcelUpdateOperation.MarkCompleted:
                    parcel.MarkCompleted(request.CompletedTime!.Value);
                    break;

                case ParcelUpdateOperation.MarkSortingException:
                    if (!Enum.IsDefined(typeof(ParcelExceptionType), request.ExceptionType!.Value)) {
                        Logger.Warn("更新包裹状态参数非法，ExceptionType={ExceptionType}", request.ExceptionType);
                        throw new ArgumentOutOfRangeException(nameof(request.ExceptionType), "异常类型无效。");
                    }

                    parcel.MarkSortingException((ParcelExceptionType)request.ExceptionType.Value);
                    break;

                case ParcelUpdateOperation.UpdateRequestStatus:
                    if (!Enum.IsDefined(typeof(ApiRequestStatus), request.RequestStatus!.Value)) {
                        Logger.Warn("更新包裹状态参数非法，RequestStatus={RequestStatus}", request.RequestStatus);
                        throw new ArgumentOutOfRangeException(nameof(request.RequestStatus), "接口访问状态无效。");
                    }

                    parcel.UpdateRequestStatus((ApiRequestStatus)request.RequestStatus.Value);
                    break;
            }

            // 步骤 5：持久化领域状态变更。
            var result = await _parcelRepository.UpdateAsync(parcel, cancellationToken);
            if (!result.IsSuccess) {
                Logger.Error(
                    "更新包裹状态失败，ParcelId={ParcelId}, Operation={Operation}, ErrorMessage={ErrorMessage}",
                    parcelId,
                    operation,
                    result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage ?? "更新包裹状态失败。");
            }

            // 步骤 6：返回更新后的包裹详情合同。
            return ParcelContractMapper.ToDetail(parcel);
        }
        catch (ArgumentException) {
            throw;
        }
        catch (InvalidOperationException) {
            throw;
        }
        catch (Exception ex) {
            Logger.Error(ex, "更新包裹状态发生意外异常，ParcelId={ParcelId}, Operation={Operation}", parcelId, operation);
            throw;
        }
    }

    /// <summary>
    /// 校验各操作类型的必须字段。
    /// </summary>
    /// <param name="operation">目标操作类型。</param>
    /// <param name="request">更新请求合同。</param>
    private static void ValidateOperationFields(ParcelUpdateOperation operation, ParcelUpdateRequest request) {
        switch (operation) {
            case ParcelUpdateOperation.MarkCompleted:
                if (request.CompletedTime is null) {
                    throw new ArgumentException("MarkCompleted 操作必须提供 completedTime 字段。", nameof(request.CompletedTime));
                }

                break;

            case ParcelUpdateOperation.MarkSortingException:
                if (request.ExceptionType is null) {
                    throw new ArgumentException("MarkSortingException 操作必须提供 exceptionType 字段。", nameof(request.ExceptionType));
                }

                break;

            case ParcelUpdateOperation.UpdateRequestStatus:
                if (request.RequestStatus is null) {
                    throw new ArgumentException("UpdateRequestStatus 操作必须提供 requestStatus 字段。", nameof(request.RequestStatus));
                }

                break;
        }
    }
}
