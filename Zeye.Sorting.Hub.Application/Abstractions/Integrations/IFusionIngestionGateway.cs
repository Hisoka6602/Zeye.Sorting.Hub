using Zeye.Sorting.Hub.Contracts.Models.Fusion;

namespace Zeye.Sorting.Hub.Application.Abstractions.Integrations;

/// <summary>融合来源的耐久接收、图片存储及待投影事实协作边界。</summary>
public interface IFusionIngestionGateway {
    /// <summary>校验独立来源的机器凭据，不接受浏览器会话。</summary>
    bool Authenticate(string sourceInstanceId, string credential);
    /// <summary>登记来源元数据并原子取得单连接租约。</summary>
    Task<FusionRegistration> RegisterAsync(string connectionId, string authenticatedSource, FusionHello hello, CancellationToken cancellationToken);
    /// <summary>逐条验证、耐久保存原文和同事务待投影状态后确认。</summary>
    Task<HubBatchReceipt> PublishAsync(string connectionId, HubFactBatch batch, CancellationToken cancellationToken);
    /// <summary>持久保存来源心跳与缓存舍弃指标。</summary>
    Task<HubHeartbeatReceipt> HeartbeatAsync(string connectionId, FusionHeartbeat heartbeat, CancellationToken cancellationToken);
    /// <summary>校验不可变图片身份并恢复耐久偏移。</summary>
    Task<HubImageBeginReceipt> BeginImageAsync(string connectionId, HubImageDescriptor descriptor, CancellationToken cancellationToken);
    /// <summary>完整落盘图片块后推进偏移。</summary>
    Task<HubImageChunkReceipt> WriteImageAsync(string connectionId, HubImageChunk chunk, CancellationToken cancellationToken);
    /// <summary>复核完整字节、大小和摘要并返回存储凭据。</summary>
    Task<HubImageStoredReceipt> CompleteImageAsync(string connectionId, HubImageComplete image, CancellationToken cancellationToken);
    /// <summary>释放当前连接租约，不释放其他连接的租约。</summary>
    Task DisconnectAsync(string connectionId, CancellationToken cancellationToken);
    /// <summary>有界认领待处理包裹事实，重启后恢复未完成认领。</summary>
    Task<IReadOnlyList<FusionProjectionItem>> ClaimProjectionsAsync(CancellationToken cancellationToken);
    /// <summary>记录包裹投影的耐久结果，失败保留重试任务。</summary>
    Task FinishProjectionAsync(FusionProjectionItem item, string? parcelId, string? errorMessage, CancellationToken cancellationToken);
    /// <summary>有界批量记录已提交用例的结果，使用认领身份阻止过期工作者覆盖。</summary>
    Task FinishProjectionsAsync(IReadOnlyList<(FusionProjectionItem Item, string? ParcelId, string? Error)> results, CancellationToken cancellationToken);
    /// <summary>读取无凭据的已登记来源及最新心跳状态。</summary>
    Task<IReadOnlyList<FusionSourceStatus>> GetSourcesAsync(CancellationToken cancellationToken);
    /// <summary>按来源查询有界原文与投影追溯记录。</summary>
    Task<IReadOnlyList<FusionFactInspection>> GetFactsAsync(string source, string? journal, int limit, CancellationToken cancellationToken);
    /// <summary>仅通过安全对象键读取已经完整落盘的图片。</summary>
    Task<(Stream Content, string ContentType)?> ReadImageAsync(string key, CancellationToken cancellationToken);
    /// <summary>清理有界、超时的未完成上传临时文件，保留不可变描述。</summary>
    Task MaintainUploadsAsync(CancellationToken cancellationToken);
}
