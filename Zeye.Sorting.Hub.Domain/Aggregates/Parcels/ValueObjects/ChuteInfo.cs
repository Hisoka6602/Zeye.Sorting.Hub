namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 格口信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射信息
    /// </summary>
    public sealed record class ChuteInfo {
        /// <summary>
        /// 目标格口 Id（例如接口返回的预期格口）
        /// </summary>
        public long? TargetChuteId { get; init; }

        /// <summary>
        /// 实际落格格口 Id（例如设备最终识别的实际落格口）
        /// </summary>
        public long? ActualChuteId { get; init; }

        /// <summary>
        /// 备用格口 Id（当主格口异常时备用使用）
        /// </summary>
        public long? BackupChuteId { get; init; }

        /// <summary>
        /// 落格时间
        /// </summary>
        public DateTime LandedTime { get; init; }
    }
}
