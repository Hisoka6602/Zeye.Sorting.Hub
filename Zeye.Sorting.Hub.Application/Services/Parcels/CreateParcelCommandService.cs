using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 管理端新增包裹应用服务。
/// </summary>
public sealed class CreateParcelCommandService {
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
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新增成功后的包裹详情响应。</returns>
    public async Task<ParcelDetailResponse> ExecuteAsync(ParcelCreateRequest request, CancellationToken cancellationToken) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        // 步骤 1：验证并映射枚举类型，拒绝无效的整型枚举值。
        if (!Enum.IsDefined(typeof(ParcelType), request.Type)) {
            Logger.Warn("新增包裹参数非法，Type={Type}", request.Type);
            throw new ArgumentOutOfRangeException(nameof(request.Type), "包裹类型无效。");
        }

        if (!Enum.IsDefined(typeof(ApiRequestStatus), request.RequestStatus)) {
            Logger.Warn("新增包裹参数非法，RequestStatus={RequestStatus}", request.RequestStatus);
            throw new ArgumentOutOfRangeException(nameof(request.RequestStatus), "接口访问状态无效。");
        }

        if (!Enum.IsDefined(typeof(NoReadType), request.NoReadType)) {
            Logger.Warn("新增包裹参数非法，NoReadType={NoReadType}", request.NoReadType);
            throw new ArgumentOutOfRangeException(nameof(request.NoReadType), "NoRead 类型无效。");
        }

        try {
            // 步骤 2：通过领域工厂方法构建聚合根，由领域层统一做字段合法性校验。
            var parcel = Parcel.Create(
                parcelTimestamp: request.ParcelTimestamp,
                type: (ParcelType)request.Type,
                barCodes: request.BarCodes,
                weight: request.Weight,
                workstationName: request.WorkstationName,
                scannedTime: request.ScannedTime,
                dischargeTime: request.DischargeTime,
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

            // 步骤 3：调用仓储持久化，EF Core 会在保存后将数据库分配的 Id 回写到实体。
            var result = await _parcelRepository.AddAsync(parcel, cancellationToken);
            if (!result.IsSuccess) {
                Logger.Error("新增包裹失败，BarCodes={BarCodes}, ErrorMessage={ErrorMessage}", request.BarCodes, result.ErrorMessage);
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
