namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>由 Hub 登记的单个工作台身份及业务归属。</summary>
public sealed class FusionSourceOptions {
    /// <summary>稳定来源编码；1至96个字母、数字、点、下划线或短横线，区分大小写。</summary>
    public string SourceInstanceId { get; set; } = "";
    /// <summary>独立高熵机器密钥；32至256个字符，不返回给前端。</summary>
    public string MachineApiKey { get; set; } = "";
    /// <summary>展示名称；1至128个字符，未填写时使用来源编码。</summary>
    public string WorkstationName { get; set; } = "";
    /// <summary>登记租户；1至96个字符，不接受来源自行覆盖。</summary>
    public string TenantId { get; set; } = "default";
    /// <summary>登记存储分区；1至96个字符，与中心编码和包裹身份分开保存。</summary>
    public string StoragePartitionId { get; set; } = "default";
    /// <summary>产线编码；1至96个字符，必须匹配注册消息。</summary>
    public string LineId { get; set; } = "";
    /// <summary>站点编码；可填写 null 或1至96个字符，必须匹配注册消息。</summary>
    public string? SiteCode { get; set; }
    /// <summary>设备编码；可填写 null 或1至96个字符，必须匹配注册消息。</summary>
    public string? DeviceCode { get; set; }
    /// <summary>来源业务时区；填写系统支持的时区标识，默认 Asia/Shanghai。</summary>
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
}
