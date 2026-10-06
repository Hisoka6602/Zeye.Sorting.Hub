using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>接入目录版本及公开配置快照。</summary>
public sealed record FusionConfigurationView(int Revision, FusionSettings Settings, string HubId, string ImageDirectory, FusionSourceView[] Sources);
