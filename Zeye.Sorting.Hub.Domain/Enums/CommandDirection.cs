using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// CommandDirection 枚举。
    /// </summary>
    public enum CommandDirection {

        /// <summary>
        /// 接收（来自外部设备的响应或主动上报）
        /// </summary>
        [Description("接收")]
        Receive = 0,

        /// <summary>
        /// 发送（由系统向外部设备发出的指令）
        /// </summary>
        [Description("发送")]
        Send = 1
    }
}
