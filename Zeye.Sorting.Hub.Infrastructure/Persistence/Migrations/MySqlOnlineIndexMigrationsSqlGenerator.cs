using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure.Internal;
using Pomelo.EntityFrameworkCore.MySql.Migrations;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

/// <summary>由 EF Core 生成分析覆盖索引，并要求 MySQL 在线创建，避免退化为阻塞写入的建表算法。</summary>
internal sealed class MySqlOnlineIndexMigrationsSqlGenerator : MySqlMigrationsSqlGenerator {
    /// <summary>覆盖索引的基础名称，历史物理表名称在此名称后附加周期。</summary>
    private static readonly string[] DurationIndexNames = ["IX_Processing_Stage_PartitionTime_Duration", "IX_Processing_Source_Stage_Duration", "IX_Dws_Time_Stage_Success"];

    /// <summary>复用 Pomelo 的标识符转义、索引属性和命令事务处理。</summary>
    public MySqlOnlineIndexMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer,
        IMySqlOptions options) : base(dependencies, commandBatchPreparer, options) {
    }

    /// <summary>仅为普通耗时分析覆盖索引补充在线选项；服务器不支持时应报错，禁止静默改为锁表创建。</summary>
    protected override void Generate(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true) {
        var isDurationIndex = DurationIndexNames.Any(name => operation.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || operation.Name.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase));
        var isProcessingTable = operation.Table.Equals("Parcel_ProcessingRecords", StringComparison.OrdinalIgnoreCase)
            || operation.Table.StartsWith("Parcel_ProcessingRecords_", StringComparison.OrdinalIgnoreCase)
            || operation.Table.Equals("Parcel_DwsMeasurements", StringComparison.OrdinalIgnoreCase)
            || operation.Table.StartsWith("Parcel_DwsMeasurements_", StringComparison.OrdinalIgnoreCase);
        var requireOnline = isDurationIndex && isProcessingTable && !operation.IsUnique
            && operation["MySql:FullTextIndex"] is not true && operation["MySql:SpatialIndex"] is not true;
        base.Generate(operation, model, builder, requireOnline ? false : terminate);
        if (!requireOnline) return;
        builder.Append(" ALGORITHM=INPLACE LOCK=NONE");
        if (terminate) {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }
}
