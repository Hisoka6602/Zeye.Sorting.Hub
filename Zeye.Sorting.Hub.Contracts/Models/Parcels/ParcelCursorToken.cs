using NLog;
using System.Buffers.Binary;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 游标令牌。
/// </summary>
public sealed record ParcelCursorToken {
    /// <summary>
    /// 二进制游标载荷字节长度。
    /// </summary>
    private const int PayloadLength = 16;

    /// <summary>
    /// 无填充 Base64Url 游标长度。
    /// </summary>
    private const int EncodedLength = 22;
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
        Span<byte> payload = stackalloc byte[PayloadLength];
        BinaryPrimitives.WriteInt64LittleEndian(payload, LastScannedTimeLocal.Ticks);
        BinaryPrimitives.WriteInt64LittleEndian(payload[sizeof(long)..], LastId);
        var base64 = Convert.ToBase64String(payload);
        return string.Create(EncodedLength, base64, static (destination, source) => {
            for (var index = 0; index < destination.Length; index++) {
                destination[index] = source[index] switch {
                    '+' => '-',
                    '/' => '_',
                    var value => value
                };
            }
        });
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

        if (token.Length != EncodedLength) {
            return false;
        }

        try {
            Span<char> base64 = stackalloc char[24];
            for (var index = 0; index < token.Length; index++) {
                base64[index] = token[index] switch {
                    '-' => '+',
                    '_' => '/',
                    var value => value
                };
            }

            base64[22] = '=';
            base64[23] = '=';
            Span<byte> payload = stackalloc byte[PayloadLength];
            if (!Convert.TryFromBase64Chars(base64, payload, out var written) || written != PayloadLength) {
                return false;
            }

            var scannedTimeTicks = BinaryPrimitives.ReadInt64LittleEndian(payload);
            var lastId = BinaryPrimitives.ReadInt64LittleEndian(payload[sizeof(long)..]);
            if (lastId <= 0 || scannedTimeTicks <= 0 || scannedTimeTicks > DateTime.MaxValue.Ticks) {
                return false;
            }

            cursorToken = new ParcelCursorToken {
                LastScannedTimeLocal = new DateTime(scannedTimeTicks, DateTimeKind.Local),
                LastId = lastId
            };
            return true;
        }
        catch (Exception ex) {
            Logger.Debug(ex, "Parcel 游标令牌解析失败。TokenLength={TokenLength}", token.Length);
            return false;
        }
    }

}
