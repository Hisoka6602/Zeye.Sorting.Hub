namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// PerDay 治理组模型。
    /// </summary>
    /// <param name="GroupName">治理组名称。</param>
    /// <param name="BaseTableNames">治理组逻辑表名清单。</param>
    internal readonly record struct PerDayGovernanceGroup(
        string GroupName,
        IReadOnlyList<string> BaseTableNames);
}
