using Zeye.Sorting.Hub.Host.Queries;
namespace Zeye.Sorting.Hub.Host.HostedServices;
/// <summary>在数据库初始化后同步内置账号，移除历史保留名冲突；空目录保留首次初始化流程。</summary>
public sealed class BuiltInAccountHostedService(IServiceScopeFactory scopes) : IHostedService {
    /// <summary>记录启动期账号同步失败。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken) {
        try {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AccessDirectoryService>().ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { Logger.Error(exception, "启动期账号目录同步失败；数据库恢复后会在下次账号访问时重新同步。"); }
    }
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
