namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>融合接收端的独立配置，不复用网页账号或全局旧机器密钥。</summary>
public sealed class FusionIngestionOptions {
    /// <summary>是否接收已登记来源；可填写 true/false，默认 true。</summary>
    public bool IsEnabled { get; set; } = true;
    /// <summary>稳定中心编码；1至96个字母、数字、点、下划线或短横线。</summary>
    public string HubId { get; set; } = "sorting-hub";
    /// <summary>允许明文测试连接；可填写 true/false，默认 false。</summary>
    public bool AllowInsecureHttp { get; set; }
    /// <summary>接收批次条数；可填写1至100，默认50。</summary>
    public int MaxBatchRecords { get; set; } = 50;
    /// <summary>完整批次 UTF-8 字节上限；可填写16384至524288，默认524288。</summary>
    public int MaxBatchBytes { get; set; } = 524288;
    /// <summary>单块解码大小；可填写1024至65536，默认32768。</summary>
    public int MaxImageChunkBytes { get; set; } = 32768;
    /// <summary>单图片大小；可填写1至134217728字节，默认128MiB。</summary>
    public long MaxImageBytes { get; set; } = 134217728;
    /// <summary>单来源未完成上传上限；可填写1至1000，默认100。</summary>
    public int MaxPendingImagesPerSource { get; set; } = 100;
    /// <summary>连接租约期限；可填写30至600秒，默认120。</summary>
    public int LeaseSeconds { get; set; } = 120;
    /// <summary>临时上传保留期；可填写1至168小时，默认24。</summary>
    public int UploadRetentionHours { get; set; } = 24;
    /// <summary>图片持久化目录；可填写可写绝对路径或相对应用目录，不能与日志清理目录重叠。</summary>
    public string ImageDirectory { get; set; } = "fusion-images";
    /// <summary>UDP发现是否启用；可填写 true/false，默认 false。</summary>
    public bool DiscoveryEnabled { get; set; }
    /// <summary>独立 UDP 端口；可填写1024至65535，但5089为分拣机保留端口，默认47651。</summary>
    public int DiscoveryPort { get; set; } = 47651;
    /// <summary>对工作台广播的服务地址；可填写带 /hubs/fusion-ingestion 的绝对 HTTPS 地址，测试 HTTP 需显式许可。</summary>
    public string AdvertisedEndpoint { get; set; } = "";
    /// <summary>登记来源集合；各来源需要独立凭据，允许为空，空值不接受任何来源。</summary>
    public FusionSourceOptions[] Sources { get; set; } = [];
}
