namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// 慢查询采样记录
    /// </summary>
    /// <param name="CommandText">SQL 文本。</param>
    /// <param name="ElapsedMilliseconds">执行耗时（毫秒）。</param>
    /// <param name="OccurredTime">发生时间（本地时间语义）。</param>
    public sealed record SlowQuerySample(
        string CommandText,
        double ElapsedMilliseconds,
        DateTime OccurredTime);
}
