namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
/// <summary>与 Fusion 已对接协议保持一致的默认异常及不可删除的未知异常。</summary>
public static class ClassificationRuleDefaults {
    /// <summary>创建规范的系统异常规则，不包含演示业务规则。</summary>
    public static ClassificationRule[] Create() {
        var codes = new[] { ("ParcelSpacingViolation", 13, "包裹间距违规"), ("TargetChuteAssignmentRejected", 14, "目标格口分配被拒绝"), ("RoutingTimeout", 3, "等待目标格口超时") };
        return [.. codes.Select((item, index) => new ClassificationRule {
            Id = -101 - index, Name = item.Item3 + "分类规则", TargetType = item.Item3, ExceptionType = item.Item2,
            Status = "已发布", Editor = "系统", Description = "来源于 Fusion 分拣机已对接的异常报文。",
            SystemRule = "sorter-protocol", Conditions = [new() { Field = "来源异常代码", Operator = "等于", Value = item.Item1 }],
            Actions = ["标记分拣异常", "记录异常原因"]
        }), new ClassificationRule {
            Id = -199, Name = "未知异常", TargetType = "未知异常", ExceptionType = 0, Status = "已发布", Editor = "系统",
            Description = "当所有已发布异常规则均未匹配时使用。", SystemRule = "unknown-fallback", Actions = ["标记分拣异常", "记录异常原因"]
        }];
    }
}
