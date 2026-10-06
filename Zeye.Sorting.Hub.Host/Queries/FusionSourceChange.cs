using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>携带预期版本的工作台保存请求。</summary>
public sealed record FusionSourceChange(int Revision, FusionSourceWrite Source);
