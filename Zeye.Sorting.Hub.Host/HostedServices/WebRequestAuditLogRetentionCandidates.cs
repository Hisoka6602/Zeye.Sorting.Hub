namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// WebRequestAuditLog 历史分表保留候选模型。
    /// 注意：此类型原为 DatabaseInitializerHostedService 内的 private 嵌套类型，
    /// 提取为独立文件后可见性提升为 internal，仅在宿主程序集内部使用。
    /// </summary>
    /// <param name="CandidateCount">候选总数。</param>
    /// <param name="CandidatePhysicalTableNames">候选物理表名清单。</param>
    internal readonly record struct WebRequestAuditLogRetentionCandidates(
        int CandidateCount,
        IReadOnlyList<string> CandidatePhysicalTableNames);
}
