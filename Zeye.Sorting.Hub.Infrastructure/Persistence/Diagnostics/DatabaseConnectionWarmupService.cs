using Microsoft.Extensions.Options;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// 数据库连接预热服务。
/// </summary>
public sealed class DatabaseConnectionWarmupService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据库连接诊断服务。
    /// </summary>
    private readonly IDatabaseConnectionDiagnostics _databaseConnectionDiagnostics;

    /// <summary>
    /// 诊断配置。
    /// </summary>
    private readonly DatabaseConnectionDiagnosticsOptions _options;

    /// <summary>
    /// 初始化 <see cref="DatabaseConnectionWarmupService"/>。
    /// </summary>
    /// <param name="databaseConnectionDiagnostics">数据库连接诊断服务。</param>
    /// <param name="options">诊断配置。</param>
    public DatabaseConnectionWarmupService(
        IDatabaseConnectionDiagnostics databaseConnectionDiagnostics,
        IOptions<DatabaseConnectionDiagnosticsOptions> options) {
        _databaseConnectionDiagnostics = databaseConnectionDiagnostics;
        _options = options.Value;
    }

    /// <summary>
    /// 执行数据库连接预热。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task WarmupAsync(CancellationToken cancellationToken) {
        if (!_options.IsWarmupEnabled) {
            Logger.Info("数据库连接预热已禁用，跳过启动期预热");
            return;
        }

        // 步骤 1：按配置预热多个短生命周期连接，避免首次真实请求承担全部建连成本。
        var warmupTasks = new List<Task<DatabaseConnectionHealthSnapshot>>(_options.WarmupConnectionCount);
        for (var index = 0; index < _options.WarmupConnectionCount; index++) {
            warmupTasks.Add(_databaseConnectionDiagnostics.ProbeAsync(cancellationToken));
        }

        var snapshots = await Task.WhenAll(warmupTasks);
        var successCount = snapshots.Count(snapshot => snapshot.IsProbeSucceeded);
        Logger.Info(
            "数据库连接预热完成，计划预热 {WarmupConnectionCount} 个连接，成功 {SuccessCount} 个，失败 {FailureCount} 个",
            _options.WarmupConnectionCount,
            successCount,
            snapshots.Length - successCount);
    }
}
