using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 包裹平面坐标信息（值对象，表示包裹占据区域）
    /// </summary>
    public sealed record class ParcelPositionInfo {
        /// <summary>
        /// 关联包裹主键（仅用于基础设施层持久化映射与分片路由，不参与领域业务输入）
        /// </summary>
        public long ParcelId { get; private init; }

        /// <summary>
        /// 最小 X 坐标（左侧边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal X1 { get; init; }

        /// <summary>
        /// 最大 X 坐标（右侧边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal X2 { get; init; }

        /// <summary>
        /// 最小 Y 坐标（上边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal Y1 { get; init; }

        /// <summary>
        /// 最大 Y 坐标（下边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal Y2 { get; init; }

        /// <summary>
        /// 包裹中心点 X 坐标
        /// </summary>
        public decimal CenterX => (X1 + X2) / 2m;

        /// <summary>
        /// 包裹中心点 Y 坐标
        /// </summary>
        public decimal CenterY => (Y1 + Y2) / 2m;

        /// <summary>
        /// 背景区域最小 X 坐标（左侧边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal BackgroundX1 { get; init; }

        /// <summary>
        /// 背景区域最大 X 坐标（右侧边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal BackgroundX2 { get; init; }

        /// <summary>
        /// 背景区域最小 Y 坐标（上边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal BackgroundY1 { get; init; }

        /// <summary>
        /// 背景区域最大 Y 坐标（下边界）
        /// </summary>
        [Precision(18, 3)]
        public required decimal BackgroundY2 { get; init; }

        /// <summary>
        /// 背景区域中心点 X 坐标
        /// </summary>
        public decimal BackgroundCenterX => (BackgroundX1 + BackgroundX2) / 2m;

        /// <summary>
        /// 背景区域中心点 Y 坐标
        /// </summary>
        public decimal BackgroundCenterY => (BackgroundY1 + BackgroundY2) / 2m;

        /// <summary>
        /// 按领域语义比较坐标信息（忽略仅用于基础设施映射的 ParcelId）。
        /// </summary>
        /// <param name="other">待比较对象。</param>
        /// <returns>当领域字段一致时返回 true。</returns>
        public bool Equals(ParcelPositionInfo? other) {
            return other is not null
                && X1 == other.X1
                && X2 == other.X2
                && Y1 == other.Y1
                && Y2 == other.Y2
                && BackgroundX1 == other.BackgroundX1
                && BackgroundX2 == other.BackgroundX2
                && BackgroundY1 == other.BackgroundY1
                && BackgroundY2 == other.BackgroundY2;
        }

        /// <summary>
        /// 生成仅基于领域字段的哈希码（忽略 ParcelId）。
        /// </summary>
        /// <returns>领域字段哈希码。</returns>
        public override int GetHashCode() {
            return HashCode.Combine(
                HashCode.Combine(X1, X2, Y1, Y2),
                HashCode.Combine(BackgroundX1, BackgroundX2, BackgroundY1, BackgroundY2));
        }

        /// <summary>
        /// 输出仅包含领域字段的调试字符串（忽略 ParcelId）。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString() {
            return $"{nameof(ParcelPositionInfo)} {{ {nameof(X1)} = {X1}, {nameof(X2)} = {X2}, {nameof(Y1)} = {Y1}, {nameof(Y2)} = {Y2}, {nameof(BackgroundX1)} = {BackgroundX1}, {nameof(BackgroundX2)} = {BackgroundX2}, {nameof(BackgroundY1)} = {BackgroundY1}, {nameof(BackgroundY2)} = {BackgroundY2} }}";
        }
    }
}
