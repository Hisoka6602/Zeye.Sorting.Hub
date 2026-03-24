using System.Collections.Concurrent;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Web 请求审计日志仓储测试替身。
/// </summary>
public sealed class InMemoryWebRequestAuditLogRepository : IWebRequestAuditLogRepository {
    /// <summary>
    /// 已写入日志集合。
    /// </summary>
    private readonly ConcurrentQueue<WebRequestAuditLog> _logs = new();

    /// <summary>
    /// 写入次数。
    /// </summary>
    private int _writeCount;

    /// <summary>
    /// 仓储失败错误码。
    /// </summary>
    private string _failureCode = "TEST_REPOSITORY_FAIL";

    /// <summary>
    /// 仓储失败错误信息。
    /// </summary>
    private string _failureMessage = "测试仓储失败";

    /// <summary>
    /// 写入行为。
    /// </summary>
    public RepositoryBehavior Behavior { get; set; } = RepositoryBehavior.Success;

    /// <summary>
    /// 获取当前写入次数。
    /// </summary>
    public int WriteCount => _writeCount;

    /// <summary>
    /// 获取当前日志快照。
    /// </summary>
    public IReadOnlyCollection<WebRequestAuditLog> Logs => _logs.ToArray();

    /// <summary>
    /// 设置失败返回信息。
    /// </summary>
    /// <param name="failureMessage">失败信息。</param>
    /// <param name="failureCode">失败码。</param>
    public void ConfigureFailure(string failureMessage, string failureCode = "TEST_REPOSITORY_FAIL") {
        _failureMessage = string.IsNullOrWhiteSpace(failureMessage) ? "测试仓储失败" : failureMessage;
        _failureCode = string.IsNullOrWhiteSpace(failureCode) ? "TEST_REPOSITORY_FAIL" : failureCode;
    }

    /// <summary>
    /// 清理已记录日志。
    /// </summary>
    public void Reset() {
        while (_logs.TryDequeue(out _)) {
        }

        _writeCount = 0;
    }

    /// <summary>
    /// 写入 Web 请求审计日志。
    /// </summary>
    /// <param name="auditLog">审计日志聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public Task<RepositoryResult> AddAsync(WebRequestAuditLog auditLog, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _writeCount);

        if (Behavior == RepositoryBehavior.ThrowException) {
            throw new InvalidOperationException("测试仓储抛出异常");
        }

        if (Behavior == RepositoryBehavior.ReturnFailure) {
            return Task.FromResult(RepositoryResult.Fail(_failureMessage, _failureCode));
        }

        _logs.Enqueue(auditLog);
        return Task.FromResult(RepositoryResult.Success());
    }
}
