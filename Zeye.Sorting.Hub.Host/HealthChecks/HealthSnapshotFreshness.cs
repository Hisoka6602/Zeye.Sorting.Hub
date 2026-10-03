namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>统一判定后台治理快照是否仍有效，过期或明显超前的结果不能继续报告健康。</summary>
internal static class HealthSnapshotFreshness {
    /// <summary>按本地时间检查快照年龄，允许五分钟的短暂校时偏差。</summary>
    public static bool IsStale(DateTime recordedAtLocal, TimeSpan maximumAge, TimeProvider timeProvider) {
        var age = timeProvider.GetLocalNow().DateTime - recordedAtLocal;
        return age > maximumAge || age < -TimeSpan.FromMinutes(5);
    }
}
