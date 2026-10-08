namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>独立诊断归档库中的脱敏采样，不依赖正在诊断的业务数据库。</summary>
internal sealed class SlowQueryArchiveRow {
    /// <summary>归档流水编号。</summary>
    public long Id { get; set; }
    /// <summary>样本发生时间，采用本地时间语义。</summary>
    public DateTime OccurredAt { get; set; }
    /// <summary>完整结构生成的指纹。</summary>
    public string Fingerprint { get; set; } = "";
    /// <summary>脱敏后的一次采样与各阶段计量。</summary>
    public string Payload { get; set; } = "";
}
