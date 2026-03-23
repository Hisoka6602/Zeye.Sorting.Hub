using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 管理端新增包裹应用服务。
/// </summary>
public sealed class CreateParcelCommandService {
    /// <summary>
    /// 新增失败冲突错误码（供 Host 层稳定映射 409）。
    /// </summary>
    public const string ParcelIdConflictErrorCode = RepositoryErrorCodes.ParcelIdConflict;

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化管理端新增包裹应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public CreateParcelCommandService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行新增包裹。
    /// </summary>
    /// <param name="request">新增请求合同。</param>
    /// <param name="scannedTime">已解析的扫码时间（本地时间，由 Host 层完成字符串解析与 UTC 拒绝）。</param>
    /// <param name="dischargeTime">已解析的落格时间（本地时间，由 Host 层完成字符串解析与 UTC 拒绝）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新增成功后的包裹详情响应。</returns>
    public async Task<ParcelDetailResponse> ExecuteAsync(ParcelCreateRequest request, DateTime scannedTime, DateTime dischargeTime, CancellationToken cancellationToken) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        // 步骤 1：验证并映射枚举类型，拒绝无效的整型枚举值。
        EnumGuard.ThrowIfUndefined<ParcelType>(request.Type, nameof(request.Type), "包裹类型无效。", "新增包裹");
        EnumGuard.ThrowIfUndefined<ApiRequestStatus>(request.RequestStatus, nameof(request.RequestStatus), "接口访问状态无效。", "新增包裹");
        EnumGuard.ThrowIfUndefined<NoReadType>(request.NoReadType, nameof(request.NoReadType), "NoRead 类型无效。", "新增包裹");

        try {
            // 步骤 2：通过领域工厂方法构建聚合根，由领域层统一做字段合法性校验。
            var parcel = Parcel.Create(
                id: request.Id,
                parcelTimestamp: request.ParcelTimestamp,
                type: (ParcelType)request.Type,
                barCodes: request.BarCodes,
                weight: request.Weight,
                workstationName: request.WorkstationName,
                scannedTime: scannedTime,
                dischargeTime: dischargeTime,
                targetChuteId: request.TargetChuteId,
                actualChuteId: request.ActualChuteId,
                requestStatus: (ApiRequestStatus)request.RequestStatus,
                bagCode: request.BagCode,
                isSticking: request.IsSticking,
                length: request.Length,
                width: request.Width,
                height: request.Height,
                volume: request.Volume,
                hasImages: request.HasImages,
                hasVideos: request.HasVideos,
                coordinate: request.Coordinate,
                noReadType: (NoReadType)request.NoReadType,
                sorterCarrierId: request.SorterCarrierId,
                segmentCodes: request.SegmentCodes,
                lifecycleMilliseconds: request.LifecycleMilliseconds);

            // 步骤 3：调用仓储持久化（包裹 Id 由调用方传入并由领域工厂赋值）。
            var result = await _parcelRepository.AddAsync(parcel, cancellationToken);
            if (!result.IsSuccess) {
                Logger.Error("新增包裹失败，BarCodes={BarCodes}, ErrorMessage={ErrorMessage}", request.BarCodes, result.ErrorMessage);
                if (string.Equals(result.ErrorCode, ParcelIdConflictErrorCode, StringComparison.Ordinal)) {
                    throw new InvalidOperationException(ParcelIdConflictErrorCode, new Exception(result.ErrorMessage ?? "新增包裹失败。"));
                }

                throw new InvalidOperationException(result.ErrorMessage ?? "新增包裹失败。");
            }

            // 步骤 4：映射领域对象到合同响应并返回。
            return ParcelContractMapper.ToDetail(parcel);
        }
        catch (ArgumentException) {
            // 领域层验证异常直接向上传播，由 Host 层统一返回 400 Bad Request。
            throw;
        }
        catch (InvalidOperationException) {
            throw;
        }
        catch (Exception ex) {
            Logger.Error(ex, "新增包裹发生意外异常，BarCodes={BarCodes}", request.BarCodes);
            throw;
        }
    }
}
