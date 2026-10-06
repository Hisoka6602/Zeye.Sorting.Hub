using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>可在线保存的接入服务设置。</summary>
public sealed record FusionSettings {
    /// <summary>是否允许 Fusion 接入。</summary>
    public bool IsEnabled { get; init; }
    /// <summary>是否允许隔离开发环境使用 HTTP。</summary>
    public bool AllowInsecureHttp { get; init; }
    /// <summary>是否启用签名 UDP 发现。</summary>
    public bool DiscoveryEnabled { get; init; }
    /// <summary>独立的 UDP 发现端口。</summary>
    public int DiscoveryPort { get; init; } = 47651;
    /// <summary>可被 Fusion 访问的 SignalR 地址。</summary>
    public string AdvertisedEndpoint { get; init; } = "";
    /// <summary>每批事实的记录条数上限。</summary>
    public int MaxBatchRecords { get; init; } = 50;
    /// <summary>每批事实的字节上限。</summary>
    public int MaxBatchBytes { get; init; } = 524288;
    /// <summary>每个图片分块的字节上限。</summary>
    public int MaxImageChunkBytes { get; init; } = 32768;
    /// <summary>每张图片的字节上限。</summary>
    public long MaxImageBytes { get; init; } = 134217728;
    /// <summary>每个来源未完成图片的数量上限。</summary>
    public int MaxPendingImagesPerSource { get; init; } = 100;
    /// <summary>正式连接的心跳租约期限。</summary>
    public int LeaseSeconds { get; init; } = 120;
    /// <summary>临时图片上传的保留小时数。</summary>
    public int UploadRetentionHours { get; init; } = 24;
    /// <summary>可在线保存的接入服务设置。</summary>
    public static FusionSettings From(FusionIngestionOptions options) => JsonSerializer.Deserialize<FusionSettings>(JsonSerializer.Serialize(options))!;
    /// <summary>将编辑字段写入待验证配置。</summary>
    public void Apply(FusionIngestionOptions options) {
        options.IsEnabled = IsEnabled; options.AllowInsecureHttp = AllowInsecureHttp; options.DiscoveryEnabled = DiscoveryEnabled;
        options.DiscoveryPort = DiscoveryPort; options.AdvertisedEndpoint = AdvertisedEndpoint;
        options.MaxBatchRecords = MaxBatchRecords; options.MaxBatchBytes = MaxBatchBytes; options.MaxImageChunkBytes = MaxImageChunkBytes;
        options.MaxImageBytes = MaxImageBytes; options.MaxPendingImagesPerSource = MaxPendingImagesPerSource;
        options.LeaseSeconds = LeaseSeconds; options.UploadRetentionHours = UploadRetentionHours;
    }
}
