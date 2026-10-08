using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>复制 EF 模型差异操作并重定位到实际分表，保留提供器注解和 SQLite 重建信息。</summary>
internal static class PartitionMigrationOperationRebaser {
    /// <summary>读取 EF 操作引用的业务表名。</summary>
    internal static string? TableName(MigrationOperation operation) => operation is CreateTableOperation or DropTableOperation or RenameTableOperation
        ? operation.GetType().GetProperty("Name")?.GetValue(operation) as string
        : operation.GetType().GetProperty("Table")?.GetValue(operation) as string;

    /// <summary>复制可变 EF 操作，重定位表、索引、键及外键，保留原始全局迁移。</summary>
    internal static MigrationOperation Rebase(MigrationOperation operation, string suffix, IReadOnlySet<string> tables, int maxLength, bool audit) {
        var copy = Clone(operation);
        Rewrite(copy, suffix, tables, maxLength, audit);
        return copy;
    }

    /// <summary>深复制嵌套迁移操作及 EF 注解。</summary>
    private static MigrationOperation Clone(MigrationOperation source) {
        var copy = (MigrationOperation)Activator.CreateInstance(source.GetType())!;
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
            if (property.GetIndexParameters().Length != 0) continue;
            var value = property.GetValue(source);
            if (property.CanWrite) property.SetValue(copy, value is MigrationOperation nested ? Clone(nested) : value);
            else if (value is IList list && property.GetValue(copy) is IList target) {
                foreach (var item in list) target.Add(item is MigrationOperation nested ? Clone(nested) : item);
            }
        }
        foreach (var annotation in source.GetAnnotations()) copy.AddAnnotation(annotation.Name, annotation.Value);
        return copy;
    }

    /// <summary>沿用 Code First 命名规则更新操作树，只重定位同组内的表引用。</summary>
    private static void Rewrite(MigrationOperation operation, string suffix, IReadOnlySet<string> tables, int maxLength, bool audit) {
        var table = TableName(operation);
        foreach (var property in operation.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
            if (property.GetIndexParameters().Length != 0) continue;
            var value = property.GetValue(operation);
            if (value is MigrationOperation nested) Rewrite(nested, suffix, tables, maxLength, audit);
            else if (value is IList list) foreach (var item in list.OfType<MigrationOperation>()) Rewrite(item, suffix, tables, maxLength, audit);
            if (!property.CanWrite || value is not string name) continue;
            if (property.Name is "Table" or "PrincipalTable" || ((property.Name is "Name" or "NewName") && operation is CreateTableOperation or DropTableOperation or RenameTableOperation)) {
                if (tables.Contains(name)) property.SetValue(operation, name + "_" + suffix);
            }
            else if ((property.Name is "Name" or "NewName") && operation is not ColumnOperation and not RenameColumnOperation && table is not null) {
                property.SetValue(operation, audit ? AuditPartitionMaintenanceService.PhysicalName(name, suffix)
                    : operation is CreateIndexOperation or DropIndexOperation or RenameIndexOperation
                        ? ParcelPartitionObjectNames.PhysicalName(name, table, suffix, maxLength)
                        : Uniquifier.Truncate(name.Replace(table, table + "_" + suffix, StringComparison.Ordinal), maxLength));
            }
        }
    }
}
