using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// BarCodeType 枚举。
    /// </summary>
    public enum BarCodeType {

        /// <summary>
        /// 快递面单条码（用于识别快递包裹）
        /// </summary>
        [Description("快递面单条码")]
        ExpressSheet = 0,

        /// <summary>
        /// 包装物条码（如箱、袋、托盘等包装材料）
        /// </summary>
        [Description("包装物条码")]
        ParcelMaterial = 1,

        /// <summary>
        /// 商品条码（用于识别具体商品）
        /// </summary>
        [Description("商品条码")]
        Product = 2,

        /// <summary>
        /// 附件条码（如赠品、配件、说明书等）
        /// </summary>
        [Description("附件条码")]
        Attachment = 3,

        /// <summary>
        /// 其他（不属于以上分类的条码）
        /// </summary>
        [Description("其他")]
        Other = 4
    }
}
