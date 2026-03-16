# EF Core 迁移说明（CodeFirst + 自动迁移）

## 1. 项目迁移架构总览

本项目采用 **EF Core CodeFirst** 模式：实体类与值对象定义在代码中，数据库结构由 EF Core 根据模型自动生成，通过迁移文件管理演进历史。

| 关键角色 | 所在位置 |
|----------|----------|
| DbContext | `Zeye.Sorting.Hub.Infrastructure/Persistence/SortingHubDbContext.cs` |
| 实体映射配置 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/` |
| 设计时工厂 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/MySqlContextFactory.cs` |
| 迁移文件存放目录 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/` |
| 运行时自动迁移服务 | `Zeye.Sorting.Hub.Host/HostedServices/DatabaseInitializerHostedService.cs` |

---

## 2. 当前迁移状态

| 项目 | 状态 |
|------|------|
| CodeFirst 模式 | ✅ 是 |
| 设计时工厂 (`IDesignTimeDbContextFactory`) | ✅ 已实现 |
| 初始迁移 (`InitialCreate`) | ✅ 已生成 |
| 运行时自动应用 (`Database.MigrateAsync`) | ✅ 已配置 |

初始迁移文件：`Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260316184030_InitialCreate.cs`

---

## 3. 运行时自动迁移（生产/开发环境默认行为）

应用程序启动时，`DatabaseInitializerHostedService` 会自动执行所有待应用的迁移，无需手动干预：

```
Host 启动
  └─► DatabaseInitializerHostedService.StartAsync()
        ├─ 创建 SortingHubDbContext 实例
        ├─ 调用 db.Database.MigrateAsync()   ← 自动应用全部 Pending 迁移
        └─ 执行方言特定的可选初始化 SQL
```

**重试策略**：最多 6 次，指数退避，最大间隔 30 秒，确保数据库容器尚未就绪时仍能稳定启动。

---

## 4. 迁移 CLI 使用方法

> **前提条件**：安装 `dotnet-ef` 全局工具
>
> ```bash
> dotnet tool install --global dotnet-ef
> # 或升级
> dotnet tool update --global dotnet-ef
> ```

所有命令均在**解决方案根目录**下执行，通过 `--project` 指定迁移所在项目。

### 4.1 新增迁移

```bash
dotnet ef migrations add <迁移名称> \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --output-dir Persistence/Migrations \
  --context SortingHubDbContext
```

**命名建议**：使用 PascalCase 动词短语，描述本次变更内容，例如：
- `AddParcelIndex`
- `AddChuteInfoLandedTimeIndex`
- `RenameParcelStatusColumn`

### 4.2 删除最新迁移（仅限尚未应用到数据库的迁移）

```bash
dotnet ef migrations remove \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext
```

### 4.3 查看迁移列表

```bash
dotnet ef migrations list \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext
```

### 4.4 手动推送迁移到数据库（可选，通常由自动迁移替代）

```bash
dotnet ef database update \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext \
  --connection "server=<HOST>;port=3306;database=zeye_sorting_hub;uid=<USER>;pwd=<PWD>;SslMode=None;"
```

> ⚠️ `MySqlContextFactory` 中的连接字符串为设计时占位值，执行 `database update` 时务必通过 `--connection` 参数传入真实连接字符串。

### 4.5 生成 DDL SQL 脚本（离线审计/DBA 审查）

```bash
dotnet ef migrations script \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext \
  --output migration.sql
```

---

## 5. 设计时工厂说明（`MySqlContextFactory` / `SqlServerContextFactory`）

`IDesignTimeDbContextFactory<SortingHubDbContext>` 的作用是让 `dotnet ef` 工具在没有宿主进程的情况下也能构建 `SortingHubDbContext`。

### 5.1 版本解析策略（两级优先级，不主动锁定版本）

`MySqlContextFactory` 通过 `ResolveServerVersion()` 按以下顺序确定 MySQL 版本，**不会硬性锁定版本号**：

| 优先级 | 机制 | 触发条件 | 说明 |
|--------|------|----------|------|
| 1 | `ServerVersion.AutoDetect` | 数据库可连通 | 最准确，无任何版本限制 |
| 2 | 兜底 `8.0.0` | AutoDetect 失败 | **仅影响 `dotnet ef` 工具链模型分析，不影响运行时行为**；选用 8.0 是因其为 Pomelo 当前最主流 LTS 基准，不限制实际部署的数据库版本 |

> **说明**：步骤 2 的兜底版本仅用于 EF Core 在设计时推断 MySQL 方言特性，不会限制实际部署时所连接的 MySQL 版本。

### 5.2 连接字符串配置

连接字符串从 `appsettings.json` 中的 `ConnectionStrings:MySql`（MySQL）或 `ConnectionStrings:SqlServer`（SQL Server）读取。

工厂按以下顺序搜索 `appsettings.json`：

| 优先级 | 搜索路径 | 适用场景 |
|--------|----------|----------|
| 1 | 当前工作目录 | 从 Host 项目目录或解决方案根运行 `dotnet ef` |
| 2 | 相邻的 `Zeye.Sorting.Hub.Host/` 子目录 | 从 Infrastructure 项目目录运行 `dotnet ef` |
| 3 | 向上遍历父目录寻找 `Zeye.Sorting.Hub.Host/` | 从任意子目录运行 `dotnet ef` |
| 4 | 代码内占位值（兜底） | 无法找到 `appsettings.json` 时，仅用于工具链模型分析 |

> **推荐运行方式**：在解决方案根目录运行 `dotnet ef`，工厂会自动找到 `Zeye.Sorting.Hub.Host/appsettings.json` 中的连接字符串。

---

## 6. 新增/修改实体后的迁移流程

当 `Parcel` 聚合或相关值对象发生结构变化时，按以下步骤生成并应用迁移：

```
1. 修改 Domain 层实体 / 值对象
        ↓
2. 修改对应的 EntityTypeConfiguration
        ↓
3. 执行 dotnet ef migrations add <Name>（见 4.1）
        ↓
4. 检查生成的迁移文件，确认 Up/Down 方法符合预期
        ↓
5. 提交迁移文件到版本控制
        ↓
6. 部署时 DatabaseInitializerHostedService 自动执行迁移
```

> 详细的 Parcel 属性新增流程，请参阅 [`Parcel属性新增操作指南.md`](./Parcel属性新增操作指南.md)。

---

## 7. 分表（Sharding）与迁移的关系

本项目通过 **EFCore.Sharding** 库实现按月/按哈希分表（见 `PersistenceServiceCollectionExtensions.cs`）。

**迁移只负责"基表"结构**，分表变体（如 `Parcels_202601`、`Parcels_202602`）由 EFCore.Sharding 在运行时按需创建。两者职责分离：

| 职责 | 负责方 |
|------|--------|
| 创建/变更表结构（列、索引、约束） | EF Core 迁移 |
| 按月/按哈希创建分片表 | EFCore.Sharding 运行时 |
| `__EFMigrationsHistory` 版本记录 | EF Core |

---

## 8. 常见问题

### Q：`Database.MigrateAsync` 和 `Database.EnsureCreated` 有什么区别？

| 方法 | 行为 |
|------|------|
| `MigrateAsync()` | 应用所有待执行的迁移文件，维护 `__EFMigrationsHistory`，支持增量变更 ✅ |
| `EnsureCreated()` | 若数据库不存在则按当前模型直接建库，不记录迁移历史，**不支持后续迁移** ❌ |

**本项目使用 `MigrateAsync()`**，是生产环境正确选择。

### Q：数据库被人为修改后，CodeFirst 模型还能保持同步吗？

`MigrateAsync()` 仅根据 `__EFMigrationsHistory` 决定哪些迁移需要执行，**不会自动检测或修复手工 DDL（`ALTER TABLE`、`DROP COLUMN` 等）导致的表结构偏差**。

#### 运行时守卫（已实现）

`DatabaseInitializerHostedService.AssertMigrationConsistencyAsync()` 在每次启动时执行两项检查：

| 检查项 | 行为 |
|--------|------|
| `GetPendingMigrationsAsync()` 不为空 | **输出 Critical 日志，列出具体迁移名称** — 说明迁移未全部应用，不阻止程序启动 |
| 代码迁移 ≠ `__EFMigrationsHistory` 记录 | **输出 Critical 日志，分别列出"在代码中但未应用"与"已应用但代码中不存在"的迁移名称** — 精确定位不一致来源 |

#### 手工 DDL 修改的正确处理方式

1. **切勿直接修改数据库表结构**（`ALTER TABLE`、`DROP TABLE` 等）
2. 若已发生，通过 `dotnet ef migrations add <Name>` 生成新迁移来描述"将当前 DB 对齐到代码模型"的变更
3. 提交迁移文件，部署时 `MigrateAsync()` 自动执行

> **注意**：EF Core 8 不具备自动检测实际列结构偏差的能力（该能力在 EF Core 9 的 `HasPendingModelChanges()` 中提供部分支持）。项目在升级到 EF Core 9 后可增强此守卫。

### Q：部署时数据库还未就绪怎么办？

`DatabaseInitializerHostedService` 内置 Polly 重试策略（最多 6 次，指数退避），数据库容器延迟启动时仍可稳定连接。重试耗尽后，数据库异常会记录为 `Critical` 日志，**程序不会崩溃**，将以降级模式运行。

### Q：数据库日志在哪里可以找到？

本项目使用 **NLog** 实现双路日志落盘（详见 `nlog.config`）：

| 日志文件 | 内容 | 归档策略 |
|----------|------|----------|
| `logs/app-<日期>.log` | 全量应用日志（所有级别） | 按天，保留 30 天 |
| `logs/database-<日期>.log` | 数据库专属日志：EF Core 迁移、`DatabaseInitializerHostedService`、`DatabaseAutoTuningHostedService`、`Persistence` 层 | 按天，保留 30 天 |

**NLog 低开销配置**：
- `targets async="true"` — 异步队列，写盘不阻塞业务线程
- `keepFileOpen="true"` — 文件句柄常开，避免每行重复 open/close
- `optimizeBufferReuse="true"` — 复用内存缓冲区，减少 GC 分配

**保证原则**：
- 任何数据库异常（连接失败、迁移失败、一致性警告等）均记录到 `database-*.log`
- 无任何数据库异常会导致程序崩溃，最坏情况是降级运行并输出 `Critical` 日志

### Q：如何针对 SQL Server 使用迁移？

`SqlServerContextFactory.cs` 已在 `Persistence/DesignTime/` 目录下创建，连接字符串从 `appsettings.json` 中的 `ConnectionStrings:SqlServer` 读取。使用以下命令生成 SQL Server 迁移：

```bash
# 1. 在 appsettings.json 中确认 ConnectionStrings:SqlServer 配置了真实连接字符串
# 2. 在解决方案根目录运行（工厂会自动找到 Zeye.Sorting.Hub.Host/appsettings.json）
dotnet ef migrations add <迁移名称> \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Host \
  --output-dir Persistence/Migrations \
  --context SortingHubDbContext
```

运行时 SQL Server 路径通过 `Persistence:Provider = SqlServer` 配置启用，迁移文件可同时服务 MySQL 和 SQL Server（MySQL 专属 Annotation 在 SQL Server 运行时会被 EF Core 自动忽略）。

### Q：如何接入第三种数据库（如 SQLite、PostgreSQL）？

请参阅 [`NewDatabaseProvider-Guide.md`](./NewDatabaseProvider-Guide.md)，文档以 SQLite 为例，逐步说明接入新提供器时需要修改的所有文件，并附接入核查清单。
