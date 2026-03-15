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

        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();
    }
}
