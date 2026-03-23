using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Repositories;
using DomainParcelExceptionType = Zeye.Sorting.Hub.Domain.Enums.ParcelExceptionType;

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
    /// <param name="completedTime">已解析的完结时间（本地时间，仅 MarkCompleted 操作时有效，由 Host 层完成字符串解析与 UTC 拒绝）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新后的包裹详情；包裹不存在时返回 null。</returns>
    public async Task<ParcelDetailResponse?> ExecuteAsync(long parcelId, ParcelUpdateRequest request, DateTime? completedTime, CancellationToken cancellationToken) {
        Guard.ThrowIfZeroOrNegative(parcelId, nameof(parcelId), "包裹 Id 必须大于 0。", "更新包裹状态");

        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        // 步骤 1：解析操作类型枚举，拒绝无效操作码。
        EnumGuard.ThrowIfUndefined<ParcelUpdateOperation>(request.Operation, nameof(request.Operation), "操作类型无效，请参阅 ParcelUpdateOperation 枚举定义。", "更新包裹状态");

        var operation = (ParcelUpdateOperation)request.Operation;

        // 步骤 2：按操作类型验证必须提供的字段。
        ValidateOperationFields(operation, request, completedTime);

        try {
            // 步骤 3：加载目标包裹聚合根；不存在则返回 null，由 Host 层返回 404。
            var parcel = await _parcelRepository.GetByIdAsync(parcelId, cancellationToken);
            if (parcel is null) {
                return null;
            }

            // 步骤 4：按操作类型调用领域方法完成状态转换，保持领域不变量。
            switch (operation) {
                case ParcelUpdateOperation.MarkCompleted:
                    parcel.MarkCompleted(completedTime!.Value);
                    break;

                case ParcelUpdateOperation.MarkSortingException:
                    EnumGuard.ThrowIfUndefined<DomainParcelExceptionType>(request.ExceptionType, nameof(request.ExceptionType), "异常类型无效。", "更新包裹状态");
                    parcel.MarkSortingException((DomainParcelExceptionType)request.ExceptionType!.Value);
                    break;

                case ParcelUpdateOperation.UpdateRequestStatus:
                    EnumGuard.ThrowIfUndefined<ApiRequestStatus>(request.RequestStatus, nameof(request.RequestStatus), "接口访问状态无效。", "更新包裹状态");
                    parcel.UpdateRequestStatus((ApiRequestStatus)request.RequestStatus!.Value);
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
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException) {
            Logger.Error(ex, "更新包裹状态发生意外异常，ParcelId={ParcelId}, Operation={Operation}", parcelId, operation);
            throw;
        }
    }

    /// <summary>
    /// 校验各操作类型的必须字段（不包含时间字符串解析，时间解析由 Host 层负责）。
    /// </summary>
    /// <param name="operation">目标操作类型。</param>
    /// <param name="request">更新请求合同。</param>
    /// <param name="completedTime">已解析的完结时间（Host 层传入，可为 null）。</param>
    private static void ValidateOperationFields(ParcelUpdateOperation operation, ParcelUpdateRequest request, DateTime? completedTime) {
        switch (operation) {
            case ParcelUpdateOperation.MarkCompleted:
                if (completedTime is null) {
                    throw new ArgumentException("MarkCompleted 操作必须提供 completedTime 字段。", nameof(completedTime));
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
