using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.SharedKernel.Utilities;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 日志清理服务 - 自动清理超过指定天数的日志文件
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
        /// 日志清理配置实例，包含开关、保留天数与检查间隔参数。
        /// </summary>
        private readonly LogCleanupSettings _settings;

        public LogCleanupService(
            SafeExecutor safeExecutor,
            IOptions<LogCleanupSettings> settings) {
            _safeExecutor = safeExecutor;
            _settings = settings.Value;
        }

        /// <summary>
        /// 执行逻辑：ExecuteAsync。
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            if (!_settings.Enabled) {
                Logger.Info("日志清理服务已禁用");
                return;
            }

            Logger.Info("日志清理服务已启动，保留天数: {RetentionDays}天，检查间隔: {CheckIntervalHours}小时",
                _settings.RetentionDays, _settings.CheckIntervalHours);

            // 首次启动时立即执行一次清理
            _safeExecutor.Execute(
                () => CleanupOldLogs(stoppingToken),
                "首次日志清理");

            while (!stoppingToken.IsCancellationRequested) {
                try {
                    await Task.Delay(TimeSpan.FromHours(_settings.CheckIntervalHours), stoppingToken);

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

        /// <summary>
        /// 执行逻辑：CleanupOldLogs。
        /// </summary>
        private void CleanupOldLogs(CancellationToken cancellationToken) {
            var logDirectory = ResolveLogDirectoryPath();

            if (!Directory.Exists(logDirectory)) {
                Logger.Warn("日志目录不存在: {LogDirectory}", logDirectory);
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-_settings.RetentionDays);
            Logger.Info("开始清理日志，删除 {CutoffDate} 之前的日志文件", cutoffDate);

            var deletedCount = 0;
            var failedCount = 0;

            // 使用目录栈递归扫描日志根目录及其所有子目录
            var (deleted, failed) = CleanupDirectoryRecursively(logDirectory, cutoffDate, cancellationToken);
            deletedCount += deleted;
            failedCount += failed;

            Logger.Info("日志清理完成，删除文件数: {DeletedCount}，失败数: {FailedCount}",
                deletedCount, failedCount);
        }

        /// <summary>
        /// 将日志目录配置解析为绝对路径。
        /// </summary>
        /// <returns>日志目录绝对路径。</returns>
        private string ResolveLogDirectoryPath() {
            if (Path.IsPathRooted(_settings.LogDirectory)) {
                return _settings.LogDirectory;
            }

            return Path.Combine(AppContext.BaseDirectory, _settings.LogDirectory);
        }

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
    }
}
