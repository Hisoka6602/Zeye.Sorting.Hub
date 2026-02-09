using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// SQL Server 方言
    /// </summary>
    public sealed class SqlServerDialect : IDatabaseDialect {
        public string ProviderName => "SQLServer";

        public IReadOnlyList<string> GetOptionalBootstrapSql() => new[] {
            ""
            // 说明：按需补充
        };
    }
}
