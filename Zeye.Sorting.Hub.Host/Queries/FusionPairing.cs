using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>含独立密钥的一次性跨项目配对载荷。</summary>
public sealed record FusionPairing(string Format, string ProtocolVersion, string HubId, string SourceInstanceId, string MachineApiKey,
    string LineId, string TimeZoneId, string SiteCode, string DeviceCode, string Endpoint, bool AllowInsecureHttp, int DiscoveryPort);
