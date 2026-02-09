using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 视频信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class VideoInfo {
        /// <summary>
        /// 通道号（摄像头编号）
        /// </summary>
        public required int Channel { get; init; }

        /// <summary>
        /// NVR 序列号（视频录像服务器唯一标识）
        /// </summary>
        public required string NvrSerialNumber { get; init; }

        /// <summary>
        /// 节点类型（如扫码节点、Chute 节点等）
        /// </summary>
        public required VideoNodeType NodeType { get; init; }
    }
}
