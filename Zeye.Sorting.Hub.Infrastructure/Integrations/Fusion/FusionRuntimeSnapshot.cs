namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>单个版本的配置与来源目录保持一致。</summary>
public sealed record FusionRuntimeSnapshot(FusionIngestionOptions Options,
    IReadOnlyDictionary<string, FusionSourceOptions> Sources, int Revision);
