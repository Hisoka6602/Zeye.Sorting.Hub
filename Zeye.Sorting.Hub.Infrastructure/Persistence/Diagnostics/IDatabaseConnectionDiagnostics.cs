namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// 数据库连接诊断服务抽象。
/// </summary>
public interface IDatabaseConnectionDiagnostics {
    /// <summary>
    /// 执行一次数据库连接探测，并刷新最近一次快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最近一次探测快照。</returns>
    Task<DatabaseConnectionHealthSnapshot> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 获取最近一次诊断快照。
    /// </summary>
    /// <returns>最近一次诊断快照；若尚未探测则返回 <see langword="null"/>。</returns>
    DatabaseConnectionHealthSnapshot? GetLatestSnapshot();
}
