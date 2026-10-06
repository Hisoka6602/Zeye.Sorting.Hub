using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>携带预期版本的机器密钥轮换请求。</summary>
public sealed record FusionKeyRotation(int Revision);
