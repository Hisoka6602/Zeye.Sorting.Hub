using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>只允许配置文档进入此存储，业务和历史仍由关系数据库管理。</summary>
public interface IConfigurationDocumentStore {
    /// <summary>识别允许进入配置集合的固定文档键。</summary>
    static bool IsConfiguration(string key) => key is "operations-policy" or "rules-parcel" or "rules-exception" or "fusion-ingestion-directory";
    /// <summary>读取某个当前配置文档。</summary>
    ManagedDocument? Read(string key);
    /// <summary>校验客户端版本后保存配置。</summary>
    ManagedDocument? Write(string key, string json, int expectedRevision);
    /// <summary>兼容导入旧配置，但不覆盖已有配置。</summary>
    void Import(IEnumerable<ManagedDocument> documents);
}
