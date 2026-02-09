using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    public enum VideoNodeType {

        /// <summary>
        /// 扫码节点
        /// </summary>
        [Description("扫码")]
        Scan = 0,

        /// <summary>
        /// 落格节点
        /// </summary>
        [Description("落格")]
        Discharge = 1
    }
}
