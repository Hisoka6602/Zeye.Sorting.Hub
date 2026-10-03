using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

/// <summary>按 Fusion 已对接的 ParcelException.ExceptionType 分类，未知编码保留原文并使用兜底类型。</summary>
public static class SorterExceptionClassifier {
    // 来源：SortingFusionService 的 SorterEventParser、ParcelSessionStore 和协议回归测试。
    private static readonly IReadOnlyDictionary<string, ParcelExceptionType> Mappings =
        new Dictionary<string, ParcelExceptionType>(StringComparer.OrdinalIgnoreCase) {
            ["ParcelSpacingViolation"] = ParcelExceptionType.ParcelSpacingViolation,
            ["TargetChuteAssignmentRejected"] = ParcelExceptionType.TargetChuteAssignmentRejected,
            ["RoutingTimeout"] = ParcelExceptionType.WaitTargetChuteTimeout
        };

    /// <summary>仅精确匹配已知来源编码；所有规则未匹配时返回不可移除的未知异常类型。</summary>
    public static ParcelExceptionType Classify(string? sourceCode) =>
        sourceCode is not null && Mappings.TryGetValue(sourceCode.Trim(), out var type)
            ? type
            : ParcelExceptionType.Unknown;
}
