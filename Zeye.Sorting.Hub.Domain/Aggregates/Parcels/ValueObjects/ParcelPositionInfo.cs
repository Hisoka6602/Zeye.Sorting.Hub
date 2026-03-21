using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
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
    }
}
