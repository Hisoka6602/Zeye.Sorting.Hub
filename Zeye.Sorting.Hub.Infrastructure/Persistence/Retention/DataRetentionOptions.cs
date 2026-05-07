using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留治理配置。
/// </summary>
public sealed class DataRetentionOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:Retention";

    /// <summary>
    /// 每批处理数量最小值。
    /// </summary>
    public const int MinBatchSize = 1;

    /// <summary>
    /// 每批处理数量最大值。
    /// </summary>
    public const int MaxBatchSize = 5000;

    /// <summary>
    /// 执行间隔最小值（分钟）。
    /// </summary>
    public const int MinExecutionIntervalMinutes = 1;

    /// <summary>
    /// 执行间隔最大值（分钟）。
    /// </summary>
    public const int MaxExecutionIntervalMinutes = 1440;

    /// <summary>
    /// 保留天数最小值。
    /// </summary>
    public const int MinRetentionDays = 1;

    /// <summary>
    /// 保留天数最大值。
    /// </summary>
    public const int MaxRetentionDays = 3650;

    /// <summary>
    /// 是否启用数据保留治理。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否仅执行 dry-run。
    /// 可填写范围：true / false；当前版本仅允许 true。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// 单策略每批处理上限。
    /// 可填写范围：1~5000。
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// 后台执行间隔（分钟）。
    /// 可填写范围：1~1440。
    /// </summary>
    public int ExecutionIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 策略集合。
    /// 可填写范围：至少支持 WebRequestAuditLog / OutboxMessage / InboxMessage / IdempotencyRecord / ArchiveTask / DeadLetterWriteEntry / SlowQueryProfile。
    /// </summary>
    public List<DataRetentionPolicy> Policies { get; set; } = [];

    /// <summary>
    /// 从配置源应用选项值。
    /// </summary>
    /// <param name="options">目标选项。</param>
    /// <param name="configuration">配置源。</param>
    public static void Apply(DataRetentionOptions options, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionPath);
        options.IsEnabled = ReadBool(section, nameof(IsEnabled), options.IsEnabled);
        options.DryRun = ReadBool(section, nameof(DryRun), options.DryRun);
        options.BatchSize = ReadInt(section, nameof(BatchSize), options.BatchSize);
        options.ExecutionIntervalMinutes = ReadInt(section, nameof(ExecutionIntervalMinutes), options.ExecutionIntervalMinutes);
        options.Policies = ReadPolicies(section);
        if (options.Policies.Count == 0) {
            options.Policies = DataRetentionPolicy.CreateDefaultPolicies().Select(ClonePolicy).ToList();
            return;
        }

        options.Policies = options.Policies
            .Select(NormalizePolicy)
            .ToList();
    }

    /// <summary>
    /// 判断策略列表是否合法。
    /// </summary>
    /// <param name="policies">策略列表。</param>
    /// <returns>合法返回 true。</returns>
    public static bool ArePoliciesValid(IReadOnlyList<DataRetentionPolicy>? policies) {
        if (policies is null || policies.Count == 0) {
            return false;
        }

        var policyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var policy in policies) {
            if (!DataRetentionPolicy.TryNormalizeName(policy.Name, out var normalizedName)) {
                return false;
            }

            if (policy.RetentionDays is < MinRetentionDays or > MaxRetentionDays) {
                return false;
            }

            if (!policyNames.Add(normalizedName)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 归一化单个策略。
    /// </summary>
    /// <param name="policy">原始策略。</param>
    /// <returns>归一化后的策略。</returns>
    private static DataRetentionPolicy NormalizePolicy(DataRetentionPolicy? policy) {
        if (policy is null) {
            return ClonePolicy(DataRetentionPolicy.CreateDefaultPolicies()[0]);
        }

        var normalizedName = DataRetentionPolicy.TryNormalizeName(policy.Name, out var name)
            ? name
            : policy.Name?.Trim() ?? string.Empty;
        var retentionDays = policy.RetentionDays > 0
            ? policy.RetentionDays
            : DataRetentionPolicy.GetDefaultRetentionDays(normalizedName);
        return new DataRetentionPolicy {
            Name = normalizedName,
            RetentionDays = retentionDays
        };
    }

    /// <summary>
    /// 复制策略项。
    /// </summary>
    /// <param name="policy">原始策略。</param>
    /// <returns>复制后的策略。</returns>
    private static DataRetentionPolicy ClonePolicy(DataRetentionPolicy policy) {
        return new DataRetentionPolicy {
            Name = policy.Name,
            RetentionDays = policy.RetentionDays
        };
    }

    /// <summary>
    /// 读取布尔配置项。
    /// </summary>
    /// <param name="section">配置节。</param>
    /// <param name="key">配置键。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>解析后的布尔值。</returns>
    private static bool ReadBool(IConfiguration section, string key, bool defaultValue) {
        return bool.TryParse(section[key], out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    /// <summary>
    /// 读取整数配置项。
    /// </summary>
    /// <param name="section">配置节。</param>
    /// <param name="key">配置键。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <returns>解析后的整数值。</returns>
    private static int ReadInt(IConfiguration section, string key, int defaultValue) {
        return int.TryParse(section[key], out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    /// <summary>
    /// 读取策略集合。
    /// </summary>
    /// <param name="section">配置节。</param>
    /// <returns>策略集合。</returns>
    private static List<DataRetentionPolicy> ReadPolicies(IConfigurationSection section) {
        var policySection = section.GetSection(nameof(Policies));
        var policies = new List<DataRetentionPolicy>();
        foreach (var child in policySection.GetChildren()) {
            policies.Add(new DataRetentionPolicy {
                Name = child[nameof(DataRetentionPolicy.Name)] ?? string.Empty,
                RetentionDays = ReadInt(child, nameof(DataRetentionPolicy.RetentionDays), 0)
            });
        }

        return policies;
    }
}
