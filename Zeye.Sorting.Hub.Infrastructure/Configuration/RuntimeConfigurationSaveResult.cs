namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>本次保存的耐久版本、修改字段及待重启字段。</summary>
public sealed record RuntimeConfigurationSaveResult(string Revision, string[] ChangedKeys, string[] RestartRequiredKeys);
