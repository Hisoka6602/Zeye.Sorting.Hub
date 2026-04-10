using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 视频信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class VideoInfo {
        /// <summary>
        /// 关联包裹主键（仅用于基础设施层持久化映射与分片路由，不参与领域业务输入）
        /// </summary>
        public long ParcelId { get; private init; }

        /// <summary>
        /// 通道号（摄像头编号）
        /// </summary>
        public required int Channel { get; init; }

        /// <summary>
        /// NVR 序列号（视频录像服务器唯一标识）
        /// </summary>
        [MaxLength(128)]
        public required string NvrSerialNumber { get; init; }

        /// <summary>
        /// 节点类型（如扫码节点、落格节点等）
        /// </summary>
        public required VideoNodeType NodeType { get; init; }

        /// <summary>
        /// 按领域语义比较视频信息（忽略仅用于基础设施映射的 ParcelId）。
        /// </summary>
        /// <param name="other">待比较对象。</param>
        /// <returns>当领域字段一致时返回 true。</returns>
        public bool Equals(VideoInfo? other) {
            return other is not null
                && Channel == other.Channel
                && string.Equals(NvrSerialNumber, other.NvrSerialNumber, StringComparison.Ordinal)
                && NodeType == other.NodeType;
        }

        /// <summary>
        /// 生成仅基于领域字段的哈希码（忽略 ParcelId）。
        /// </summary>
        /// <returns>领域字段哈希码。</returns>
        public override int GetHashCode() {
            return HashCode.Combine(Channel, NvrSerialNumber, NodeType);
        }

        /// <summary>
        /// 输出仅包含领域字段的调试字符串（忽略 ParcelId）。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString() {
            return $"{nameof(VideoInfo)} {{ {nameof(Channel)} = {Channel}, {nameof(NvrSerialNumber)} = {NvrSerialNumber}, {nameof(NodeType)} = {NodeType} }}";
        }
    }
}
