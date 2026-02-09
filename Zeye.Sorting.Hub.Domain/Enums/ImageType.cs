using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    public enum ImageType {

        /// <summary>
        /// 扫码图（用于识别条码）
        /// </summary>
        [Description("扫码图")]
        Scan = 0,

        /// <summary>
        /// 全景图（俯视或整体图像）
        /// </summary>
        [Description("全景图")]
        Panorama = 1,

        /// <summary>
        /// 体积云点图（点云数据图，三维建模）
        /// </summary>
        [Description("体积云点图")]
        VolumePointCloud = 2,

        /// <summary>
        /// 体积彩图（结构光或多摄像头融合生成的彩色图）
        /// </summary>
        [Description("体积彩图")]
        VolumeColor = 3,

        /// <summary>
        /// 面单抠图（用于识别面单的局部图）
        /// </summary>
        [Description("面单抠图")]
        WaybillCropped = 4,

        /// <summary>
        /// 灰度仪
        /// </summary>
        [Description("灰度仪图")]
        GrayDetector = 5
    }
}
