using NLog;
using Zeye.Sorting.Hub.Application.Mappers.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Application.Services.Idempotency;
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
    /// 异常 Data 中冲突错误码键名。
    /// </summary>
    public const string ErrorCodeDataKey = "ErrorCode";

    /// <summary>
    /// 幂等请求处理中错误码。
    /// </summary>
    public const string IdempotencyInProgressErrorCode = "Idempotency.Request.InProgress";

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 幂等守卫应用服务。
    /// </summary>
    private readonly IdempotencyGuardService _idempotencyGuardService;

    /// <summary>
    /// 初始化管理端新增包裹应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    /// <param name="idempotencyGuardService">幂等守卫应用服务。</param>
    public CreateParcelCommandService(
        IParcelRepository parcelRepository,
        IdempotencyGuardService idempotencyGuardService) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
        _idempotencyGuardService = idempotencyGuardService ?? throw new ArgumentNullException(nameof(idempotencyGuardService));
    }

    /// <summary>
    /// 执行新增包裹。
    /// </summary>
    /// <param name="request">新增请求合同。</param>
    /// <param name="scannedTime">已解析的扫码时间（本地时间，由 Host 层完成字符串解析与 UTC 拒绝）。</param>
    /// <param name="dischargeTime">已解析的落格时间（本地时间，由 Host 层完成字符串解析与 UTC 拒绝）。</param>
    /// <param name="sourceSystem">幂等来源系统。</param>
    /// <param name="operationName">幂等操作名称。</param>
    /// <param name="payloadHash">请求载荷哈希。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新增成功后的包裹详情响应，以及是否命中幂等重放。</returns>
    public async Task<(ParcelDetailResponse Response, bool IsReplay)> ExecuteAsync(
        ParcelCreateRequest request,
        DateTime scannedTime,
        DateTime dischargeTime,
        string sourceSystem,
        string operationName,
        string payloadHash,
        CancellationToken cancellationToken) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        try {
            // 步骤 1：通过共享映射器构建聚合根，由领域层统一做字段合法性校验。
            var parcel = ParcelCreateRequestMapper.MapToParcel(request, scannedTime, dischargeTime);
            
            // 步骤 2：通过幂等守卫保护真实写入，重复请求命中 Completed 时直接回放已有结果。
            var executionResult = await _idempotencyGuardService.ExecuteAsync(
                sourceSystem,
                operationName,
                request.Id.ToString(),
                payloadHash,
                async innerCancellationToken => {
                    var result = await _parcelRepository.AddAsync(parcel, innerCancellationToken);
                    if (!result.IsSuccess) {
                        Logger.Error("新增包裹失败，BarCodes={BarCodes}, ErrorMessage={ErrorMessage}", request.BarCodes, result.ErrorMessage);
                        ThrowCreateFailedException(result);
                    }

                    return ParcelContractMapper.ToDetail(parcel);
                },
                async innerCancellationToken => {
                    var persistedParcel = await _parcelRepository.GetByIdAsync(request.Id, innerCancellationToken);
                    return persistedParcel is null ? null : ParcelContractMapper.ToDetail(persistedParcel);
                },
                cancellationToken);

            // 步骤 3：返回首写结果或重放结果。
            return executionResult;
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, IdempotencyGuardService.RequestInProgressMessage, StringComparison.Ordinal)) {
            ex.Data[ErrorCodeDataKey] = IdempotencyInProgressErrorCode;
            Logger.Warn(ex, "新增包裹命中幂等处理中状态，ParcelId={ParcelId}", request.Id);
            throw;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException) {
            Logger.Error(ex, "新增包裹发生意外异常，BarCodes={BarCodes}", request.BarCodes);
            throw;
        }
    }

    /// <summary>
    /// 抛出新增包裹失败异常，并在冲突场景附带稳定错误码。
    /// </summary>
    /// <param name="result">仓储执行结果。</param>
    private static void ThrowCreateFailedException(RepositoryResult result) {
        var exception = new InvalidOperationException(result.ErrorMessage ?? "新增包裹失败。");
        if (string.Equals(result.ErrorCode, ParcelIdConflictErrorCode, StringComparison.Ordinal)) {
            exception.Data[ErrorCodeDataKey] = ParcelIdConflictErrorCode;
        }

        throw exception;
    }
}
