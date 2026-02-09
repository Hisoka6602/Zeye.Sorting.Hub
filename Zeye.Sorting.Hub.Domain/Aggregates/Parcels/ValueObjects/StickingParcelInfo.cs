using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// Sticking 信息（值对象）
    /// 说明：用于记录 Sticking 判断结果与原始返回数据
    /// </summary>
    public sealed record class StickingParcelInfo {
        /// <summary>
        /// 是否 Sticking（true=存在 Sticking，false=无 Sticking）
        /// </summary>
        public required bool IsSticking { get; init; }

        /// <summary>
        /// 判断结果接收时间（系统接收判断结果的时间点）
        /// </summary>
        public DateTime? ReceiveTime { get; init; }

        /// <summary>
        /// 判断源数据内容（原始字符串或 JSON 格式数据）
        /// </summary>
        public string RawData { get; init; } = string.Empty;

        /// <summary>
        /// 总耗时（单位：毫秒，含判断请求发送到接收返回的总时间）
        /// </summary>
        public int? ElapsedMilliseconds { get; init; }
    }
}
