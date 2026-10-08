namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>每张物理分表的耗时索引补齐进度，与投影批次原子提交，重启后从主键继续。</summary>
public sealed class ParcelDurationBackfillState {
    /// <summary>已校验的物理周期后缀，空值表示历史基础表。</summary>
    public string Suffix { get; set; } = string.Empty;
    /// <summary>最后完整提交的原始事实主键；不使用深分页。</summary>
    public string Cursor { get; set; } = string.Empty;
    /// <summary>全部历史接口事实已补齐，新事实始终在原事务中保存投影。</summary>
    public bool Completed { get; set; }
    /// <summary>同一物理分表的DWS窄投影已提交主键游标。</summary>
    public string DwsCursor { get; set; } = string.Empty;
    /// <summary>DWS检测、接收与绑定事实全部补齐，新事实由原保存事务派生。</summary>
    public bool DwsCompleted { get; set; }
    /// <summary>跨实例乐观并发版本。</summary>
    public int Revision { get; set; }
    /// <summary>最后成功提交时间。</summary>
    public DateTime UpdatedAt { get; set; }
}
