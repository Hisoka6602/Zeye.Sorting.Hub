using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 灰检信息（值对象）
    /// 说明：用于记录灰检设备返回的识别框信息与结果
    /// </summary>
    public sealed record class GrayDetectorInfo {
        /// <summary>
        /// Carrier 编号
        /// </summary>
        public required string CarrierNumber { get; init; }

        /// <summary>
        /// 附加框信息（建议存 JSON 或自定义结构字符串）
        /// </summary>
        public string AttachBoxInfo { get; init; } = string.Empty;

        /// <summary>
        /// 主框信息（建议存 JSON 或自定义结构字符串）
        /// </summary>
        public string MainBoxInfo { get; init; } = string.Empty;

        /// <summary>
        /// 联动 Carrier 数量
        /// </summary>
        public required int LinkedCarrierCount { get; init; }

        /// <summary>
        /// 包裹中心点坐标（例如："X,Y" 或 JSON）
        /// </summary>
        public string? CenterPosition { get; init; }

        /// <summary>
        /// 检测结果返回时间
        /// </summary>
        public required DateTime ResultTime { get; init; }

        /// <summary>
        /// 原始返回数据内容（建议存 JSON 或原始字符串）
        /// </summary>
        public required string RawResult { get; init; }
    }
}
