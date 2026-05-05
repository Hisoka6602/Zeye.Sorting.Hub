namespace Zeye.Sorting.Hub.Contracts.Models.DataGovernance;

/// <summary>
/// 创建归档任务请求合同。
/// </summary>
public sealed record ArchiveTaskCreateRequest {
    /// <summary>
    /// 任务类型。
    /// 可填写范围：WebRequestAuditLogHistory。
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// 保留天数。
    /// 可填写范围：1~3650。
    /// </summary>
    public int RetentionDays { get; init; }

    /// <summary>
    /// 发起人标识。
    /// 可填写范围：1~64 个字符；留空时由系统回填为 system。
    /// </summary>
    public string? RequestedBy { get; init; }

    /// <summary>
    /// 备注。
    /// 可填写范围：0~512 个字符。
    /// </summary>
    public string? Remark { get; init; }
}
