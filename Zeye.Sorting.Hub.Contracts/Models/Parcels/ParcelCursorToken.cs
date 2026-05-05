using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 游标令牌。
/// </summary>
public sealed record ParcelCursorToken {
    /// <summary>
    /// 上一页最后一条记录的扫码时间（本地时间语义）。
    /// </summary>
    public required DateTime LastScannedTimeLocal { get; init; }

    /// <summary>
    /// 上一页最后一条记录的主键 Id。
    /// </summary>
    public required long LastId { get; init; }

    /// <summary>
    /// 将当前游标编码为可传输字符串。
    /// </summary>
    /// <returns>游标字符串。</returns>
    public string Encode() {
        var payload = new CursorPayload {
            LastScannedTimeTicks = LastScannedTimeLocal.Ticks,
            LastId = LastId
        };
        var payloadJson = JsonSerializer.Serialize(payload);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        return encoded
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// 解析游标字符串。
    /// </summary>
    /// <param name="token">游标字符串。</param>
    /// <param name="cursorToken">解析出的游标对象。</param>
    /// <returns>是否解析成功。</returns>
    public static bool TryDecode(string? token, out ParcelCursorToken? cursorToken) {
        cursorToken = null;
        if (string.IsNullOrWhiteSpace(token)) {
            return true;
        }

        try {
            var paddedToken = PadBase64(token);
            var base64 = paddedToken
                .Replace('-', '+')
                .Replace('_', '/');
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var payload = JsonSerializer.Deserialize<CursorPayload>(payloadJson);
            if (payload is null || payload.LastId <= 0 || payload.LastScannedTimeTicks <= 0) {
                return false;
            }

            cursorToken = new ParcelCursorToken {
                LastScannedTimeLocal = new DateTime(payload.LastScannedTimeTicks, DateTimeKind.Local),
                LastId = payload.LastId
            };
            return true;
        }
        catch (Exception) {
            return false;
        }
    }

    /// <summary>
    /// 补齐 Base64 字符串尾部填充。
    /// </summary>
    /// <param name="token">原始游标字符串。</param>
    /// <returns>补齐后的 Base64 字符串。</returns>
    private static string PadBase64(string token) {
        var remainder = token.Length % 4;
        if (remainder == 0) {
            return token;
        }

        return token + new string('=', 4 - remainder);
    }

    /// <summary>
    /// 游标序列化载荷。
    /// </summary>
    private sealed record CursorPayload {
        /// <summary>
        /// 最后一条记录扫码时间 ticks。
        /// </summary>
        public long LastScannedTimeTicks { get; init; }

        /// <summary>
        /// 最后一条记录 Id。
        /// </summary>
        public long LastId { get; init; }
    }
}
