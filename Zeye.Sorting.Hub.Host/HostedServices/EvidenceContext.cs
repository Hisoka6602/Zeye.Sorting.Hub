namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 自动调优证据上下文。
/// </summary>
internal readonly record struct EvidenceContext(string EvidenceId, string CorrelationId);
