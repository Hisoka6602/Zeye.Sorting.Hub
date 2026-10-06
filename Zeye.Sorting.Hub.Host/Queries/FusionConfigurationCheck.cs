using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>身份检查结果和不匹配字段。</summary>
public sealed record FusionConfigurationCheck(bool Matched, string HubId, string SourceInstanceId, string[] Mismatches);
