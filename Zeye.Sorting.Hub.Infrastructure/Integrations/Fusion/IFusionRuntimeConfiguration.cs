namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>原子发布的运行配置；机器入口不访问网页或部署文件。</summary>
public interface IFusionRuntimeConfiguration {
    /// <summary>获取当前目录版本的原子快照。</summary>
    FusionRuntimeSnapshot Snapshot { get; }
}
