using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;

/// <summary>来源处理事实合同；Stage取值0检测、1DWS接收、2绑定、3扫描上传、4格口分配、5指令下发、6实际落格、7异常、8落格上报、9图片登记、10图片上传。所有时间为本地时间。</summary>
public record ParcelProcessingRecordRequest {
    /// <summary>与前一检测包裹的间隔，单位毫秒。</summary>
    public long? PreviousCreationGapMilliseconds { get; init; }
    /// <summary>是否触发包裹间距违规。</summary>
    public bool? IsSpacingViolation { get; init; }
    /// <summary>是否仍等待WCS路由决策。</summary>
    public bool? IsAwaitingWcsDecision { get; init; }
    /// <summary>来源工作台名称，未知时保留空值。</summary>
    [MaxLength(128)]
    public string? WorkstationName { get; init; }

    /// <summary>来源处理记录的稳定身份；同一事实重试复用，新执行尝试另取新值。检测事实可由来源三元组确定性生成。</summary>
    [MaxLength(128)]
    public string RecordId { get; init; } = string.Empty;

    /// <summary>由来源部署配置的稳定设备实例编码；不得使用条码、进程号或启动时间。</summary>
    [MaxLength(96)]
    public string SourceInstanceId { get; init; } = string.Empty;

    /// <summary>设备包裹计数有效会话；计数重置时更换，普通服务重启及消息重放时保持不变。</summary>
    [MaxLength(96)]
    public string SourceRunId { get; init; } = string.Empty;

    /// <summary>来源设备包裹号，未关联DWS报文可为空。</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? SourceParcelId { get; init; }

    /// <summary>处理阶段。</summary>
    public int Stage { get; init; }

    /// <summary>事件实际发生时间，使用本地时间语义。</summary>
    [JsonConverter(typeof(Zeye.Sorting.Hub.Contracts.Serialization.LocalDateTimeJsonConverter))]
    public DateTime OccurredAt { get; init; }

    /// <summary>阶段执行结果，未获得结果时为空。</summary>
    public bool? IsSuccess { get; init; }

    /// <summary>同一业务阶段的尝试序号，从1开始。</summary>
    public int AttemptNumber { get; init; } = 1;

    /// <summary>主条码，未识读前为空。</summary>
    [MaxLength(1024)]
    public string? Barcode { get; init; }

    /// <summary>扫描上传使用的多条码列表原文。</summary>
    public string? BarcodesJson { get; init; }

    /// <summary>原始测量重量，单位为克，缺失时为空。</summary>
    public decimal? WeightGrams { get; init; }

    /// <summary>测量长度，单位为毫米，缺失时为空。</summary>
    public decimal? LengthMm { get; init; }

    /// <summary>测量宽度，单位为毫米，缺失时为空。</summary>
    public decimal? WidthMm { get; init; }

    /// <summary>测量高度，单位为毫米，缺失时为空。</summary>
    public decimal? HeightMm { get; init; }

    /// <summary>物理体积，单位为立方毫米。</summary>
    public decimal? VolumeMm3 { get; init; }

    /// <summary>体积重量，单位为克，与物理体积分开保存。</summary>
    public decimal? VolumetricWeightGrams { get; init; }

    /// <summary>来源程序捕获的报文接收时间。</summary>
    [JsonConverter(typeof(Zeye.Sorting.Hub.Contracts.Serialization.LocalDateTimeJsonConverter))]
    public DateTime? ReceivedAt { get; init; }

    /// <summary>设备原始测量时间，缺失时为空。</summary>
    [JsonConverter(typeof(Zeye.Sorting.Hub.Contracts.Serialization.LocalDateTimeJsonConverter))]
    public DateTime? MeasuredAt { get; init; }

    /// <summary>时间是否由可靠的设备载荷解析。</summary>
    public bool? HasReliableTimestamp { get; init; }

    /// <summary>报文是否具有可靠帧边界。</summary>
    public bool? HasReliableFrameBoundary { get; init; }

    /// <summary>设备共同关联号。</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? CorrelationId { get; init; }

    /// <summary>设备触发批次，保留前导零。</summary>
    [MaxLength(128)]
    public string? TriggerBatch { get; init; }

    /// <summary>设备扫描序号，保留前导零。</summary>
    [MaxLength(128)]
    public string? ScanSequence { get; init; }

    /// <summary>DWS稳定消息身份，不能退化为条码。</summary>
    [MaxLength(256)]
    public string? MessageIdentity { get; init; }

    /// <summary>融合关联方式，如Exact或Fifo。</summary>
    [MaxLength(32)]
    public string? BindingMode { get; init; }

    /// <summary>融合候选设备包裹号。</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? CandidateSourceParcelId { get; init; }

    /// <summary>融合最终设备包裹号。</summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long? FinalSourceParcelId { get; init; }

    /// <summary>检测与报文时间差，单位为毫秒。</summary>
    public decimal? DeltaMilliseconds { get; init; }

    /// <summary>绑定拒绝、兜底或处理结果原因。</summary>
    [MaxLength(2048)]
    public string? DecisionReason { get; init; }

    /// <summary>FIFO恢复模式。</summary>
    [MaxLength(64)]
    public string? FifoRecoveryMode { get; init; }

    /// <summary>业务Provider标识。</summary>
    [MaxLength(96)]
    public string? Provider { get; init; }

    /// <summary>外部扫描上传返回的任务号。</summary>
    [MaxLength(256)]
    public string? TaskCode { get; init; }

    /// <summary>原始目标格口编码。</summary>
    [MaxLength(128)]
    public string? TargetChuteCode { get; init; }

    /// <summary>最终实际下发的格口编码。</summary>
    [MaxLength(128)]
    public string? DispatchedChuteCode { get; init; }

    /// <summary>设备实际落格编码。</summary>
    [MaxLength(128)]
    public string? ActualChuteCode { get; init; }

    /// <summary>是否采用兜底格口。</summary>
    public bool? IsFallback { get; init; }

    /// <summary>是否禁止继续正常路由。</summary>
    public bool? IsRoutingBlocked { get; init; }

    /// <summary>来源异常代码，保留未映射的设备异常类型。</summary>
    [MaxLength(128)]
    public string? ExceptionCode { get; init; }

    /// <summary>本次执行失败说明。</summary>
    [MaxLength(2048)]
    public string? ErrorMessage { get; init; }

    /// <summary>原始设备报文或通信指令。</summary>
    public string? RawPayload { get; init; }

    /// <summary>外部接口地址。</summary>
    [MaxLength(512)]
    public string? RequestUrl { get; init; }

    /// <summary>外部接口请求头。</summary>
    public string? RequestHeaders { get; init; }

    /// <summary>外部接口请求体。</summary>
    public string? RequestBody { get; init; }

    /// <summary>外部接口响应体。</summary>
    public string? ResponseBody { get; init; }

    /// <summary>外部接口响应状态码。</summary>
    public int? ResponseStatusCode { get; init; }

    /// <summary>外部接口请求时间。</summary>
    [JsonConverter(typeof(Zeye.Sorting.Hub.Contracts.Serialization.LocalDateTimeJsonConverter))]
    public DateTime? RequestAt { get; init; }

    /// <summary>外部接口响应时间。</summary>
    [JsonConverter(typeof(Zeye.Sorting.Hub.Contracts.Serialization.LocalDateTimeJsonConverter))]
    public DateTime? ResponseAt { get; init; }

    /// <summary>外部接口或指令执行耗时，单位为毫秒。</summary>
    public int? ElapsedMilliseconds { get; init; }

    /// <summary>图片文件路径或对象存储键。</summary>
    [MaxLength(1024)]
    public string? ImagePath { get; init; }

    /// <summary>图片来源相机标识。</summary>
    [MaxLength(128)]
    public string? ImageCamera { get; init; }

    /// <summary>图片文件完整性哈希。</summary>
    [MaxLength(128)]
    public string? ImageContentHash { get; init; }
}
