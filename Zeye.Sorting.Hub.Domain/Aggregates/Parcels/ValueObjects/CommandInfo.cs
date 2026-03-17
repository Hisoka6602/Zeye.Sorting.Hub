using System;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 通信指令记录（值对象）
    /// 说明：
    /// 1) 属于 Parcel 聚合的属性集合
    /// 2) ORM 映射与独立表结构由 Infrastructure 层负责
    /// </summary>
    public sealed record class CommandInfo {
        /// <summary>
        /// 通信方式（如 TCP、UDP、SerialPort 等）
        /// </summary>
        public required ProtocolType ProtocolType { get; init; }

        /// <summary>
        /// 协议名称（如 Scs-V1、ChuteLock-Protocol 等）
        /// </summary>
        [MaxLength(128)]
        public required string ProtocolName { get; init; }

        /// <summary>
        /// 连接名称（如串口号、TCP 连接标识、设备编号等）
        /// </summary>
        [MaxLength(128)]
        public string ConnectionName { get; init; } = string.Empty;

        /// <summary>
        /// 指令内容
        /// </summary>
        public string CommandPayload { get; init; } = string.Empty;

        /// <summary>
        /// 指令产生时间（例如业务下发或设备接收时间）
        /// </summary>
        public required DateTime GeneratedTime { get; init; }

        /// <summary>
        /// 指令作用类型（如落格控制、锁格、灯控等）
        /// </summary>
        public required ActionType ActionType { get; init; }

        /// <summary>
        /// 格式化说明（如“锁定目标格口 A12，超时30秒”）
        /// </summary>
        [MaxLength(1024)]
        public string FormattedMessage { get; init; } = string.Empty;

        /// <summary>
        /// 指令方向（发送或接收）
        /// </summary>
        public required CommandDirection Direction { get; init; }
    }
}
