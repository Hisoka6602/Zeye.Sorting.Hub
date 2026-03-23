using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 管理端过期包裹清理应用服务（治理型，调用仓储内置隔离器）。
/// </summary>
public sealed class CleanupExpiredParcelsCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化管理端过期包裹清理应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public CleanupExpiredParcelsCommandService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行过期包裹清理（必须经过仓储内置隔离器，由隔离器决策 blocked / dry-run / execute）。
    /// </summary>
    /// <param name="createdBefore">过期时间上界（本地时间，早于此时间创建的包裹为过期候选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清理治理响应合同（含决策、计划量、执行量、补偿边界）。</returns>
    public async Task<ParcelCleanupExpiredResponse> ExecuteAsync(DateTime createdBefore, CancellationToken cancellationToken) {
        try {
            // 步骤 1：调用仓储过期清理方法，由仓储内置隔离器完成 blocked/dry-run/execute 决策。
            //         本层不得绕过隔离器，不可直接操作数据。
            var result = await _parcelRepository.RemoveExpiredAsync(createdBefore, cancellationToken);

            if (!result.IsSuccess) {
                Logger.Error(
                    "过期包裹清理仓储调用失败，CreatedBefore={CreatedBefore}, ErrorMessage={ErrorMessage}",
                    createdBefore,
                    result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage ?? "过期包裹清理失败。");
            }

            // 步骤 2：将领域层 DangerousBatchActionResult 映射为对外合同响应。
            var actionResult = result.Value;
            return new ParcelCleanupExpiredResponse {
                ActionName = actionResult.ActionName,
                Decision = MapDecisionToString(actionResult.Decision),
                PlannedCount = actionResult.PlannedCount,
                ExecutedCount = actionResult.ExecutedCount,
                IsDryRun = actionResult.IsDryRun,
                IsBlockedByGuard = actionResult.IsBlockedByGuard,
                CompensationBoundary = actionResult.CompensationBoundary
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            Logger.Error(ex, "过期包裹清理发生意外异常，CreatedBefore={CreatedBefore}", createdBefore);
            throw;
        }
    }

    /// <summary>
    /// 将领域隔离决策枚举映射为对外合同字符串（blocked / dry-run / execute）。
    /// </summary>
    /// <param name="decision">领域隔离决策枚举值。</param>
    /// <returns>外部合同决策字符串。</returns>
    private static string MapDecisionToString(ActionIsolationDecision decision) {
        return decision switch {
            ActionIsolationDecision.BlockedByGuard => "blocked",
            ActionIsolationDecision.DryRunOnly => "dry-run",
            ActionIsolationDecision.Execute => "execute",
            _ => decision.ToString()
        };
    }
}
