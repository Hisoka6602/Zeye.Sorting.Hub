using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 条码信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射信息
    /// </summary>
    public sealed record class BarCodeInfo {
        /// <summary>
        /// 关联包裹主键（仅用于基础设施层持久化映射与分片路由，不参与领域业务输入）
        /// </summary>
        public long ParcelId { get; private init; }

        /// <summary>
        /// 条码
        /// </summary>
        [MaxLength(128)]
        public required string BarCode { get; init; }

        /// <summary>
        /// 条码类型
        /// </summary>
        public required BarCodeType BarCodeType { get; init; }

        /// <summary>
        /// 采集时间（可选）
        /// </summary>
        public DateTime? CapturedTime { get; init; }

        /// <summary>
        /// 按领域语义比较条码信息（忽略仅用于基础设施映射的 ParcelId）。
        /// </summary>
        /// <param name="other">待比较对象。</param>
        /// <returns>当领域字段一致时返回 true。</returns>
        public bool Equals(BarCodeInfo? other) {
            return other is not null
                && string.Equals(BarCode, other.BarCode, StringComparison.Ordinal)
                && BarCodeType == other.BarCodeType
                && CapturedTime == other.CapturedTime;
        }

        /// <summary>
        /// 生成仅基于领域字段的哈希码（忽略 ParcelId）。
        /// </summary>
        /// <returns>领域字段哈希码。</returns>
        public override int GetHashCode() {
            return HashCode.Combine(BarCode, BarCodeType, CapturedTime);
        }

        /// <summary>
        /// 输出仅包含领域字段的调试字符串（忽略 ParcelId）。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString() {
            return $"{nameof(BarCodeInfo)} {{ {nameof(BarCode)} = {BarCode}, {nameof(BarCodeType)} = {BarCodeType}, {nameof(CapturedTime)} = {CapturedTime} }}";
        }
    }
}
