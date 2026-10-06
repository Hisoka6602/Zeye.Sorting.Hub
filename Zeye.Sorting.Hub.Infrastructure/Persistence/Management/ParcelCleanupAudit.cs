using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>永久清理操作记录，独立于被清理的聚合及会轮转的请求日志。</summary>
public sealed record ParcelCleanupAudit {
    /// <summary>记录键前缀；不纳入任何过期清理策略。</summary>
    public const string Prefix = "parcel-cleanup:";
    /// <summary>汇总存储格式，不保存逐票包裹快照。</summary>
    public const string SummaryStorageFormat = "operation-summary";
    /// <summary>汇总审计的清理范围，独立处理事实仍按原有策略保留。</summary>
    public const string SummaryScope = "包裹主数据及聚合附属数据；保留来源身份和处理事实，清理历史仅保存操作汇总。";
    /// <summary>物理删除恢复边界，汇总记录不承担包裹备份功能。</summary>
    public const string SummaryCompensationBoundary = "物理删除不支持自动回滚；清理历史仅永久保留操作汇总，不保存逐票包裹快照。恢复包裹需要使用备份。";
    /// <summary>每批事务提交凭据的键前缀。</summary>
    public static string BatchPrefix(string id) => "parcel-cleanup-batch:" + id + ":";
    /// <summary>操作编号。</summary>
    public required string Id { get; init; }
    /// <summary>操作人、账号、来源地址与请求追踪编号。</summary>
    public required ParcelCleanupOperator Operator { get; init; }
    /// <summary>创建时间上界，严格早于该本地时间。</summary>
    public DateTime CreatedBefore { get; init; }
    /// <summary>开始时间。</summary>
    public DateTime StartedAtLocal { get; init; } = DateTime.Now;
    /// <summary>结束时间；未完成时为空。</summary>
    public DateTime? CompletedAtLocal { get; init; }
    /// <summary>服务端隔离决策。</summary>
    public required string Decision { get; init; }
    /// <summary>执行状态：running/completed/failed/cancelled。</summary>
    public string Status { get; init; } = "running";
    /// <summary>单次计划数量，最多一万条。</summary>
    public int PlannedCount { get; init; }
    /// <summary>已在事务中提交删除的准确数量。</summary>
    public int ExecutedCount { get; init; }
    /// <summary>已提交的批次数量。</summary>
    public int BatchCount { get; init; }
    /// <summary>记录存储格式；空值表示仍含逐票清单的旧版记录。</summary>
    public string? StorageFormat { get; init; }
    /// <summary>清理范围、保留对象与恢复边界。</summary>
    public string Scope { get; init; } = SummaryScope;
    /// <summary>物理删除的补偿边界。</summary>
    public required string CompensationBoundary { get; init; }
    /// <summary>失败原因，不记录口令或数据库连接信息。</summary>
    public string? ErrorMessage { get; init; }
}
