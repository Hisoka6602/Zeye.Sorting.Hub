using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>使显式命名与约定命名的分表对象都具有独立名称，避免 schema 内索引重名。</summary>
internal static class ParcelPartitionObjectNames {
    /// <summary>包含原表名时替换表名，否则追加分表后缀，并遵守提供器标识符长度限制。</summary>
    internal static string PhysicalName(string name, string table, string suffix, int maxLength) {
        var physical = name.Contains(table, StringComparison.Ordinal)
            ? name.Replace(table, table + "_" + suffix, StringComparison.Ordinal)
            : name + "_" + suffix;
        return Uniquifier.Truncate(physical, maxLength);
    }
}
