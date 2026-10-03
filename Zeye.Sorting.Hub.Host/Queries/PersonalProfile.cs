using System.Buffers.Binary;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>每个用户独立保存的个人资料，头像不进入账号目录或认证 Cookie。</summary>
public sealed record PersonalProfile {
    /// <summary>邮箱。</summary>
    public string Email { get; init; } = string.Empty;
    /// <summary>手机或联系电话。</summary>
    public string Phone { get; init; } = string.Empty;
    /// <summary>个人简介。</summary>
    public string Bio { get; init; } = string.Empty;
    /// <summary>已校验且不超过 96 KiB 的 PNG 或 JPEG 头像。</summary>
    public string AvatarDataUrl { get; init; } = string.Empty;
    /// <summary>个人资料文档键。</summary>
    public static string Key(string userId) => $"access-profile:{userId}";
    /// <summary>个人头像通过当前登录身份读取，并按资料版本刷新。</summary>
    public string? AvatarUrl(int revision) => AvatarDataUrl.Length == 0 ? null : $"/api/access/profile/avatar?v={revision}";
    /// <summary>只允许本地图片数据，拒绝外部地址、SVG、过大图片及错误格式。</summary>
    public static bool TryDecodeAvatar(string value, out byte[] bytes, out string contentType) {
        bytes = []; contentType = string.Empty;
        if (value.Length is 0 or > 131104) return false;
        var png = value.StartsWith("data:image/png;base64,", StringComparison.Ordinal);
        var jpeg = value.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal);
        if (!png && !jpeg) return false;
        try { bytes = Convert.FromBase64String(value[(value.IndexOf(',') + 1)..]); }
        catch (FormatException) { return false; }
        if (bytes.Length is < 24 or > 98304) return false;
        if (png) {
            if (!bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
                || !bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8)) return false;
            var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (width is 0 or > 1024 || height is 0 or > 1024) return false;
            contentType = "image/png"; return true;
        }
        if (bytes[0] != 255 || bytes[1] != 216 || bytes[^2] != 255 || bytes[^1] != 217) return false;
        // 读取 JPEG 帧尺寸，不在服务器解压不受信任的图片。
        for (var offset = 2; offset + 4 <= bytes.Length;) {
            if (bytes[offset++] != 255) return false;
            while (offset < bytes.Length && bytes[offset] == 255) offset++;
            if (offset >= bytes.Length) return false;
            var marker = bytes[offset++];
            if (marker is 216 or 217 or 1 || marker is >= 208 and <= 215) continue;
            if (offset + 2 > bytes.Length) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (length < 2 || offset + length > bytes.Length) return false;
            if (marker is 192 or 193 or 194) {
                if (length < 8) return false;
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2));
                if (width is 0 or > 1024 || height is 0 or > 1024) return false;
                contentType = "image/jpeg"; return true;
            }
            if (marker == 218) return false;
            offset += length;
        }
        return false;
    }
}
