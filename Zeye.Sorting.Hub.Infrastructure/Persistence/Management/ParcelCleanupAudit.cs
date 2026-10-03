using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>永久清理操作记录，独立于被清理的聚合及会轮转的请求日志。</summary>
public sealed record ParcelCleanupAudit {
    /// <summary>记录键前缀；不纳入任何过期清理策略。</summary>
    public const string Prefix = "parcel-cleanup:";
    /// <summary>每批删除快照的键前缀。</summary>
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
    /// <summary>清理范围、保留对象与恢复边界。</summary>
    public string Scope { get; init; } = "包裹主数据及聚合附属数据；保留来源身份、处理事实及清理操作记录。";
    /// <summary>物理删除的补偿边界。</summary>
    public required string CompensationBoundary { get; init; }
    /// <summary>失败原因，不记录口令或数据库连接信息。</summary>
    public string? ErrorMessage { get; init; }
}
