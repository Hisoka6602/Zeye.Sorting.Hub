namespace Zeye.Sorting.Hub.Application.Abstractions.Integrations;

/// <summary>经过来源凭据认证的设备发现生命周期。</summary>
public interface IFusionDiscoveryService {
    /// <summary>监听配置的独立 UDP 端口直到停止，停用时不占用端口。</summary>
    Task ListenAsync(CancellationToken cancellationToken);
}
