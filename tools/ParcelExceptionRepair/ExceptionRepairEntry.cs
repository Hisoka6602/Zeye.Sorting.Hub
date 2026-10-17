namespace ParcelExceptionRepair;

/// <summary>仅保存异常修复涉及的字段；回滚前复核数据库位置及修复后的值。</summary>
public sealed record ExceptionRepairEntry {
    /// <summary>SQLite 数据库绝对路径，禁止把审计记录用于另一个数据库。</summary>
    public required string DatabasePath { get; init; }
    /// <summary>可信分表后缀，空字符串代表基础表。</summary>
    public required string Suffix { get; init; }
    /// <summary>中心包裹编号。</summary>
    public long ParcelId { get; init; }
    /// <summary>修复前的质量状态。</summary>
    public required ExceptionRepairSnapshot Before { get; init; }
    /// <summary>修复后预期的质量状态。</summary>
    public required ExceptionRepairSnapshot After { get; init; }
}
