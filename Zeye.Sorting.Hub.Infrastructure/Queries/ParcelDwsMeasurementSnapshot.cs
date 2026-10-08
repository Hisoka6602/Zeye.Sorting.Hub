using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using System.Linq.Expressions;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>DWS耐久窄投影，不包含设备报文、图片或接口正文；与原事实保持同一事务。</summary>
public sealed record ParcelDwsMeasurementSnapshot {
    /// <summary>查询及后台补齐共用字段投影，避免量测字段在两条链路中漂移。</summary>
    internal static readonly Expression<Func<ParcelProcessingRecord, ParcelDwsMeasurementSnapshot>> Projection = row => new() {
        Key = row.Key, RecordId = row.RecordId, ParcelId = row.ParcelId,
        SourceInstanceId = row.SourceInstanceId, SourceRunId = row.SourceRunId, SourceParcelId = row.SourceParcelId,
        WorkstationName = row.WorkstationName, MessageIdentity = row.MessageIdentity, Barcode = row.Barcode,
        Stage = row.Stage, PartitionTime = row.PartitionTime, OccurredAt = row.OccurredAt, MeasuredAt = row.MeasuredAt,
        ReceivedAt = row.ReceivedAt, HasReliableTimestamp = row.HasReliableTimestamp, IsSuccess = row.IsSuccess,
        WeightGrams = row.WeightGrams, LengthMm = row.LengthMm, WidthMm = row.WidthMm, HeightMm = row.HeightMm, VolumeMm3 = row.VolumeMm3
    };
    /// <summary>一次编译，写入热路径只作内存字段映射。</summary>
    private static readonly Func<ParcelProcessingRecord, ParcelDwsMeasurementSnapshot> Project = Projection.Compile();
    /// <summary>只保存检测、接收及绑定事实，其他事实不增加量测索引写入。</summary>
    internal static bool Includes(ParcelProcessingStage stage) => stage is ParcelProcessingStage.Detected or ParcelProcessingStage.DwsReceived or ParcelProcessingStage.DwsBound;
    /// <summary>保留原始幂等键与真实字段，不推测条码、关联关系和时间。</summary>
    internal static ParcelDwsMeasurementSnapshot Create(ParcelProcessingRecord record) => Project(record);
    /// <summary>持久化记录键。</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>原始事实身份。</summary>
    public string RecordId { get; init; } = string.Empty;
    /// <summary>关联包裹编号。</summary>
    public long? ParcelId { get; init; }
    /// <summary>来源实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>运行批次。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>设备包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>来源工作台。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>稳定测量消息身份。</summary>
    public string? MessageIdentity { get; init; }
    /// <summary>当前测量条码。</summary>
    public string? Barcode { get; init; }
    /// <summary>接收或绑定阶段。</summary>
    public ParcelProcessingStage Stage { get; init; }
    /// <summary>不可变首次入库分表时间。</summary>
    public DateTime PartitionTime { get; init; }
    /// <summary>当前事实发生时间。</summary>
    public DateTime OccurredAt { get; init; }
    /// <summary>真实设备测量时间。</summary>
    public DateTime? MeasuredAt { get; init; }
    /// <summary>来源程序读取DWS报文时捕获的接收时间。</summary>
    public DateTime? ReceivedAt { get; init; }
    /// <summary>明确不可靠的时间不参与扫码耗时比较。</summary>
    public bool? HasReliableTimestamp { get; init; }
    /// <summary>明确关联结果。</summary>
    public bool? IsSuccess { get; init; }
    /// <summary>原始重量克。</summary>
    public decimal? WeightGrams { get; init; }
    /// <summary>原始长度毫米。</summary>
    public decimal? LengthMm { get; init; }
    /// <summary>原始宽度毫米。</summary>
    public decimal? WidthMm { get; init; }
    /// <summary>原始高度毫米。</summary>
    public decimal? HeightMm { get; init; }
    /// <summary>物理体积立方毫米。</summary>
    public decimal? VolumeMm3 { get; init; }
}
