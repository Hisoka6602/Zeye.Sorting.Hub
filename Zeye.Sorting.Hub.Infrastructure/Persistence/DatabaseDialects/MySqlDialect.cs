using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// MySQL 方言
    /// </summary>
    public sealed class MySqlDialect : IDatabaseDialect {
        public string ProviderName => "MySQL";

        public IReadOnlyList<string> GetOptionalBootstrapSql() => new[] {
            // 说明：只保留低风险设置；SET GLOBAL 需要高权限，建议运维侧完成
            "SET SESSION sql_safe_updates = 0;"
        };
    }
}
