using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>携带预期版本的接入设置保存请求。</summary>
public sealed record FusionSettingsWrite(int Revision, FusionSettings Settings);
