namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
/// <summary>界面配置的分类规则，已发布规则参与后续处理记录的分类。</summary>
public sealed record ClassificationRule {
    /// <summary>小范围规则编号，不使用包裹的长整数编号。</summary>
    public int Id { get; init; }
    /// <summary>规则名称。</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>版本名称。</summary>
    public string Version { get; init; } = "v1.0.0";
    /// <summary>发布状态。</summary>
    public string Status { get; init; } = "草稿";
    /// <summary>产线或工作站范围。</summary>
    public string Scope { get; init; } = "全部产线";
    /// <summary>最后修改的本地时间文本。</summary>
    public string Modified { get; init; } = string.Empty;
    /// <summary>修改人。</summary>
    public string Editor { get; init; } = string.Empty;
    /// <summary>用途说明。</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>附加说明。</summary>
    public string? Note { get; init; }
    /// <summary>分类结果名称。</summary>
    public string? TargetType { get; init; }
    /// <summary>异常分类枚举值。</summary>
    public int? ExceptionType { get; init; }
    /// <summary>包裹分类枚举值，与异常分类结果互斥。</summary>
    public int? ParcelType { get; init; }
    /// <summary>全部条件或任意条件。</summary>
    public string MatchMode { get; init; } = "all";
    /// <summary>匹配条件。</summary>
    public ClassificationCondition[] Conditions { get; init; } = [];
    /// <summary>动作说明；实时处理只执行分类标记。</summary>
    public string[] Actions { get; init; } = [];
    /// <summary>系统规则标记。</summary>
    public string? SystemRule { get; init; }
}
