using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>保持 Oracle 的空字符串及长文本行为与现有业务模型一致。</summary>
internal static class OracleModelCompatibility {
    /// <summary>Oracle 把空字符串存成 NULL；采用可逆前缀编码，保留空值与空字符串的区别。</summary>
    private static readonly ValueConverter<string, string> EmptyStringConverter = new(
        value => value.Length == 0 ? "\u0001" : value.StartsWith('\u0001') ? "\u0001" + value : value,
        value => value.StartsWith('\u0001') ? value.Substring(1) : value);

    /// <summary>为 Oracle 配置空字符串编码及无长度上限的正文列；索引列使用受支持的有界字符类型。</summary>
    internal static void Configure(ModelBuilder builder) {
        foreach (var entity in builder.Model.GetEntityTypes()) {
            foreach (var property in entity.GetProperties().Where(property => property.ClrType == typeof(string))) {
                property.SetValueConverter(EmptyStringConverter);
                if (property.GetMaxLength() is null) {
                    if (property.IsKey() || property.IsIndex() || property.IsForeignKey()) property.SetMaxLength(512);
                    else property.SetColumnType("NCLOB");
                }
            }
        }
    }
}
