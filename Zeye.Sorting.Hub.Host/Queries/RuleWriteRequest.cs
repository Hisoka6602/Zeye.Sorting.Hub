using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>带版本号的规则提交请求。</summary>
public sealed record RuleWriteRequest {
    /// <summary>读取规则时的版本号。</summary>
    public int ExpectedRevision { get; init; }
    /// <summary>当前分类下的完整规则列表。</summary>
    public ClassificationRule[] Rules { get; init; } = [];
}
