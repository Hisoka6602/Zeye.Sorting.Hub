using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.SharedKernel.Utilities;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 日志清理服务 - 自动清理超过指定天数的日志文件，支持配置热加载与可观测性指标输出。
    /// <para>
    /// 配置热加载行为：使用 <see cref="IOptionsMonitor{T}"/>，配置文件变更后，
    /// 下次执行 <see cref="ExecuteAsync"/> 循环时自动读取 <see cref="Settings"/> 属性获取最新配置，
    /// 无需手动重启服务。变更事件同步输出审计日志（含前后值对比）并记录到历史快照存储器。
    /// </para>
    /// </summary>
    public class LogCleanupService : BackgroundService {
        /// <summary>
        /// NLog 静态日志器实例，用于输出日志清理服务执行状态。
        /// </summary>
        private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// 安全执行器实例，用于隔离并捕获日志清理过程中的异常。
        /// </summary>
        private readonly SafeExecutor _safeExecutor;
        /// <summary>
        /// 配置热加载监视器，支持运行时配置变更自动生效。
        /// </summary>
        private readonly IOptionsMonitor<LogCleanupSettings> _settingsMonitor;
        /// <summary>
        /// 自动调优可观测输出器，用于输出清理指标（删除数/失败数）。
        /// </summary>
        private readonly IAutoTuningObservability _observability;
        /// <summary>
        /// 配置变更历史存储器，保留最近 N 次配置快照，用于前后值审计与回滚查询。
        /// </summary>
        private readonly ConfigChangeHistoryStore<LogCleanupSettings> _changeHistory;
        /// <summary>
        /// 上一次生效的配置快照，用于变更时的前后值对比审计。
        /// </summary>
        private LogCleanupSettings? _previousSettings;

        /// <summary>
        /// 初始化 <see cref="LogCleanupService"/>。
        /// </summary>
        /// <param name="safeExecutor">安全执行器。</param>
        /// <param name="settingsMonitor">配置热加载监视器。</param>
        /// <param name="observability">可观测性指标输出器。</param>
        /// <param name="changeHistory">配置变更历史存储器。</param>
        public LogCleanupService(
            SafeExecutor safeExecutor,
            IOptionsMonitor<LogCleanupSettings> settingsMonitor,
            IAutoTuningObservability observability,
            ConfigChangeHistoryStore<LogCleanupSettings> changeHistory) {
            _safeExecutor = safeExecutor;
            _settingsMonitor = settingsMonitor;
            _observability = observability;
            _changeHistory = changeHistory;

            // 记录初始配置快照
            _previousSettings = settingsMonitor.CurrentValue;

            // 配置热加载：当配置变更时记录审计日志（含前后值对比）
            _settingsMonitor.OnChange(OnSettingsChanged);
        }

        /// <summary>
        /// 当前生效配置（支持热加载，每次读取获取最新值）。
        /// </summary>
        private LogCleanupSettings Settings => _settingsMonitor.CurrentValue;

        /// <summary>后台服务主循环：按设定间隔周期性执行日志清理。</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            if (!Settings.Enabled) {
                Logger.Info("日志清理服务已禁用");
                return;
            }

            Logger.Info("日志清理服务已启动，保留天数: {RetentionDays}天，检查间隔: {CheckIntervalHours}小时",
                Settings.RetentionDays, Settings.CheckIntervalHours);

            // 首次启动时立即执行一次清理
            _safeExecutor.Execute(
                () => CleanupOldLogs(stoppingToken),
                "首次日志清理");

            while (!stoppingToken.IsCancellationRequested) {
                try {
                    await Task.Delay(TimeSpan.FromHours(Settings.CheckIntervalHours), stoppingToken);

                    _safeExecutor.Execute(
                        () => CleanupOldLogs(stoppingToken),
                        "定期日志清理");
                }
                catch (OperationCanceledException) {
                    // 服务正在停止，正常退出
                    Logger.Info("日志清理服务正在停止，日志目录: {LogDirectory}", ResolveLogDirectoryPath());
                    break;
                }
            }
        }

        /// <summary>扫描日志目录，删除超过保留天数的旧日志文件。</summary>
        internal void CleanupOldLogs(CancellationToken cancellationToken) {
            var settings = Settings;
            var logDirectory = ResolveLogDirectoryPath(settings);

            if (!Directory.Exists(logDirectory)) {
                Logger.Warn("日志目录不存在: {LogDirectory}", logDirectory);
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-settings.RetentionDays);
            Logger.Info("开始清理日志，删除 {CutoffDate} 之前的日志文件", cutoffDate);

            var deletedCount = 0;
            var failedCount = 0;

            // 使用目录栈递归扫描日志根目录及其所有子目录
            var (deleted, failed) = CleanupDirectoryRecursively(logDirectory, cutoffDate, cancellationToken);
            deletedCount += deleted;
            failedCount += failed;

            Logger.Info("日志清理完成，删除文件数: {DeletedCount}，失败数: {FailedCount}",
                deletedCount, failedCount);

            // 输出可观测性指标（失败计数与删除计数）
            _observability.EmitMetric("log.cleanup.deleted_files", deletedCount);
            _observability.EmitMetric("log.cleanup.failed_files", failedCount);
        }

        /// <summary>
        /// 将日志目录配置解析为绝对路径。
        /// </summary>
        /// <param name="settings">当前生效的日志清理配置。</param>
        /// <returns>日志目录绝对路径。</returns>
        private static string ResolveLogDirectoryPath(LogCleanupSettings settings) {
            if (Path.IsPathRooted(settings.LogDirectory)) {
                return settings.LogDirectory;
            }

            return Path.Combine(AppContext.BaseDirectory, settings.LogDirectory);
        }

        /// <summary>
        /// 将日志目录配置解析为绝对路径（使用当前热加载配置）。
        /// </summary>
        /// <returns>日志目录绝对路径。</returns>
        private string ResolveLogDirectoryPath() => ResolveLogDirectoryPath(Settings);

        /// <summary>
        /// 使用目录栈递归扫描目录树并清理过期日志。
        /// </summary>
        /// <param name="rootDirectory">日志根目录。</param>
        /// <param name="cutoffDate">清理截止时间。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>删除/失败统计。</returns>
        private (int DeletedCount, int FailedCount) CleanupDirectoryRecursively(string rootDirectory, DateTime cutoffDate, CancellationToken cancellationToken) {
            var deletedCount = 0;
            var failedCount = 0;
            var directoryStack = new Stack<string>();
            directoryStack.Push(rootDirectory);

            while (directoryStack.Count > 0) {
                var currentDirectory = directoryStack.Pop();
                if (cancellationToken.IsCancellationRequested) {
                    Logger.Info("日志清理扫描已取消，当前位置: {CurrentDirectory}", currentDirectory);
                    break;
                }

                try {
                    foreach (var file in Directory.EnumerateFiles(currentDirectory, "*.log", SearchOption.TopDirectoryOnly)) {
                        try {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTime < cutoffDate) {
                                Logger.Info("删除旧日志文件: {FileName}, 所在目录: {Directory}, 最后修改时间: {LastWriteTime}",
                                    fileInfo.Name, currentDirectory, fileInfo.LastWriteTime);

                                fileInfo.Delete();
                                deletedCount++;
                            }
                        }
                        catch (Exception ex) {
                            Logger.Warn(ex, "删除日志文件失败: {FilePath}, 所在目录: {Directory}", file, currentDirectory);
                            failedCount++;
                        }
                    }

                    foreach (var subDirectory in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly)) {
                        directoryStack.Push(subDirectory);
                    }
                }
                catch (Exception ex) {
                    Logger.Error(ex, "扫描日志目录失败: {CurrentDirectory}", currentDirectory);
                    failedCount++;
                }
            }

            return (deletedCount, failedCount);
        }

        /// <summary>
        /// 配置变更回调：热加载生效时输出前后值对比审计日志并记录快照历史。
        /// </summary>
        /// <param name="newSettings">新配置值。</param>
        /// <param name="name">配置名称（IOptionsMonitor 名称，通常为 null）。</param>
        private void OnSettingsChanged(LogCleanupSettings newSettings, string? name) {
            var prev = _previousSettings;
            var changedFields = BuildChangedFieldsSummary(prev, newSettings);

            Logger.Info(
                "日志清理配置已热加载更新（变更审计）：EffectiveTime={EffectiveTime}, ChangedFields=[{ChangedFields}], " +
                "Before=[Enabled={PrevEnabled}, RetentionDays={PrevRetentionDays}, CheckIntervalHours={PrevCheckIntervalHours}, LogDirectory={PrevLogDirectory}], " +
                "After=[Enabled={Enabled}, RetentionDays={RetentionDays}, CheckIntervalHours={CheckIntervalHours}, LogDirectory={LogDirectory}]",
                DateTime.Now,
                changedFields,
                prev?.Enabled,
                prev?.RetentionDays,
                prev?.CheckIntervalHours,
                prev?.LogDirectory,
                newSettings.Enabled,
                newSettings.RetentionDays,
                newSettings.CheckIntervalHours,
                newSettings.LogDirectory);

            // 记录到历史存储器（传入 record with {} 拷贝，确保历史条目为真正不可变快照）
            _changeHistory.Record(
                prev is null ? null : prev with { },
                newSettings with { },
                changedFields);

            // 更新前值快照（保存当前值副本，供下次变更对比使用）
            _previousSettings = newSettings with { };
        }

        /// <summary>
        /// 构建配置变更字段摘要，列出发生变化的字段及其前后值。
        /// </summary>
        /// <param name="prev">变更前配置（为 null 表示首次初始化）。</param>
        /// <param name="curr">变更后配置。</param>
        /// <returns>变更字段摘要字符串，如 "RetentionDays: 2→5; LogDirectory: logs→/var/logs"。</returns>
        private static string BuildChangedFieldsSummary(LogCleanupSettings? prev, LogCleanupSettings curr) {
            if (prev is null) {
                return "(首次初始化)";
            }

            var parts = new List<string>(4);
            if (prev.Enabled != curr.Enabled) {
                parts.Add($"Enabled: {prev.Enabled}→{curr.Enabled}");
            }
            if (prev.RetentionDays != curr.RetentionDays) {
                parts.Add($"RetentionDays: {prev.RetentionDays}→{curr.RetentionDays}");
            }
            if (prev.CheckIntervalHours != curr.CheckIntervalHours) {
                parts.Add($"CheckIntervalHours: {prev.CheckIntervalHours}→{curr.CheckIntervalHours}");
            }
            if (!string.Equals(prev.LogDirectory, curr.LogDirectory, StringComparison.Ordinal)) {
                parts.Add($"LogDirectory: {prev.LogDirectory}→{curr.LogDirectory}");
            }

            return parts.Count > 0 ? string.Join("; ", parts) : "(无字段变更)";
        }
    }
}
