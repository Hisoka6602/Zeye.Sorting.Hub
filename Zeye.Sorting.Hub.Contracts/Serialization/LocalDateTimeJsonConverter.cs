using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Serialization;

/// <summary>本地时间JSON合同转换器，拒绝时区后缀并保持来源墙钟时间。</summary>
public sealed class LocalDateTimeJsonConverter : JsonConverter<DateTime> {
    /// <summary>允许的本地时间格式，精度最多七位小数。</summary>
    private static readonly string[] Formats = ["yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"];

    /// <summary>读取不包含时区信息的本地时间。</summary>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text) || !DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localTime))
            throw new JsonException("时间必须采用本地时间格式yyyy-MM-ddTHH:mm:ss，可附加毫秒，不允许时区后缀。");
        return localTime;
    }

    /// <summary>输出本地墙钟时间，不添加时区后缀。</summary>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture));
}
