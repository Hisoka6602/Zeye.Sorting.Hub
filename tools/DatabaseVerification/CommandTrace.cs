namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>仅留查询结构与计时，不记录参数、密码或业务报文。</summary>
internal sealed class CommandTrace {
    /// <summary>EF 生成的参数化查询结构。</summary>
    public required string Sql { get; init; }
    /// <summary>服务器打开读取器的执行耗时。</summary>
    public decimal Milliseconds { get; init; }
    /// <summary>实际投影列数。</summary>
    public int Fields { get; init; }
    /// <summary>读取器创建至释放的完整消费耗时。</summary>
    public decimal ReadMilliseconds { get; set; }
    /// <summary>EF 已读取行数。</summary>
    public int Rows { get; set; }
}
