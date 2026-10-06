using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>配对信息及保存后的目录版本。</summary>
public sealed record FusionPairingResult(int Revision, FusionPairing Pairing);
