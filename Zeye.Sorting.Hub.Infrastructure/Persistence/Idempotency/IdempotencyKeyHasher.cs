using System.Security.Cryptography;
using System.Buffers;
using System.Text.Json;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Idempotency;

/// <summary>
/// 幂等键载荷哈希计算器。
/// </summary>
public sealed class IdempotencyKeyHasher {
    /// <summary>
    /// JSON 序列化配置。
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 计算载荷的 SHA256 十六进制哈希。
    /// </summary>
    /// <param name="payload">待计算的载荷对象。</param>
    /// <returns>SHA256 十六进制字符串。</returns>
    public string ComputeHash(object payload) {
        ArgumentNullException.ThrowIfNull(payload);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) {
            JsonSerializer.Serialize(writer, payload, payload.GetType(), SerializerOptions);
        }

        Span<byte> hashBytes = stackalloc byte[32];
        SHA256.HashData(buffer.WrittenSpan, hashBytes);
        return Convert.ToHexString(hashBytes);
    }
}
