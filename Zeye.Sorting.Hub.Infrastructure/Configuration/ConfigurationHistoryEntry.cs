namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>SQLite 中保存的配置变更原值和提交结果，仅供超级管理员查询。</summary>
public sealed record ConfigurationHistoryEntry(string Id, string DocumentKey, string PreviousRevision, string Revision,
    string BeforeJson, string AfterJson, string[] ChangedKeys, string RecordedAtLocal, string Status);
