using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;

namespace Zeye.Sorting.Hub.Host.Logging;

/// <summary>保持普通日志过滤策略，同时确保框架故障与 SignalR 流异常能进入 NLog。</summary>
public sealed class ExceptionLoggingFilter : IPostConfigureOptions<LoggerFilterOptions> {
    /// <summary>NLog 日志提供器的完整类型名称。</summary>
    private static readonly string ProviderName = typeof(NLogLoggerProvider).FullName!;

    /// <summary>仅为 NLog 补充故障级别下限，配置热加载时同样执行。</summary>
    public void PostConfigure(string? name, LoggerFilterOptions options) {
        // 步骤 1：复制有效的全局和 NLog 规则；其他提供器保持原规则。
        var rules = options.Rules.Where(rule => rule.ProviderName is null
            || rule.ProviderName == ProviderName || rule.ProviderName == "NLog").ToArray();
        options.Rules.Add(new LoggerFilterRule(ProviderName, null,
            options.MinLevel > LogLevel.Warning ? LogLevel.Warning : options.MinLevel, null));
        foreach (var rule in rules) {
            var originalFilter = rule.Filter;
            var originalLevel = rule.LogLevel ?? options.MinLevel;
            options.Rules.Add(new LoggerFilterRule(ProviderName, rule.CategoryName,
                originalLevel > LogLevel.Warning ? LogLevel.Warning : originalLevel,
                originalFilter is null ? null : (provider, category, level) =>
                    level >= LogLevel.Warning || originalFilter(provider, category, level)));
        }
        // 步骤 2：框架在 Debug 记录流迭代失败；NLog 只落盘携带异常的 Debug 事件。
        options.Rules.Add(new LoggerFilterRule(ProviderName,
            "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher", LogLevel.Debug, null));
    }
}
