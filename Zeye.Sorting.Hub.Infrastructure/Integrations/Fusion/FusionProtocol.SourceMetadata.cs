using System.Globalization;
using System.Text.Json;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>读取源端实际发布的DWS元数据，保持接收时间、设备时间和消息身份各自的来源。</summary>
public static partial class FusionProtocol {
    /// <summary>完整显式设备身份的摘要命名空间，避免超长身份截断合并。</summary>
    private const string ExplicitMessageIdentityPrefix = "explicit-sha256:";
    /// <summary>缺失设备身份时，明确接收事实引用使用的独立命名空间。</summary>
    private const string ReceivedFactIdentityPrefix = "received-fact:";
    /// <summary>顶层协议字段优先，兼容源端字典中保持原大小写的稳定元数据键。</summary>
    private static string? SourceText(JsonElement data, JsonElement metadata, string name, string metadataName, int limit = 256) =>
        Text(data, name, limit) ?? (metadata.ValueKind == JsonValueKind.Object ? Text(metadata, metadataName, limit) : null);

    /// <summary>规范绝对时间字段优先，源端本地文本保持墙钟值；带显式偏移的文本只在接入边界转换。</summary>
    private static DateTime? SourceTime(JsonElement data, JsonElement metadata, string protocolName, string localName, string metadataName, string zone) {
        if (Text(data, protocolName) is not null) return ProtocolTime(data, protocolName, zone);
        var value = SourceText(data, metadata, localName, metadataName);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (LocalDateTimeJsonConverter.TryParse(value, out var local)) return local;
        var explicitOffset = value.EndsWith('Z') || value.Length >= 6 && (value[^6] is '+' or '-') && value[^3] == ':';
        if (explicitOffset && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var absolute))
            return TimeZoneInfo.ConvertTime(absolute, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime;
        throw new ArgumentException("InvalidSourceTimestamp:" + localName);
    }

    /// <summary>只解析源端明确提供的布尔文本，不把帧边界可靠性等同于时间可靠性。</summary>
    private static bool? MetadataBoolean(JsonElement metadata, string name) => metadata.ValueKind == JsonValueKind.Object
        && bool.TryParse(Text(metadata, name), out var value) ? value : null;

    /// <summary>保持设备关联编号的Int64精度，空值和非正数不生成关联。</summary>
    private static long? MetadataLong(JsonElement metadata, string name) => metadata.ValueKind == JsonValueKind.Object
        && long.TryParse(Text(metadata, name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;

    /// <summary>设备消息身份优先；缺少设备身份时仅使用源端明确的接收事实引用，不按条码猜测。</summary>
    private static string? SourceMessageIdentity(HubFactBody fact, JsonElement metadata) {
        var explicitIdentity = FullText(fact.Data, "messageIdentity") ?? FullText(metadata, "MessageIdentity");
        if (!string.IsNullOrWhiteSpace(explicitIdentity)) {
            return explicitIdentity.Length > 256
                || explicitIdentity.StartsWith(ExplicitMessageIdentityPrefix, StringComparison.OrdinalIgnoreCase)
                || explicitIdentity.StartsWith(ReceivedFactIdentityPrefix, StringComparison.OrdinalIgnoreCase)
                    ? ExplicitMessageIdentityPrefix + Hash(explicitIdentity) : explicitIdentity;
        }
        var receivedRecordId = fact.Kind == "dws.received" ? fact.RecordId
            : fact.Kind == "dws.binding" ? Text(fact.Data, "receivedRecordId") : null;
        return IsHex(receivedRecordId, 32) ? ReceivedFactIdentityPrefix + Key(fact.SourceInstanceId, fact.JournalId, receivedRecordId!) : null;
    }

    /// <summary>身份参与关联前读取完整字符串；字段长度通过摘要规范化处理，不复用展示字段截断逻辑。</summary>
    private static string? FullText(JsonElement data, string name) => data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
