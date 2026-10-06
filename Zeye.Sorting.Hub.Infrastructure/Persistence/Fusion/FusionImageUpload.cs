namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

/// <summary>不可变图片描述、耐久偏移及明确关联身份。</summary>
public sealed class FusionImageUpload {
    /// <summary>来源和图片编号联合摘要。</summary>
    public string Key { get; set; } = "";
    /// <summary>来源编码。</summary>
    public string SourceInstanceId { get; set; } = "";
    /// <summary>来源图片编号。</summary>
    public string SourceImageId { get; set; } = "";
    /// <summary>耐久上传身份。</summary>
    public string? UploadId { get; set; }
    /// <summary>仅用于展示的文件名。</summary>
    public string FileName { get; set; } = "";
    /// <summary>图片媒体类型。</summary>
    public string ContentType { get; set; } = "application/octet-stream";
    /// <summary>完整大小，尚未描述时为0。</summary>
    public long SizeBytes { get; set; } = 0;
    /// <summary>完整不可变摘要。</summary>
    public string ContentSha256 { get; set; } = "";
    /// <summary>耐久上传偏移。</summary>
    public long NextOffset { get; set; } = 0;
    /// <summary>完整对象已经校验落盘。</summary>
    public bool IsStored { get; set; } = false;
    /// <summary>中心本地最近上传时间。</summary>
    public DateTime ModifiedAt { get; set; } = default;
    /// <summary>明确关联的计数周期。</summary>
    public string? SourceRunId { get; set; } = null;
    /// <summary>明确关联的来源包裹编号。</summary>
    public long? SourceParcelId { get; set; } = null;
    /// <summary>来源相机。</summary>
    public string? CameraName { get; set; } = null;
    /// <summary>元数据并发版本。</summary>
    public long Revision { get; set; } = 0;
}
