using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 体积来源类型
    /// </summary>
    public enum VolumeSourceType {

        /// <summary>
        /// 3D 相机
        /// </summary>
        [Description("3D相机")]
        Camera3D = 0,

        /// <summary>
        /// 线扫相机
        /// </summary>
        [Description("线扫相机")]
        LineScanCamera = 1,

        /// <summary>
        /// 传感器测量
        /// </summary>
        [Description("传感器测量")]
        Sensor = 2,

        /// <summary>
        /// 手动输入
        /// </summary>
        [Description("手动输入")]
        Manual = 3,

        /// <summary>
        /// TCP
        /// </summary>
        [Description("Tcp")]
        Tcp = 4
    }
}
