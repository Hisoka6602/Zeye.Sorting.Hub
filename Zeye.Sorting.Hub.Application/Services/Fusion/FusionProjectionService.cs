using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Application.Services.Parcels;

namespace Zeye.Sorting.Hub.Application.Services.Fusion;

/// <summary>复用现有包裹处理用例，将耐久接收簿投影到业务聚合。</summary>
public sealed class FusionProjectionService(IFusionIngestionGateway ingress, ParcelProcessingApplicationService processing) {
    /// <summary>投影异常日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>认领有界任务；每条事实独立重试，不丢失已向来源确认的原文。</summary>
    public async Task<int> ProjectAsync(CancellationToken cancellationToken) {
        var changed = 0;
        foreach (var item in await ingress.ClaimProjectionsAsync(cancellationToken)) {
            string? parcelId = null;
            string? error = null;
            try {
                var result = await processing.AppendAsync(item.Request, cancellationToken);
                if (result.IsSuccess) { parcelId = result.Value?.ParcelId; changed++; }
                else error = result.ErrorCode ?? "ProjectionFailed";
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                Logger.Error(exception, "Fusion 事实投影失败，Key={Key}", item.Key);
                error = "ProjectionFailed";
            }
            await ingress.FinishProjectionAsync(item, parcelId, error, cancellationToken);
        }
        return changed;
    }
}
