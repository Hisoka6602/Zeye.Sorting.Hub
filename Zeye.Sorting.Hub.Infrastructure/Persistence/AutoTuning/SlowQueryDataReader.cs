using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>透明委托读取器，计量流式读取与释放，捕获命令返回之后的失败和取消。</summary>
internal sealed class SlowQueryDataReader : DbDataReader, IDbColumnSchemaGenerator {
    /// <summary>原始提供器读取器。</summary>
    private readonly DbDataReader _inner;
    /// <summary>一次实际命令的计量状态。</summary>
    private readonly SlowQueryExecution _execution;
    /// <summary>是否已明确读完全部结果集。</summary>
    private bool _exhausted;
    /// <summary>组合原始读取器和计量状态。</summary>
    internal SlowQueryDataReader(DbDataReader inner, SlowQueryExecution execution) { _inner = inner; _execution = execution; }
    /// <summary>透明调用提供器并保留异常堆栈。</summary>
    private T Value<T>(Func<T> action) {
        var start = Stopwatch.GetTimestamp();
        try { var result = action(); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <summary>计量实际读取或结果集切换。</summary>
    private bool Advance(bool row) {
        var start = Stopwatch.GetTimestamp();
        try { var result = row ? _inner.Read() : _inner.NextResult(); _execution.ReadFinished(start, row && result); _exhausted = !result; return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <summary>计量异步读取或结果集切换，保持调用方取消语义。</summary>
    private async Task<bool> AdvanceAsync(bool row, CancellationToken token) {
        var start = Stopwatch.GetTimestamp();
        try { var result = await (row ? _inner.ReadAsync(token) : _inner.NextResultAsync(token)).ConfigureAwait(false); _execution.ReadFinished(start, row && result); _exhausted = !result; return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override bool Read() => Advance(true);
    /// <inheritdoc />
    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => AdvanceAsync(true, cancellationToken);
    /// <inheritdoc />
    public override bool NextResult() => Advance(false);
    /// <inheritdoc />
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => AdvanceAsync(false, cancellationToken);
    /// <summary>读取器写命令的受影响行数；提供器释放后不支持访问时诊断不改变原始结果。</summary>
    private int AffectedRows() { try { return Math.Max(_inner.RecordsAffected, 0); } catch (Exception) { return 0; } }
    /// <inheritdoc />
    public override T GetFieldValue<T>(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetFieldValue<T>(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override async Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) {
        var start = Stopwatch.GetTimestamp();
        try { var result = await _inner.GetFieldValueAsync<T>(ordinal, cancellationToken).ConfigureAwait(false); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override Stream GetStream(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = new SlowQueryReaderStream(_inner.GetStream(ordinal), _execution); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override TextReader GetTextReader(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = new SlowQueryTextReader(_inner.GetTextReader(ordinal), _execution); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override void Close() {
        var start = Stopwatch.GetTimestamp();
        try { _inner.Close(); _execution.ReadFinished(start); _execution.Complete(partial: !_exhausted, affectedRows: AffectedRows()); }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override async Task CloseAsync() {
        var start = Stopwatch.GetTimestamp();
        try { await _inner.CloseAsync().ConfigureAwait(false); _execution.ReadFinished(start); _execution.Complete(partial: !_exhausted, affectedRows: AffectedRows()); }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        if (!disposing) return;
        var start = Stopwatch.GetTimestamp();
        try { _inner.Dispose(); _execution.ReadFinished(start); _execution.Complete(partial: !_exhausted, affectedRows: AffectedRows()); }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override async ValueTask DisposeAsync() {
        var start = Stopwatch.GetTimestamp();
        try { await _inner.DisposeAsync().ConfigureAwait(false); _execution.ReadFinished(start); _execution.Complete(partial: !_exhausted, affectedRows: AffectedRows()); }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override int Depth => _inner.Depth;
    /// <inheritdoc />
    public override int FieldCount => _inner.FieldCount;
    /// <inheritdoc />
    public override int VisibleFieldCount => _inner.VisibleFieldCount;
    /// <inheritdoc />
    public override bool HasRows => Value(() => _inner.HasRows);
    /// <inheritdoc />
    public override bool IsClosed => _inner.IsClosed;
    /// <inheritdoc />
    public override int RecordsAffected => _inner.RecordsAffected;
    /// <inheritdoc />
    public override object this[int ordinal] => Value(() => _inner[ordinal]);
    /// <inheritdoc />
    public override object this[string name] => Value(() => _inner[name]);
    /// <inheritdoc />
    public override bool GetBoolean(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetBoolean(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override byte GetByte(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetByte(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override char GetChar(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetChar(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override DateTime GetDateTime(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetDateTime(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override decimal GetDecimal(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetDecimal(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override System.Double GetDouble(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetDouble(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override System.Single GetFloat(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetFloat(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override Guid GetGuid(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetGuid(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override short GetInt16(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetInt16(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override int GetInt32(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetInt32(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override long GetInt64(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetInt64(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override string GetString(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetString(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override string GetName(int ordinal) => _inner.GetName(ordinal);
    /// <inheritdoc />
    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
    /// <inheritdoc />
    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
    /// <inheritdoc />
    public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
    /// <inheritdoc />
    public override Type GetProviderSpecificFieldType(int ordinal) => _inner.GetProviderSpecificFieldType(ordinal);
    /// <inheritdoc />
    public override object GetProviderSpecificValue(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetProviderSpecificValue(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override int GetProviderSpecificValues(object[] values) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetProviderSpecificValues(values); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override object GetValue(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetValue(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override int GetValues(object[] values) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.GetValues(values); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override bool IsDBNull(int ordinal) {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.IsDBNull(ordinal); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override async Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) {
        var start = Stopwatch.GetTimestamp();
        try { var result = await _inner.IsDBNullAsync(ordinal, cancellationToken).ConfigureAwait(false); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override DataTable? GetSchemaTable() => _inner.GetSchemaTable();
    /// <inheritdoc />
    public System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> GetColumnSchema() => _inner.GetColumnSchema();
    /// <inheritdoc />
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, false);
}
