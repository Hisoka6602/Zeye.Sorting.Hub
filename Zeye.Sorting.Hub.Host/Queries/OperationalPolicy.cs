namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>可在线维护的运维策略，其余数据库及部署参数保持由服务器配置管理。</summary>
public sealed record OperationalPolicy {
    /// <summary>用于防止覆盖并发修改的版本。</summary>
    public int Revision { get; init; }
    /// <summary>是否自动创建实际数据库备份。</summary>
    public bool AutomaticBackups { get; init; }
    /// <summary>自动备份间隔分钟数，范围为十至一千四百四十。</summary>
    public int BackupIntervalMinutes { get; init; } = 60;
    /// <summary>包裹分表预建窗口小时数，范围为一至一百六十八。</summary>
    public int PrebuildAheadHours { get; init; } = 72;
}
