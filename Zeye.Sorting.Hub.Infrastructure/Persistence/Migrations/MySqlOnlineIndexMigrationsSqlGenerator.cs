using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Migrations;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

/// <summary>由 EF Core 生成分析覆盖索引，并要求 MySQL 在线创建，避免退化为阻塞写入的建表算法。</summary>
internal sealed class MySqlOnlineIndexMigrationsSqlGenerator : IMigrationsSqlGenerator {
    /// <summary>覆盖索引的基础名称，历史物理表名称在此名称后附加周期。</summary>
    private static readonly string[] DurationIndexNames = ["IX_Processing_Stage_PartitionTime_Duration", "IX_Processing_Source_Stage_Duration", "IX_Dws_Time_Stage_Success"];

    /// <summary>提供器负责完整迁移的预处理、转义、批处理和事务标记。</summary>
    private readonly MySqlMigrationsSqlGenerator _provider;
    /// <summary>构建替换命令所需的公开 EF Core 依赖。</summary>
    private readonly MigrationsSqlGeneratorDependencies _dependencies;

    /// <summary>通过依赖注入创建公开提供器生成器，内部构造参数由提供器自行注册。</summary>
    public MySqlOnlineIndexMigrationsSqlGenerator(IServiceProvider services, MigrationsSqlGeneratorDependencies dependencies) {
        _provider = ActivatorUtilities.CreateInstance<MySqlMigrationsSqlGenerator>(services);
        _dependencies = dependencies;
    }

    /// <summary>完整批次先交由提供器生成，仅替换精确匹配的独立目标索引命令。</summary>
    public IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations,
        IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default) {
        var commands = _provider.Generate(operations, model, options);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var terminator = _dependencies.SqlGenerationHelper.StatementTerminator;
        foreach (var operation in operations.OfType<CreateIndexOperation>().Where(RequiresOnlineCreation)) {
            var generated = _provider.Generate([operation], model, options);
            if (generated.Count != 1) throw new InvalidOperationException("提供器未生成独立索引命令，无法保证在线创建。");
            var original = generated[0].CommandText;
            var sql = original.TrimEnd();
            if (!sql.EndsWith(terminator, StringComparison.Ordinal)) throw new InvalidOperationException("索引命令缺少终止符，无法保证在线创建。");
            replacements[original] = sql[..^terminator.Length] + " ALGORITHM=INPLACE LOCK=NONE" + terminator + original[sql.Length..];
        }
        if (replacements.Count == 0) return commands;
        var result = new List<MigrationCommand>(commands.Count);
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in commands) {
            if (!replacements.TryGetValue(command.CommandText, out var onlineSql)) {
                result.Add(command);
                continue;
            }
            matched.Add(command.CommandText);
            var builder = new MigrationCommandListBuilder(_dependencies);
            builder.Append(onlineSql).EndCommand(command.TransactionSuppressed);
            result.AddRange(builder.GetCommandList());
        }
        if (matched.Count != replacements.Count) throw new InvalidOperationException("提供器改变了目标索引的批处理形式，已阻止退化为阻塞写入的创建操作。");
        return result;
    }

    /// <summary>仅普通分析覆盖索引要求在线创建，唯一、全文、空间和其他表索引沿用提供器行为。</summary>
    private static bool RequiresOnlineCreation(CreateIndexOperation operation) {
        var isDurationIndex = DurationIndexNames.Any(name => operation.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || operation.Name.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase));
        var isProcessingTable = operation.Table.Equals("Parcel_ProcessingRecords", StringComparison.OrdinalIgnoreCase)
            || operation.Table.StartsWith("Parcel_ProcessingRecords_", StringComparison.OrdinalIgnoreCase)
            || operation.Table.Equals("Parcel_DwsMeasurements", StringComparison.OrdinalIgnoreCase)
            || operation.Table.StartsWith("Parcel_DwsMeasurements_", StringComparison.OrdinalIgnoreCase);
        return isDurationIndex && isProcessingTable && !operation.IsUnique
            && operation["MySql:FullTextIndex"] is not true && operation["MySql:SpatialIndex"] is not true;
    }
}
