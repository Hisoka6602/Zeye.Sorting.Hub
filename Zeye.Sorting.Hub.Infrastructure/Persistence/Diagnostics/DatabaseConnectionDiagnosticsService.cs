using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// 数据库连接诊断服务。
/// </summary>
public sealed class DatabaseConnectionDiagnosticsService : IDatabaseConnectionDiagnostics {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 线程同步锁。
    /// </summary>
    private readonly object _syncLock = new();

    /// <summary>
    /// 短生命周期 DbContext 工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _dbContextFactory;

    /// <summary>
    /// 诊断配置。
    /// </summary>
    private readonly DatabaseConnectionDiagnosticsOptions _options;

    /// <summary>
    /// 最近一次快照。
    /// </summary>
    private DatabaseConnectionHealthSnapshot? _latestSnapshot;

    /// <summary>
    /// 连续失败次数。
    /// </summary>
    private int _consecutiveFailureCount;

    /// <summary>
    /// 连续成功次数。
    /// </summary>
    private int _consecutiveSuccessCount;

    /// <summary>
    /// 是否处于恢复观察期。
    /// </summary>
    private bool _isRecoveryPending;

    /// <summary>
    /// 初始化 <see cref="DatabaseConnectionDiagnosticsService"/>。
    /// </summary>
    /// <param name="dbContextFactory">短生命周期 DbContext 工厂。</param>
    /// <param name="options">诊断配置。</param>
    public DatabaseConnectionDiagnosticsService(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        IOptions<DatabaseConnectionDiagnosticsOptions> options) {
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<DatabaseConnectionHealthSnapshot> ProbeAsync(CancellationToken cancellationToken) {
        var probeTimestamp = Stopwatch.GetTimestamp();
        using var timeoutTokenSource = CreateTimeoutTokenSource(cancellationToken);

        try {
            // 步骤 1：创建短生命周期上下文，避免把诊断连接与业务请求上下文耦合。
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(timeoutTokenSource.Token);
            var provider = dbContext.Database.ProviderName ?? "Unknown";
            var database = ResolveDatabaseName(dbContext);

            // 步骤 2：执行最小连接探测，仅确认数据库可连通，不承载业务查询副作用。
            var canConnect = await dbContext.Database.CanConnectAsync(timeoutTokenSource.Token);
            var elapsedMilliseconds = GetElapsedMilliseconds(probeTimestamp);
            if (!canConnect) {
                return RecordFailureSnapshot(provider, database, elapsedMilliseconds, "数据库连接不可用。");
            }

            return RecordSuccessSnapshot(provider, database, elapsedMilliseconds);
        }
        catch (Exception ex) {
            var elapsedMilliseconds = GetElapsedMilliseconds(probeTimestamp);
            Logger.Error(ex, "数据库连接诊断探测失败");
            return RecordFailureSnapshot("Unknown", "Unknown", elapsedMilliseconds, ex.Message);
        }
    }

    /// <inheritdoc />
    public DatabaseConnectionHealthSnapshot? GetLatestSnapshot() {
        lock (_syncLock) {
            return _latestSnapshot;
        }
    }

    /// <summary>
    /// 创建带超时的取消令牌源。
    /// </summary>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>带超时的取消令牌源。</returns>
    private CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken) {
        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        tokenSource.CancelAfter(_options.ProbeTimeoutMilliseconds);
        return tokenSource;
    }

    /// <summary>
    /// 记录成功快照。
    /// </summary>
    /// <param name="provider">数据库提供器。</param>
    /// <param name="database">数据库名称。</param>
    /// <param name="elapsedMilliseconds">探测耗时。</param>
    /// <returns>成功快照。</returns>
    private DatabaseConnectionHealthSnapshot RecordSuccessSnapshot(string provider, string database, long elapsedMilliseconds) {
        lock (_syncLock) {
            _consecutiveFailureCount = 0;
            _consecutiveSuccessCount++;
            if (_isRecoveryPending && _consecutiveSuccessCount >= _options.RecoveryThreshold) {
                _isRecoveryPending = false;
            }

            _latestSnapshot = new DatabaseConnectionHealthSnapshot {
                Provider = provider,
                Database = database,
                CheckedAtLocal = DateTime.Now,
                ElapsedMilliseconds = elapsedMilliseconds,
                ConsecutiveFailureCount = _consecutiveFailureCount,
                ConsecutiveSuccessCount = _consecutiveSuccessCount,
                IsProbeSucceeded = true,
                IsRecoveryPending = _isRecoveryPending,
                FailureMessage = null
            };
            return _latestSnapshot;
        }
    }

    /// <summary>
    /// 记录失败快照。
    /// </summary>
    /// <param name="provider">数据库提供器。</param>
    /// <param name="database">数据库名称。</param>
    /// <param name="elapsedMilliseconds">探测耗时。</param>
    /// <param name="failureMessage">失败信息。</param>
    /// <returns>失败快照。</returns>
    private DatabaseConnectionHealthSnapshot RecordFailureSnapshot(string provider, string database, long elapsedMilliseconds, string failureMessage) {
        lock (_syncLock) {
            _consecutiveFailureCount++;
            _consecutiveSuccessCount = 0;
            if (_consecutiveFailureCount >= _options.FailureThreshold) {
                _isRecoveryPending = true;
            }

            _latestSnapshot = new DatabaseConnectionHealthSnapshot {
                Provider = provider,
                Database = database,
                CheckedAtLocal = DateTime.Now,
                ElapsedMilliseconds = elapsedMilliseconds,
                ConsecutiveFailureCount = _consecutiveFailureCount,
                ConsecutiveSuccessCount = _consecutiveSuccessCount,
                IsProbeSucceeded = false,
                IsRecoveryPending = _isRecoveryPending,
                FailureMessage = failureMessage
            };
            return _latestSnapshot;
        }
    }

    /// <summary>
    /// 解析数据库名称。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <returns>数据库名称。</returns>
    private static string ResolveDatabaseName(SortingHubDbContext dbContext) {
        try {
            var connection = dbContext.Database.GetDbConnection();
            if (!string.IsNullOrWhiteSpace(connection.Database)) {
                return connection.Database;
            }
        }
        catch (Exception ex) {
            Logger.Debug(ex, "解析数据库名称失败，回退到 Provider 名称。Provider={Provider}", dbContext.Database.ProviderName);
            // 非关系型提供器不暴露 DbConnection 时回退到 Provider 名称。
        }

        return dbContext.Database.ProviderName ?? "Unknown";
    }

    /// <summary>
    /// 将高精度时间戳转换为毫秒。
    /// </summary>
    /// <param name="probeTimestamp">探测开始时间戳。</param>
    /// <returns>毫秒耗时。</returns>
    private static long GetElapsedMilliseconds(long probeTimestamp) {
        var elapsed = Stopwatch.GetElapsedTime(probeTimestamp);
        return (long)elapsed.TotalMilliseconds;
    }
}
