using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Performance.ParcelAnalyticsBenchmark;

/// <summary>捕获报表实际执行的参数化SQL，以便在同一隔离库中执行执行计划。</summary>
internal sealed class AnalyticsSqlCaptureInterceptor : DbCommandInterceptor {
    /// <summary>捕获到的只读查询及参数。</summary>
    private readonly List<(string Sql, (string Name, object? Value, DbType Type)[] Parameters)> _queries = [];
    /// <summary>仅在单次报表查询期间打开捕获。</summary>
    private bool _enabled;

    /// <summary>返回最近一次捕获的查询副本。</summary>
    public IReadOnlyList<(string Sql, (string Name, object? Value, DbType Type)[] Parameters)> Queries => _queries.ToArray();

    /// <summary>开始一次独立的报表SQL捕获。</summary>
    public void Begin() {
        _queries.Clear();
        _enabled = true;
    }

    /// <summary>结束捕获，避免将执行计划本身计入报表SQL。</summary>
    public void End() => _enabled = false;

    /// <summary>记录EF实际完成的异步只读SQL及参数值。</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
        if (_enabled && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) {
            _queries.Add((command.CommandText, command.Parameters.Cast<DbParameter>()
                .Select(parameter => (parameter.ParameterName, parameter.Value, parameter.DbType)).ToArray()));
        }
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}
