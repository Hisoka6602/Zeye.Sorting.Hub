namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// WebRequestAuditLog 历史分表保留候选模型。
    /// </summary>
    /// <param name="CandidateCount">候选总数。</param>
    /// <param name="CandidatePhysicalTableNames">候选物理表名清单。</param>
    internal readonly record struct WebRequestAuditLogRetentionCandidates(
        int CandidateCount,
        IReadOnlyList<string> CandidatePhysicalTableNames);
}
