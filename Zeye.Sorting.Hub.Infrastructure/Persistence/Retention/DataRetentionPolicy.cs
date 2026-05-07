namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留策略。
/// </summary>
public sealed record class DataRetentionPolicy {
    /// <summary>
    /// 支持的策略名称：WebRequestAuditLog。
    /// </summary>
    public const string WebRequestAuditLogName = "WebRequestAuditLog";

    /// <summary>
    /// 支持的策略名称：OutboxMessage。
    /// </summary>
    public const string OutboxMessageName = "OutboxMessage";

    /// <summary>
    /// 支持的策略名称：InboxMessage。
    /// </summary>
    public const string InboxMessageName = "InboxMessage";

    /// <summary>
    /// 支持的策略名称：IdempotencyRecord。
    /// </summary>
    public const string IdempotencyRecordName = "IdempotencyRecord";

    /// <summary>
    /// 支持的策略名称：ArchiveTask。
    /// </summary>
    public const string ArchiveTaskName = "ArchiveTask";

    /// <summary>
    /// 支持的策略名称：DeadLetterWriteEntry。
    /// </summary>
    public const string DeadLetterWriteEntryName = "DeadLetterWriteEntry";

    /// <summary>
    /// 支持的策略名称：SlowQueryProfile。
    /// </summary>
    public const string SlowQueryProfileName = "SlowQueryProfile";

    /// <summary>
    /// 保留天数最小值。
    /// </summary>
    public const int MinRetentionDays = 1;

    /// <summary>
    /// 保留天数最大值。
    /// </summary>
    public const int MaxRetentionDays = 3650;

    /// <summary>
    /// 支持的策略名称集合。
    /// </summary>
    private static readonly IReadOnlySet<string> SupportedNames = new HashSet<string>(StringComparer.Ordinal) {
        WebRequestAuditLogName,
        OutboxMessageName,
        InboxMessageName,
        IdempotencyRecordName,
        ArchiveTaskName,
        DeadLetterWriteEntryName,
        SlowQueryProfileName
    };

    /// <summary>
    /// 策略名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 保留天数。
    /// </summary>
    public required int RetentionDays { get; init; }

    /// <summary>
    /// 判断策略名称是否受支持。
    /// </summary>
    /// <param name="name">策略名称。</param>
    /// <returns>受支持返回 true。</returns>
    public static bool IsSupportedName(string? name) {
        return !string.IsNullOrWhiteSpace(name) && SupportedNames.Contains(name.Trim());
    }

    /// <summary>
    /// 判断保留天数是否有效。
    /// </summary>
    /// <param name="retentionDays">保留天数。</param>
    /// <returns>有效返回 true。</returns>
    public static bool IsValidRetentionDays(int retentionDays) {
        return retentionDays is >= MinRetentionDays and <= MaxRetentionDays;
    }

    /// <summary>
    /// 创建默认策略清单。
    /// </summary>
    /// <returns>默认策略清单。</returns>
    public static IReadOnlyList<DataRetentionPolicy> CreateDefaultPolicies() {
        return [
            new DataRetentionPolicy { Name = WebRequestAuditLogName, RetentionDays = 30 },
            new DataRetentionPolicy { Name = OutboxMessageName, RetentionDays = 14 },
            new DataRetentionPolicy { Name = InboxMessageName, RetentionDays = 30 },
            new DataRetentionPolicy { Name = IdempotencyRecordName, RetentionDays = 30 },
            new DataRetentionPolicy { Name = ArchiveTaskName, RetentionDays = 90 },
            new DataRetentionPolicy { Name = DeadLetterWriteEntryName, RetentionDays = 14 },
            new DataRetentionPolicy { Name = SlowQueryProfileName, RetentionDays = 7 }
        ];
    }
}
