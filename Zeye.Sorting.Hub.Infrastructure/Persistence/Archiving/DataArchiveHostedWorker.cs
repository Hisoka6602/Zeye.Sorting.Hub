using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;

/// <summary>
/// 数据归档后台 Worker。
/// </summary>
public sealed class DataArchiveHostedWorker {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档任务仓储。
    /// </summary>
    private readonly IArchiveTaskRepository _archiveTaskRepository;

    /// <summary>
    /// 归档执行器。
    /// </summary>
    private readonly DataArchiveExecutor _dataArchiveExecutor;

    /// <summary>
    /// 归档配置。
    /// </summary>
    private readonly DataArchiveOptions _options;

    /// <summary>
    /// 初始化归档后台 Worker。
    /// </summary>
    /// <param name="archiveTaskRepository">归档任务仓储。</param>
    /// <param name="dataArchiveExecutor">归档执行器。</param>
    /// <param name="options">归档配置。</param>
    public DataArchiveHostedWorker(
        IArchiveTaskRepository archiveTaskRepository,
        DataArchiveExecutor dataArchiveExecutor,
        IOptions<DataArchiveOptions> options) {
        _archiveTaskRepository = archiveTaskRepository ?? throw new ArgumentNullException(nameof(archiveTaskRepository));
        _dataArchiveExecutor = dataArchiveExecutor ?? throw new ArgumentNullException(nameof(dataArchiveExecutor));
        _options = options.Value;
    }

    /// <summary>
    /// 获取下一次轮询延迟。
    /// </summary>
    /// <returns>轮询延迟。</returns>
    public TimeSpan GetPollDelay() {
        return TimeSpan.FromSeconds(_options.WorkerPollIntervalSeconds);
    }

    /// <summary>
    /// 执行一次归档 Worker 轮询。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否处理了任务。</returns>
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken) {
        if (!_options.IsEnabled) {
            return false;
        }

        var nextTask = await _archiveTaskRepository.GetNextPendingAsync(cancellationToken);
        if (nextTask is null) {
            return false;
        }

        Logger.Info("归档 Worker 获取到待执行任务，TaskId={TaskId}, TaskType={TaskType}", nextTask.Id, nextTask.TaskType);
        await _dataArchiveExecutor.ExecuteAsync(nextTask.Id, cancellationToken);
        return true;
    }
}
