namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留策略项。
/// </summary>
public sealed class DataRetentionPolicy {
    /// <summary>
    /// Web 请求审计日志策略名称。
    /// </summary>
    public const string WebRequestAuditLog = "WebRequestAuditLog";

    /// <summary>
    /// Outbox 消息策略名称。
    /// </summary>
    public const string OutboxMessage = "OutboxMessage";

    /// <summary>
    /// Inbox 消息策略名称。
    /// </summary>
    public const string InboxMessage = "InboxMessage";

    /// <summary>
    /// 幂等记录策略名称。
    /// </summary>
    public const string IdempotencyRecord = "IdempotencyRecord";

    /// <summary>
    /// 归档任务策略名称。
    /// </summary>
    public const string ArchiveTask = "ArchiveTask";

    /// <summary>
    /// 死信写入记录策略名称。
    /// </summary>
    public const string DeadLetterWriteEntry = "DeadLetterWriteEntry";

    /// <summary>
    /// 慢查询画像策略名称。
    /// </summary>
    public const string SlowQueryProfile = "SlowQueryProfile";

    /// <summary>
    /// 策略名称。
    /// 可填写范围：WebRequestAuditLog / OutboxMessage / InboxMessage / IdempotencyRecord / ArchiveTask / DeadLetterWriteEntry / SlowQueryProfile。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 保留天数。
    /// 可填写范围：1~3650 的整数。
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// 判断策略名称是否受支持。
    /// </summary>
    /// <param name="name">策略名称。</param>
    /// <returns>受支持返回 true。</returns>
    public static bool IsSupportedPolicyName(string? name) {
        return TryNormalizeName(name, out _);
    }

    /// <summary>
    /// 尝试归一化策略名称。
    /// </summary>
    /// <param name="name">原始名称。</param>
    /// <param name="normalizedName">归一化名称。</param>
    /// <returns>成功返回 true。</returns>
    public static bool TryNormalizeName(string? name, out string normalizedName) {
        var trimmedName = string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : name.Trim();
        if (string.Equals(trimmedName, WebRequestAuditLog, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = WebRequestAuditLog;
            return true;
        }

        if (string.Equals(trimmedName, OutboxMessage, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = OutboxMessage;
            return true;
        }

        if (string.Equals(trimmedName, InboxMessage, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = InboxMessage;
            return true;
        }

        if (string.Equals(trimmedName, IdempotencyRecord, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = IdempotencyRecord;
            return true;
        }

        if (string.Equals(trimmedName, ArchiveTask, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = ArchiveTask;
            return true;
        }

        if (string.Equals(trimmedName, DeadLetterWriteEntry, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = DeadLetterWriteEntry;
            return true;
        }

        if (string.Equals(trimmedName, SlowQueryProfile, StringComparison.OrdinalIgnoreCase)) {
            normalizedName = SlowQueryProfile;
            return true;
        }

        normalizedName = trimmedName;
        return false;
    }

    /// <summary>
    /// 创建默认策略集合。
    /// </summary>
    /// <returns>默认策略集合。</returns>
    public static IReadOnlyList<DataRetentionPolicy> CreateDefaultPolicies() {
        return [
            new DataRetentionPolicy { Name = WebRequestAuditLog, RetentionDays = 30 },
            new DataRetentionPolicy { Name = OutboxMessage, RetentionDays = 14 },
            new DataRetentionPolicy { Name = InboxMessage, RetentionDays = 30 },
            new DataRetentionPolicy { Name = IdempotencyRecord, RetentionDays = 30 },
            new DataRetentionPolicy { Name = ArchiveTask, RetentionDays = 30 },
            new DataRetentionPolicy { Name = DeadLetterWriteEntry, RetentionDays = 14 },
            new DataRetentionPolicy { Name = SlowQueryProfile, RetentionDays = 7 }
        ];
    }

    /// <summary>
    /// 获取指定策略的默认保留天数。
    /// </summary>
    /// <param name="name">策略名称。</param>
    /// <returns>默认保留天数。</returns>
    public static int GetDefaultRetentionDays(string name) {
        return name switch {
            WebRequestAuditLog => 30,
            OutboxMessage => 14,
            InboxMessage => 30,
            IdempotencyRecord => 30,
            ArchiveTask => 30,
            DeadLetterWriteEntry => 14,
            SlowQueryProfile => 7,
            _ => DataRetentionOptions.MinRetentionDays
        };
    }
}
