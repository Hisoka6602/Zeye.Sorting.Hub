using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 包裹所属设备信息（值对象）
    /// 说明：用于描述包裹在哪个工位/设备被入站或处理
    /// </summary>
    public sealed record class ParcelDeviceInfo {
        /// <summary>
        /// 工作台名称（默认使用计算机名称或工位标识）
        /// </summary>
        [MaxLength(128)]
        public required string WorkstationName { get; init; }

        /// <summary>
        /// 设备机器码（设备唯一编号，如硬件指纹、主板号等）
        /// </summary>
        [MaxLength(128)]
        public required string MachineCode { get; init; }

        /// <summary>
        /// 设备自定义名称（如“扫码器-A1”、“称重台-B2”等）
        /// </summary>
        [MaxLength(128)]
        public required string CustomName { get; init; }
    }
}
