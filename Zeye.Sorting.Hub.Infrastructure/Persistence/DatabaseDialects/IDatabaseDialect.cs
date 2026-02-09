using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    public interface IDatabaseDialect {

        /// <summary>
        /// 数据库类型名称（用于日志/诊断）
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 初始化阶段可选 SQL（非关键，失败必须可降级）
        /// </summary>
        IReadOnlyList<string> GetOptionalBootstrapSql();
    }
}
