using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

/// <summary>追加保存的包裹处理记录；失败、重试和未关联报文均保留原始事实。</summary>
public sealed record class ParcelProcessingRecord {
    /// <summary>来源实例、会话与记录标识计算得到的稳定持久化主键。</summary>
    [MaxLength(64)]
    public string Key { get; init; } = string.Empty;

    /// <summary>来源工作台名称，未知时保留空值。</summary>
    [MaxLength(128)]
    public string? WorkstationName { get; init; }

    /// <summary>与前一检测包裹的间隔，单位毫秒。</summary>
    public long? PreviousCreationGapMilliseconds { get; init; }

    /// <summary>是否触发包裹间距违规。</summary>
    public bool? IsSpacingViolation { get; init; }

    /// <summary>是否仍等待WCS路由决策。</summary>
    public bool? IsAwaitingWcsDecision { get; init; }
    /// <summary>来源处理记录的稳定身份，重试时必须保持一致。</summary>
    [MaxLength(128)]
    public string RecordId { get; init; } = string.Empty;

    /// <summary>来源实例编码。</summary>
    [MaxLength(96)]
    public string SourceInstanceId { get; init; } = string.Empty;

    /// <summary>设备编号所属运行批次，不能用条码替代。</summary>
    [MaxLength(96)]
    public string SourceRunId { get; init; } = string.Empty;

    /// <summary>来源设备包裹号，未关联DWS报文可为空。</summary>
    public long? SourceParcelId { get; init; } 

    /// <summary>Hub包裹主键，未关联报文可为空。</summary>
    public long? ParcelId { get; init; } 

    /// <summary>处理阶段。</summary>
    public ParcelProcessingStage Stage { get; init; } 

    /// <summary>事件实际发生时间，使用本地时间语义。</summary>
    public DateTime OccurredAt { get; init; } 

    /// <summary>Hub记录时间，使用本地时间语义。</summary>
    public DateTime RecordedAt { get; init; } = DateTime.Now;

    /// <summary>不可变分表锚点，关联包裹后使用包裹创建时间。</summary>
    public DateTime PartitionTime { get; init; } 

    /// <summary>规范化载荷哈希，用于拒绝同身份不同内容。</summary>
    [MaxLength(64)]
    public string PayloadHash { get; init; } = string.Empty;

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
    public DateTime? ReceivedAt { get; init; } 

    /// <summary>设备原始测量时间，缺失时为空。</summary>
    public DateTime? MeasuredAt { get; init; } 

    /// <summary>时间是否由可靠的设备载荷解析。</summary>
    public bool? HasReliableTimestamp { get; init; } 

    /// <summary>报文是否具有可靠帧边界。</summary>
    public bool? HasReliableFrameBoundary { get; init; } 

    /// <summary>设备共同关联号。</summary>
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
    public long? CandidateSourceParcelId { get; init; } 

    /// <summary>融合最终设备包裹号。</summary>
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
    public DateTime? RequestAt { get; init; } 

    /// <summary>外部接口响应时间。</summary>
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

    /// <summary>验证处理记录的身份、时间、测量值和阶段必要字段。</summary>
    public void Validate() {
        // 步骤1：身份与本地时间是每条追加记录的稳定边界。
        ValidateIdentityPart(RecordId, nameof(RecordId));
        ValidateIdentityPart(SourceInstanceId, nameof(SourceInstanceId));
        ValidateIdentityPart(SourceRunId, nameof(SourceRunId));
        if (!Enum.IsDefined(Stage) || AttemptNumber < 1 || SourceParcelId is <= 0 || ParcelId is <= 0
            || CandidateSourceParcelId is <= 0 || FinalSourceParcelId is <= 0) {
            throw new ArgumentException("处理阶段、尝试序号或包裹编号无效。");
        }
        if (SourceParcelId is null && Stage is not (ParcelProcessingStage.DwsReceived or ParcelProcessingStage.DwsBound)) throw new ArgumentException("除未关联DWS外，处理事实必须提供来源包裹编号。");
        if (Stage == ParcelProcessingStage.DwsBound && IsSuccess == true && (SourceParcelId is null || FinalSourceParcelId != SourceParcelId)) throw new ArgumentException("绑定成功必须提供与来源包裹号一致的FinalSourceParcelId。");
        if (Stage == ParcelProcessingStage.DwsBound && IsSuccess != true && SourceParcelId is not null) throw new ArgumentException("拒绝绑定不得关联包裹，候选编号应放入CandidateSourceParcelId。");
        ValidateLocalTime(OccurredAt, nameof(OccurredAt));
        ValidateLocalTime(RecordedAt, nameof(RecordedAt));
        ValidateLocalTime(PartitionTime, nameof(PartitionTime));
        foreach (var timestamp in new[] { ReceivedAt, MeasuredAt, RequestAt, ResponseAt }) {
            if (timestamp.HasValue) ValidateLocalTime(timestamp.Value, "来源时间");
        }
        // 步骤2：未知量测保留空值，负值不得进入业务快照。
        if (new[] { WeightGrams, LengthMm, WidthMm, HeightMm, VolumeMm3, VolumetricWeightGrams }.Any(value => value is < 0) || ElapsedMilliseconds is < 0 || PreviousCreationGapMilliseconds is < 0) {
            throw new ArgumentException("测量值和耗时不能为负数。");
        }
        if (Stage == ParcelProcessingStage.ChuteAssigned && IsSuccess == true && string.IsNullOrWhiteSpace(TargetChuteCode)) throw new ArgumentException("格口分配成功必须提供TargetChuteCode。");
        if (Stage == ParcelProcessingStage.SortingCompleted && string.IsNullOrWhiteSpace(ActualChuteCode)) {
            throw new ArgumentException("实际落格记录必须包含ActualChuteCode。");
        }
        if (Stage == ParcelProcessingStage.ParcelException && string.IsNullOrWhiteSpace(ExceptionCode)) {
            throw new ArgumentException("设备异常记录必须包含ExceptionCode。");
        }
        if ((Stage is ParcelProcessingStage.ImageRegistered or ParcelProcessingStage.ImageUploaded) && string.IsNullOrWhiteSpace(ImagePath)) {
            throw new ArgumentException("图片记录必须包含ImagePath。");
        }
        // 步骤3：统一检查持久化字段长度，拒绝数据库截断后才发现错误。
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
    }

    /// <summary>拒绝会改变来源身份哈希的首尾空白及不可见控制字符。</summary>
    private static void ValidateIdentityPart(string value, string fieldName) {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsControl)) {
            throw new ArgumentException(fieldName + "必须是非空、无首尾空白及控制字符的稳定来源标识。");
        }
    }

    /// <summary>验证本地时间或未指定时区的时间，拒绝缺省日期。</summary>
    private static void ValidateLocalTime(DateTime value, string fieldName) {
        if (value == default || value.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)) {
            throw new ArgumentException(fieldName + "必须使用有效的本地时间。");
        }
    }
}
