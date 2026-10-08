using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>DWS测量一致性、标准值偏差与可追溯的原始明细。</summary>
public sealed record ParcelDwsConsistencyResponse {
    /// <summary>当前完整测量快照生成时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime GeneratedAt { get; init; }
    /// <summary>排除身份未知和数值冲突之后的唯一测量次数。</summary>
    public int MeasurementCount { get; init; }
    /// <summary>重复测量条码数量。</summary>
    public int RepeatedBarcodeCount { get; init; }
    /// <summary>重量、物理体积或扫码耗时可比较的重复条码数。</summary>
    public int ComparableBarcodeCount { get; init; }
    /// <summary>重复条码中超过阈值的数量。</summary>
    public int DeviationBarcodeCount { get; init; }
    /// <summary>具有可靠检测和有效条码接收时间的独立样本数。</summary>
    public int ScanTimingSampleCount { get; init; }
    /// <summary>缺少可靠时间端点或明确包裹关联的样本数，扫码耗时保持未知。</summary>
    public int MissingScanTimingCount { get; init; }
    /// <summary>缺少稳定测量消息身份的原始记录数，不猜测去重。</summary>
    public int MissingIdentityCount { get; init; }
    /// <summary>同一测量身份出现互相冲突的数值或条码，整组排除。</summary>
    public int ConflictingMeasurementCount { get; init; }
    /// <summary>没有明确条码的唯一测量数量。</summary>
    public int MissingBarcodeCount { get; init; }
    /// <summary>合并重复接收、绑定和重复投影记录数。</summary>
    public int DuplicateRecordCount { get; init; }
    /// <summary>当前排行筛选命中条码数。</summary>
    public int FilteredCount { get; init; }
    /// <summary>当前20个条码排行。</summary>
    public IReadOnlyList<DwsConsistencyGroup> Items { get; init; } = [];
    /// <summary>当前总体的来源对比，最多100个来源。</summary>
    public IReadOnlyList<DwsConsistencySource> Sources { get; init; } = [];
    /// <summary>来源对比是否裁剪，完整总体不受影响。</summary>
    public bool SourcesTruncated { get; init; }
    /// <summary>所选条码的完整统计与分页明细。</summary>
    public DwsConsistencyDetail? Detail { get; init; }
}
