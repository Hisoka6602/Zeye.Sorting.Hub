using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 叠包信息（值对象）
    /// 说明：用于记录叠包判断结果与原始返回数据
    /// </summary>
    public sealed record class StickingParcelInfo {
        /// <summary>
        /// 关联包裹主键（仅用于基础设施层持久化映射与分片路由，不参与领域业务输入）
        /// </summary>
        public long ParcelId { get; private init; }

        /// <summary>
        /// 是否叠包（true=存在叠包，false=无叠包）
        /// </summary>
        public required bool IsSticking { get; init; }

        /// <summary>
        /// 判断结果接收时间（系统接收判断结果的时间点）
        /// </summary>
        public DateTime? ReceiveTime { get; init; }

        /// <summary>
        /// 判断源数据内容（原始字符串或 JSON 格式数据）
        /// </summary>
        [MaxLength(2048)]
        public string RawData { get; init; } = string.Empty;

        /// <summary>
        /// 总耗时（单位：毫秒，含判断请求发送到接收返回的总时间）
        /// </summary>
        public int? ElapsedMilliseconds { get; init; }

        /// <summary>
        /// 按领域语义比较叠包信息（忽略仅用于基础设施映射的 ParcelId）。
        /// </summary>
        /// <param name="other">待比较对象。</param>
        /// <returns>当领域字段一致时返回 true。</returns>
        public bool Equals(StickingParcelInfo? other) {
            return other is not null
                && IsSticking == other.IsSticking
                && ReceiveTime == other.ReceiveTime
                && string.Equals(RawData, other.RawData, StringComparison.Ordinal)
                && ElapsedMilliseconds == other.ElapsedMilliseconds;
        }

        /// <summary>
        /// 生成仅基于领域字段的哈希码（忽略 ParcelId）。
        /// </summary>
        /// <returns>领域字段哈希码。</returns>
        public override int GetHashCode() {
            return HashCode.Combine(IsSticking, ReceiveTime, RawData, ElapsedMilliseconds);
        }

        /// <summary>
        /// 输出仅包含领域字段的调试字符串（忽略 ParcelId）。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString() {
            return $"{nameof(StickingParcelInfo)} {{ {nameof(IsSticking)} = {IsSticking}, {nameof(ReceiveTime)} = {ReceiveTime}, {nameof(RawData)} = {RawData}, {nameof(ElapsedMilliseconds)} = {ElapsedMilliseconds} }}";
        }
    }
}
