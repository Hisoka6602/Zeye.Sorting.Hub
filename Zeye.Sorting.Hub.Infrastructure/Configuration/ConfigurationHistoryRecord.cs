namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>映射既有 SQLite 历史表；保留原始 JSON、内容版本和提交状态。</summary>
internal sealed class ConfigurationHistoryRecord {
    /// <summary>既有历史条目的字符串键，对应数据库 Id 列。</summary>
    public string ChangeKey { get; set; } = string.Empty;
    /// <summary>修改的配置文档键。</summary>
    public string DocumentKey { get; set; } = string.Empty;
    /// <summary>修改前的内容版本。</summary>
    public string PreviousRevision { get; set; } = string.Empty;
    /// <summary>修改后的内容版本。</summary>
    public string Revision { get; set; } = string.Empty;
    /// <summary>修改前的完整 JSON。</summary>
    public string BeforeJson { get; set; } = string.Empty;
    /// <summary>修改后的完整 JSON。</summary>
    public string AfterJson { get; set; } = string.Empty;
    /// <summary>变更字段数组的 JSON。</summary>
    public string ChangedKeysJson { get; set; } = string.Empty;
    /// <summary>保持既有毫秒精度的本地时间文本。</summary>
    public string RecordedAtLocal { get; set; } = string.Empty;
    /// <summary>跨存储提交的确认结果。</summary>
    public string Status { get; set; } = string.Empty;
}
