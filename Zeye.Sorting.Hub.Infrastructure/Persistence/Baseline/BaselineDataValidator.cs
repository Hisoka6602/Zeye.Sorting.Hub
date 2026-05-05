using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NLog;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

/// <summary>
/// 基线数据与配置一致性校验器。
/// </summary>
public sealed partial class BaselineDataValidator {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档任务类型参考数据目录名称。
    /// </summary>
    private const string ArchiveTaskTypeCatalogName = nameof(ArchiveTaskType);

    /// <summary>
    /// 归档任务状态参考数据目录名称。
    /// </summary>
    private const string ArchiveTaskStatusCatalogName = nameof(ArchiveTaskStatus);

    /// <summary>
    /// 失败模式参考数据目录名称。
    /// </summary>
    private const string FailureModeCatalogName = nameof(MigrationFailureMode);

    /// <summary>
    /// 本地时间配置键集合。
    /// </summary>
    private static readonly string[] LocalTimeConfigurationKeys = [
        "Persistence:Sharding:ParcelStartTime"
    ];

    /// <summary>
    /// 必要配置键集合。
    /// </summary>
    private static readonly string[] RequiredConfigurationKeys = [
        "Persistence:Provider",
        "Persistence:Sharding:ParcelStartTime",
        BaselineDataOptions.FailureModeConfigKey
    ];

    /// <summary>
    /// 关键枚举类型集合。
    /// </summary>
    private static readonly Type[] KeyEnumTypes = [
        typeof(MigrationFailureMode),
        typeof(ArchiveTaskType),
        typeof(ArchiveTaskStatus),
        typeof(ParcelStatus),
        typeof(ParcelShardingStrategyMode),
        typeof(ParcelTimeShardingGranularity),
        typeof(ParcelVolumeThresholdAction),
        typeof(ParcelFinerGranularityMode),
        typeof(ParcelFinerGranularityPlanLifecycle),
        typeof(ParcelAggregateShardingRuleKind)
    ];

    /// <summary>
    /// 最近一次校验结果访问锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// 应用配置源。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 基线配置选项。
    /// </summary>
    private readonly IOptions<BaselineDataOptions> _baselineDataOptions;

    /// <summary>
    /// 最近一次校验结果。
    /// </summary>
    private BaselineDataValidationResult? _latestResult;

    /// <summary>
    /// 初始化基线校验器。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <param name="baselineDataOptions">配置选项。</param>
    public BaselineDataValidator(
        IConfiguration configuration,
        IOptions<BaselineDataOptions> baselineDataOptions) {
        _configuration = configuration;
        _baselineDataOptions = baselineDataOptions;
    }

    /// <summary>
    /// 执行基线校验。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>校验结果。</returns>
    public Task<BaselineDataValidationResult> ValidateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var options = _baselineDataOptions.Value;
        if (!options.IsValidationEnabled) {
            var disabledResult = BaselineDataValidationResult.CreateDisabled(options);
            SetLatestResult(disabledResult);
            return Task.FromResult(disabledResult);
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        ValidateRequiredConfigurationKeys(errors);
        var provider = ValidateProviderAndConnectionString(errors);
        ValidateLocalTimeConfigurations(errors);
        ValidateShardingStartTime(errors);
        ValidateEnumDescriptions(errors);
        ValidateDefaultReferenceData(errors);

        var failureMode = options.ResolveFailureMode();
        var result = new BaselineDataValidationResult {
            ValidatedAtLocal = DateTime.Now,
            IsValidationEnabled = options.IsValidationEnabled,
            IsSeedEnabled = options.IsSeedEnabled,
            FailureMode = failureMode,
            IsValid = errors.Count == 0,
            ShouldBlockStartup = errors.Count > 0 && failureMode == MigrationFailureMode.FailFast,
            WasSeedAttempted = false,
            SeededRecordCount = 0,
            Summary = BuildSummary(errors, warnings, provider),
            Errors = errors,
            Warnings = warnings,
            SeedMessages = []
        };
        SetLatestResult(result);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 读取最近一次校验结果。
    /// </summary>
    /// <returns>校验结果。</returns>
    public BaselineDataValidationResult? GetLatestResult() {
        lock (_syncRoot) {
            return _latestResult;
        }
    }

    /// <summary>
    /// 写入最近一次校验结果。
    /// </summary>
    /// <param name="result">校验结果。</param>
    public void SetLatestResult(BaselineDataValidationResult? result) {
        lock (_syncRoot) {
            _latestResult = result;
        }
    }

    /// <summary>
    /// 校验必要配置节点。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    private void ValidateRequiredConfigurationKeys(List<string> errors) {
        foreach (var key in RequiredConfigurationKeys) {
            if (string.IsNullOrWhiteSpace(_configuration[key])) {
                errors.Add($"缺少必要配置节点：{key}。");
            }
        }
    }

    /// <summary>
    /// 校验 Provider 与连接字符串是否匹配。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    /// <returns>归一化后的 Provider 名称。</returns>
    private string ValidateProviderAndConnectionString(List<string> errors) {
        var provider = _configuration["Persistence:Provider"]?.Trim();
        if (string.IsNullOrWhiteSpace(provider)) {
            return "Unknown";
        }

        if (string.Equals(provider, ConfiguredProviderNames.MySql, StringComparison.OrdinalIgnoreCase)) {
            var connectionString = _configuration.GetConnectionString(ConfiguredProviderNames.MySql);
            if (string.IsNullOrWhiteSpace(connectionString)) {
                errors.Add($"Provider 为 {ConfiguredProviderNames.MySql} 时必须提供 ConnectionStrings:{ConfiguredProviderNames.MySql}。");
                return ConfiguredProviderNames.MySql;
            }

            try {
                var builder = new MySqlConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(builder.Database)) {
                    errors.Add("MySql 连接字符串必须包含 Database。");
                }
            }
            catch (Exception ex) {
                Logger.Error(ex, "MySQL 连接字符串解析失败。");
                errors.Add($"MySql 连接字符串格式非法：{ex.Message}");
            }

            return ConfiguredProviderNames.MySql;
        }

        if (string.Equals(provider, ConfiguredProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase)) {
            var connectionString = _configuration.GetConnectionString(ConfiguredProviderNames.SqlServer);
            if (string.IsNullOrWhiteSpace(connectionString)) {
                errors.Add($"Provider 为 {ConfiguredProviderNames.SqlServer} 时必须提供 ConnectionStrings:{ConfiguredProviderNames.SqlServer}。");
                return ConfiguredProviderNames.SqlServer;
            }

            try {
                var builder = new SqlConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(builder.InitialCatalog)) {
                    errors.Add("SqlServer 连接字符串必须包含 Database 或 Initial Catalog。");
                }
            }
            catch (Exception ex) {
                Logger.Error(ex, "SQL Server 连接字符串解析失败。");
                errors.Add($"SqlServer 连接字符串格式非法：{ex.Message}");
            }

            return ConfiguredProviderNames.SqlServer;
        }

        errors.Add($"不支持的 Persistence:Provider={provider}，仅允许 {ConfiguredProviderNames.MySql} / {ConfiguredProviderNames.SqlServer}。");
        return provider;
    }

    /// <summary>
    /// 校验本地时间配置不含时区后缀。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    private void ValidateLocalTimeConfigurations(List<string> errors) {
        foreach (var key in LocalTimeConfigurationKeys) {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            if (TimeZoneSuffixRegex().IsMatch(value.Trim())) {
                errors.Add($"配置项 {key} 仅允许本地时间语义，禁止使用 Z 或 offset。");
            }
        }
    }

    /// <summary>
    /// 校验分表起始时间是否合法。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    private void ValidateShardingStartTime(List<string> errors) {
        var value = _configuration["Persistence:Sharding:ParcelStartTime"];
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out _)) {
            errors.Add("配置项 Persistence:Sharding:ParcelStartTime 无法按本地时间语义解析。");
        }
    }

    /// <summary>
    /// 校验关键枚举 Description 是否完整。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    private static void ValidateEnumDescriptions(List<string> errors) {
        foreach (var enumType in KeyEnumTypes) {
            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                var descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute is null || string.IsNullOrWhiteSpace(descriptionAttribute.Description)) {
                    errors.Add($"关键枚举 {enumType.Name}.{field.Name} 缺少 Description。");
                }
            }
        }
    }

    /// <summary>
    /// 校验默认参考数据定义是否完整且无重复。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    private static void ValidateDefaultReferenceData(List<string> errors) {
        var definitions = BuildReferenceDataDefinitions();
        if (definitions.Count == 0) {
            errors.Add("默认参考数据定义为空，无法建立基线。");
            return;
        }

        var duplicateCodes = definitions
            .GroupBy(static definition => $"{definition.Catalog}:{definition.Code}", StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateCodes.Length > 0) {
            errors.Add($"默认参考数据存在重复编码：{string.Join("、", duplicateCodes)}。");
        }

        var duplicateValues = definitions
            .GroupBy(static definition => $"{definition.Catalog}:{definition.Value}", StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateValues.Length > 0) {
            errors.Add($"默认参考数据存在重复数值：{string.Join("、", duplicateValues)}。");
        }
    }

    /// <summary>
    /// 构建默认参考数据定义。
    /// </summary>
    /// <returns>参考数据定义集合。</returns>
    private static IReadOnlyList<(string Catalog, string Code, int Value)> BuildReferenceDataDefinitions() {
        var definitions = new List<(string Catalog, string Code, int Value)>();
        AppendEnumDefinitions<ArchiveTaskType>(ArchiveTaskTypeCatalogName, definitions);
        AppendEnumDefinitions<ArchiveTaskStatus>(ArchiveTaskStatusCatalogName, definitions);
        AppendEnumDefinitions<MigrationFailureMode>(FailureModeCatalogName, definitions);
        return definitions;
    }

    /// <summary>
    /// 将枚举值追加到参考数据定义集合。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <param name="catalog">目录名称。</param>
    /// <param name="definitions">目标集合。</param>
    private static void AppendEnumDefinitions<TEnum>(string catalog, List<(string Catalog, string Code, int Value)> definitions)
        where TEnum : struct, Enum {
        foreach (var enumValue in Enum.GetValues<TEnum>()) {
            definitions.Add((catalog, enumValue.ToString(), Convert.ToInt32(enumValue, CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// 生成结果摘要。
    /// </summary>
    /// <param name="errors">错误集合。</param>
    /// <param name="warnings">告警集合。</param>
    /// <param name="provider">Provider 名称。</param>
    /// <returns>摘要文本。</returns>
    private static string BuildSummary(IReadOnlyList<string> errors, IReadOnlyList<string> warnings, string provider) {
        if (errors.Count == 0) {
            return warnings.Count == 0
                ? $"基线数据校验通过，Provider={provider}。"
                : $"基线数据校验通过，但存在 {warnings.Count} 条提示，Provider={provider}。";
        }

        return $"基线数据校验失败，共 {errors.Count} 项，Provider={provider}。";
    }

    /// <summary>
    /// 时区后缀匹配正则。
    /// </summary>
    /// <returns>正则对象。</returns>
    [GeneratedRegex(@"(Z|[+\-]\d{2}:\d{2}|[+\-]\d{4})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeZoneSuffixRegex();
}
