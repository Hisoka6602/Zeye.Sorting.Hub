using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>统计真实关系数据库的目录和规则读取，验证热处理没有重复配置 I/O。</summary>
public sealed class ParcelMetadataIoInterceptor : DbCommandInterceptor {
    /// <summary>已观察的分表目录读取次数。</summary>
    private long _catalogReads;
    /// <summary>已观察的分类规则配置读取次数。</summary>
    private long _ruleReads;
    /// <summary>并发安全的目录读取计数。</summary>
    public long CatalogReads => Interlocked.Read(ref _catalogReads);
    /// <summary>并发安全的规则读取计数。</summary>
    public long RuleReads => Interlocked.Read(ref _ruleReads);

    /// <summary>同步 EF 查询也计入实际元数据读取。</summary>
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result) { Observe(command); return result; }

    /// <summary>异步 EF 查询计入实际元数据读取。</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        Observe(command); return ValueTask.FromResult(result);
    }

    /// <summary>只统计 SELECT，不将迁移和保存命令误认为配置读取。</summary>
    private void Observe(DbCommand command) {
        if (!command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return;
        if (command.CommandText.Contains("ParcelPartitionCatalog", StringComparison.Ordinal)) Interlocked.Increment(ref _catalogReads);
        if (command.CommandText.Contains("ManagedDocuments", StringComparison.Ordinal)
            && command.CommandText.Contains("rules-", StringComparison.Ordinal)) Interlocked.Increment(ref _ruleReads);
    }
}
