using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zeye.Sorting.Hub.Domain.Abstractions;

namespace Zeye.Sorting.Hub.Domain.Primitives {

    /// <summary>
    /// 审计实体基类（领域层）
    /// 说明：仅表达领域语义，不包含任何 ORM 映射特性
    /// </summary>
    public abstract class AuditableEntity : IEntity<long> {

        /// <summary>
        /// 主键 Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; protected set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime ModifyTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 修改 IP
        /// </summary>
        public string ModifyIp { get; set; } = string.Empty;
    }
}
