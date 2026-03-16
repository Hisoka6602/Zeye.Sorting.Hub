# 接入新数据库提供器指南

本文档以 **SQLite** 为示例，说明接入任意新数据库提供器时需要修改的文件与步骤。同样适用于 PostgreSQL、Oracle、DM（达梦）等任何 EF Core 支持的提供器。

---

## 1. 当前已支持的提供器

| 配置值 (`Persistence:Provider`) | 提供器 | NuGet 包 |
|---|---|---|
| `MySql` | MySQL / MariaDB | `Pomelo.EntityFrameworkCore.MySql` |
| `SqlServer` | SQL Server / Azure SQL | `Microsoft.EntityFrameworkCore.SqlServer` |

---

## 2. 接入新提供器需修改的文件清单

以接入 **SQLite** 为例（将 `Sqlite` 替换为你的提供器名）：

| 步骤 | 文件 | 操作 |
|------|------|------|
| 1 | `Zeye.Sorting.Hub.Infrastructure/Zeye.Sorting.Hub.Infrastructure.csproj` | 新增 NuGet PackageReference |
| 2 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/SqliteDialect.cs` | 新建方言实现 `IDatabaseDialect` |
| 3 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/SqliteContextFactory.cs` | 新建设计时工厂 `IDesignTimeDbContextFactory` |
| 4 | `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs` | 新增 `else if` 注册分支 |
| 5 | `Zeye.Sorting.Hub.Host/appsettings.json` | 新增连接字符串 + 切换 `Provider` |
| 6 | 终端命令 | 使用 `dotnet ef migrations add` 生成迁移 |

---

## 3. 逐步操作说明（SQLite 示例）

### 步骤 1：添加 NuGet 包

```bash
dotnet add Zeye.Sorting.Hub.Infrastructure/Zeye.Sorting.Hub.Infrastructure.csproj \
  package Microsoft.EntityFrameworkCore.Sqlite
```

或手动在 `.csproj` 中添加：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
```

> ⚠️ 不要锁定具体 patch 版本号，使用 `8.0.*` 允许安全更新。

---

### 步骤 2：新建方言实现

创建 `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/SqliteDialect.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>SQLite 方言（开发/测试/嵌入场景）</summary>
    public sealed class SqliteDialect : IDatabaseDialect {

        public string ProviderName => "SQLite";

        // SQLite 无需额外的初始化 SQL（无 QueryStore / Statistics 概念）
        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();

        public IReadOnlyList<string> BuildAutomaticTuningSql(
            string? schemaName, string tableName, IReadOnlyList<string> whereColumns) {
            // SQLite 的索引创建 SQL
            if (whereColumns.Count == 0) return Array.Empty<string>();
            var cols = string.Join(", ", whereColumns);
            var idxName = $"idx_auto_{tableName}_{string.Join("_", whereColumns)}";
            return new[] {
                $"CREATE INDEX IF NOT EXISTS \"{idxName}\" ON \"{tableName}\" ({cols})"
            };
        }

        public bool ShouldIgnoreAutoTuningException(Exception exception) => false;

        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(
            string? schemaName, string tableName, bool inPeakWindow, bool highRisk) {
            // SQLite 支持 ANALYZE（等价于统计信息更新）
            return new[] { $"ANALYZE \"{tableName}\"" };
        }
    }
}
```

---

### 步骤 3：新建设计时工厂

创建 `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/SqliteContextFactory.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime {

    /// <summary>
    /// SQLite 设计时 DbContext 工厂，供 dotnet ef 迁移工具使用。
    /// 连接字符串从 appsettings.json 中的 ConnectionStrings:Sqlite 读取。
    /// </summary>
    internal sealed class SqliteContextFactory : IDesignTimeDbContextFactory<SortingHubDbContext> {

        private const string FallbackConnectionString = "Data Source=zeye_sorting_hub_dev.db";

        public SortingHubDbContext CreateDbContext(string[] args) {
            var config = LoadConfiguration();
            var connectionString = config.GetConnectionString("Sqlite") ?? FallbackConnectionString;

            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseSqlite(connectionString)
                .Options;

            return new SortingHubDbContext(options);
        }

        private static IConfiguration LoadConfiguration() {
            var cwd = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(cwd, "appsettings.json")))
                return new ConfigurationBuilder().SetBasePath(cwd)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true).Build();

            var dir = new DirectoryInfo(cwd);
            for (var i = 0; i < 6 && dir != null; i++) {
                var hostPath = Path.Combine(dir.FullName, "Zeye.Sorting.Hub.Host", "appsettings.json");
                if (File.Exists(hostPath))
                    return new ConfigurationBuilder()
                        .SetBasePath(Path.Combine(dir.FullName, "Zeye.Sorting.Hub.Host"))
                        .AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile("appsettings.Development.json", optional: true).Build();
                dir = dir.Parent;
            }
            return new ConfigurationBuilder().Build();
        }
    }
}
```

> **注意**：`dotnet ef` 工具通过程序集扫描 `IDesignTimeDbContextFactory` 实现类。
> 同一程序集中可以存在多个工厂类（如 `MySqlContextFactory`、`SqlServerContextFactory`、`SqliteContextFactory`），
> 工具会按字母顺序选择第一个，**或**通过 `--context` 指定。若出现多工厂歧义，
> 可在命令行显式传入 `--context SortingHubDbContext` 加以消除。

---

### 步骤 4：在依赖注入中注册新提供器

打开 `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`，
在现有 `else if (SqlServer)` 分支之后、`else throw` 之前新增：

```csharp
else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)) {
    var connectionString = configuration.GetConnectionString("Sqlite");
    if (string.IsNullOrWhiteSpace(connectionString)) {
        throw new InvalidOperationException("缺少连接字符串：ConnectionStrings:Sqlite");
    }

    services.AddDbContextPool<SortingHubDbContext>(static (sp, options) => {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("Sqlite")!;
        options.UseSqlite(cs);
    });

    // EFCore.Sharding 目前无官方 SQLite 包，若需分表请参考社区方案
    // 暂时跳过 EFCore.Sharding 注册（仅支持单库非分表模式）

    services.AddSingleton<IDatabaseDialect, SqliteDialect>();
}
```

同时将错误提示中的可选值列表更新：

```csharp
throw new InvalidOperationException(
    $"不支持的数据库类型：{provider}，可选值：MySql / SqlServer / Sqlite");
```

> **分表限制**：EFCore.Sharding 目前仅提供 `EFCore.Sharding.MySql` 和 `EFCore.Sharding.SqlServer` 官方包，
> SQLite 暂不支持分表。若接入的新提供器无 EFCore.Sharding 支持，需跳过分表注册代码。

---

### 步骤 5：更新 appsettings.json

```jsonc
{
  "Persistence": {
    // 切换到新提供器
    "Provider": "Sqlite"
  },
  "ConnectionStrings": {
    // 新增 Sqlite 连接字符串（使用占位符，禁止提交真实凭据）
    "Sqlite": "Data Source=zeye_sorting_hub.db",
    // 保留原有连接字符串（切换时注释或删除不用的即可）
    "MySql": "server=127.0.0.1;port=3306;database=zeye_sorting_hub;uid={MYSQL_USER};password={MYSQL_PASSWORD};SslMode=None;",
    "SqlServer": "Server=127.0.0.1,1433;Database=zeye_sorting_hub;User Id={SQLSERVER_USER};Password={SQLSERVER_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
  }
}
```

---

### 步骤 6：生成初始迁移

由于不同数据库方言生成的 DDL 有差异（如 `AUTOINCREMENT` vs `IDENTITY`），
**推荐为每个提供器维护独立的迁移文件夹**：

```bash
# 生成 SQLite 专属初始迁移
dotnet ef migrations add InitialCreate \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --output-dir Persistence/Migrations/Sqlite \
  --context SortingHubDbContext
```

> 若多个提供器共用同一迁移文件夹，MySQL 特有的 `LONGTEXT`、`MySql:CharSet` 等 Annotation
> 在其他提供器运行时会被 EF Core 自动忽略，仍可正常建表，但 DDL 不够精简。
> 生产环境建议按提供器分文件夹管理。

---

## 4. 迁移文件夹策略对比

| 策略 | 适用场景 | 优点 | 缺点 |
|------|----------|------|------|
| 共享单一迁移文件夹（当前默认） | 仅单一提供器或开发期灵活切换 | 操作简单，迁移文件只维护一套 | 跨提供器 DDL 注解冗余 |
| 按提供器独立迁移文件夹 | 生产环境多提供器并存 | DDL 精准，无冗余 Annotation | 每次模型变更需为每个提供器各生成一次迁移 |

---

## 5. 新提供器接入核查清单

```
[ ] NuGet 包已添加到 Infrastructure 项目
[ ] XxxDialect.cs 实现 IDatabaseDialect 的 5 个方法
[ ] XxxContextFactory.cs 实现 IDesignTimeDbContextFactory
[ ] PersistenceServiceCollectionExtensions.cs 新增 else if 注册分支
[ ] appsettings.json 新增连接字符串并切换 Provider
[ ] dotnet ef migrations add 生成初始迁移
[ ] 运行 dotnet build 确认无编译错误
[ ] 启动应用，确认 DatabaseInitializerHostedService 日志显示迁移成功
[ ] 检查 logs/database-*.log 无 Critical/Error 级别日志
```

---

## 6. 已知注意事项

### EFCore.Sharding 分表支持范围

| 提供器 | EFCore.Sharding 包 | 分表支持 |
|--------|--------------------|----------|
| MySQL | `EFCore.Sharding.MySql` | ✅ |
| SQL Server | `EFCore.Sharding.SqlServer` | ✅ |
| 其他（SQLite、PostgreSQL 等） | 无官方包 | ❌ 需跳过分表注册 |

### AutoTuning 自动调优支持范围

`DatabaseAutoTuningHostedService` 的慢查询分析与自动索引建议对所有提供器均可工作，
但部分方言的 `BuildAutomaticTuningSql` / `BuildAutonomousMaintenanceSql` 需根据目标数据库语法进行适配（如 SQLite 无 `UPDATE STATISTICS` 命令）。

### 连接字符串安全

- 禁止在代码或 `appsettings.json` 中提交真实数据库凭据
- 生产环境在 `appsettings.json` 中使用 `{PLACEHOLDER}` 标记敏感字段，通过 .NET User Secrets（本地开发）或运维部署流程中的配置注入替换为真实值
- `appsettings.json` 中的连接字符串仅作格式示例，避免提交真实用户名/密码到版本控制
