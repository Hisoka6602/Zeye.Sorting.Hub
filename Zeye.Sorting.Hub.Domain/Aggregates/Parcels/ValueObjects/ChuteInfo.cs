using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// Chute 信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射信息
    /// </summary>
    public sealed record class ChuteInfo {
        /// <summary>
        /// 目标 Chute Id（例如接口返回的预期 Chute）
        /// </summary>
        public long? TargetChuteId { get; init; }

        /// <summary>
        /// 实际 Chute Id（例如设备最终识别的实际 Chute）
        /// </summary>
        public long? ActualChuteId { get; init; }

        /// <summary>
        /// 备用 Chute Id（当主 Chute 异常时备用使用）
        /// </summary>
        public long? BackupChuteId { get; init; }

        /// <summary>
        /// Chute 时间
        /// </summary>
        public DateTime LandedTime { get; init; }
    }
}
