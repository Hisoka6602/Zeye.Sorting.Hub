# EF Core 迁移说明（CodeFirst + 自动迁移）

来源：仓库内 SortingHubDbContext、设计时工厂、迁移源码和 CI 工作流；本文为项目原生操作指南，未从 doc/pdf 解析。

## 1. 项目迁移架构总览

本项目采用 **EF Core CodeFirst** 模式：实体类与值对象定义在代码中，数据库结构由 EF Core 根据模型自动生成，通过迁移文件管理演进历史。

| 关键角色 | 所在位置 |
|----------|----------|
| DbContext | `Zeye.Sorting.Hub.Infrastructure/Persistence/SortingHubDbContext.cs` |
| 实体映射配置 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/` |
| 设计时工厂 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/MySqlContextFactory.cs`<br>`Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/SqlServerContextFactory.cs` |
| 迁移文件存放目录 | MySQL：`Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/`<br>SQL Server：`Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations/Migrations/` |
| 运行时自动迁移服务 | `Zeye.Sorting.Hub.Host/HostedServices/DatabaseInitializerHostedService.cs` |

---

## 2. 当前迁移状态

| 项目 | 状态 |
|------|------|
| CodeFirst 模式 | ✅ 是 |
| 设计时工厂 (`IDesignTimeDbContextFactory`) | ✅ 已实现 |
| MySQL迁移链与SQL Server独立基线 | ✅ 已生成；两种Provider分别维护模型快照 |
| 运行时自动应用 (`Database.MigrateAsync`) | ✅ 已配置 |

MySQL初始迁移为`20260324094539_RebuildBaseline20260324`；SQL Server初始迁移为`20260928143409_InitialSqlServerSchema`，其后为`20260928160257_AddImageObjectStorageMetadataSqlServer`。

### 2.1 SQL Server 迁移策略（明确）

- **当前落地策略**：MySQL保留现有迁移链；SQL Server使用独立迁移程序集和SQL Server模型快照，运行时由Provider自动选路。两边共用同一实体模型，不复制业务代码。
- **提供器约定**：SQL Server 相关 `dotnet ef` 命令统一追加 `-- --provider SqlServer`，MySQL 相关命令统一使用 `-- --provider MySql`（或默认值）。
- **发布门禁**：MySQL与SQL Server分别检查模型漂移；SQL Server还在隔离库执行升级、空库回退和再次升级。
- **历史库边界**：SQL Server初始迁移只允许空库。有旧共享迁移历史或现存业务表时，必须另定保留数据的过渡方案；基线会主动拒绝直接覆盖。
- **图片分表边界**：增量迁移检测到已有`Parcel_ImageInfos_*`物理表时主动拒绝；必须先制定字段和索引受控补建方案。含图片对象定位数据或图片物理分表时，回退拒绝删除字段。

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

### 3.1 发布策略：迁移失败是否阻断启动（明确）

- **运行时策略可配置**：通过 `Persistence:Migration:FailStartupOnError` 控制迁移失败后的启动行为。
  - `false`（默认）：重试耗尽后记录 `Critical` 并降级运行（不阻断启动）。
  - `true`：重试耗尽后记录 `Critical` 并重新抛出异常，**阻断启动（fail-fast）**。
- **发布门禁策略**：是否允许发布由发布流程决定，**发布前必须通过 CI 的 EF 验收流水线**（`dotnet ef migrations list / database update / migrations script`）。
- **结论**：运行时支持“可降级不中断 / fail-fast”双模式，发布侧“未通过 EF 验收即阻断发布”。

示例配置：

```json
"Persistence": {
  "Migration": {
    "FailStartupOnError": false
  }
}
```

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

**MySQL（默认）**

```bash
dotnet ef migrations add <迁移名称> \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --output-dir Persistence/Migrations \
  --context SortingHubDbContext \
  -- --provider MySql
```

**SQL Server**

```bash
dotnet ef migrations add <迁移名称> \
  --project Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations \
  --startup-project Zeye.Sorting.Hub.Host \
  --output-dir Migrations \
  --context SortingHubDbContext \
  -- --provider SqlServer
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
  --context SortingHubDbContext \
  -- --provider MySql
```

SQL Server删除最新未应用迁移时，将`--project`改为`Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations`，`--startup-project`改为`Zeye.Sorting.Hub.Host`，并使用`-- --provider SqlServer`。

### 4.3 查看迁移列表

**MySQL（默认）**

```bash
dotnet ef migrations list \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext \
  -- --provider MySql
```

**SQL Server**

```bash
dotnet ef migrations list \
  --project Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations \
  --startup-project Zeye.Sorting.Hub.Host \
  --context SortingHubDbContext \
  -- --provider SqlServer
```

### 4.4 手动推送迁移到数据库（可选，通常由自动迁移替代）

**MySQL（默认）**

```bash
dotnet ef database update \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext \
  --connection "server=<HOST>;port=3306;database=zeye_sorting_hub;uid=<USER>;Password=<PWD>;SslMode=None;" \
  -- --provider MySql
```

**SQL Server**

```bash
dotnet ef database update \
  --project Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations \
  --startup-project Zeye.Sorting.Hub.Host \
  --context SortingHubDbContext \
  --connection "Server=<HOST>,1433;Database=zeye_sorting_hub;User Id=<USER>;Password=<PWD>;TrustServerCertificate=True;Encrypt=False;" \
  -- --provider SqlServer
```

> 执行数据库更新前必须核对目标库。优先用进程环境变量`ConnectionStrings__MySql`或`ConnectionStrings__SqlServer`传入连接字符串，避免将密码写入命令行或提交到仓库；上述`--connection`只展示参数格式。

### 4.5 生成 DDL SQL 脚本（离线审计/DBA 审查）

**MySQL（默认）**

```bash
dotnet ef migrations script \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Infrastructure \
  --context SortingHubDbContext \
  --output migration.sql \
  -- --provider MySql
```

**SQL Server**

```bash
dotnet ef migrations script \
  --project Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations \
  --startup-project Zeye.Sorting.Hub.Host \
  --context SortingHubDbContext \
  --output migration.sql \
  -- --provider SqlServer
```

---

## 4.6 CI / 部署前 EF 验收流水线（真实执行）

仓库已提供工作流：`.github/workflows/ef-migration-validation.yml`，分别在 **MySQL** 与 **SQL Server** 容器检查迁移列表、模型快照差异、正向脚本和真实升级；SQL Server再验证空库回退与重新升级。含业务数据或物理分表的回退由迁移内守卫拒绝。

1. `dotnet ef migrations list`
2. `dotnet ef database update`
3. `dotnet ef migrations script`

历史物理事实表的`OccurredAt`索引由`performance/scripts/backfill-processing-occurred-index.ps1`生成计划、审计和回滚清单；新物理周期通过当前模型自带索引。用法见`performance/README.md`。

---

## 4.7 迁移基线与回滚演练制度（建议执行）

为降低迁移回归与灾备切换风险，建议将以下流程纳入常态化制度：

1. **迁移基线冻结（每次发布前）**
   - 固定待发布迁移集合（迁移名 + 脚本哈希）。
   - 同时导出 MySQL / SQL Server 的 `migrations script` 结果并存档审计。
2. **定期回滚演练（建议每月至少一次）**
   - 在灾备环境执行“升级 -> 验证 -> 回滚（`database update <旧迁移>`）-> 再验证”闭环。
   - 记录耗时、失败点、人工介入步骤，形成可复用 Runbook。
3. **灾备升降级演练（建议每季度至少一次）**
   - 以生产同版本数据快照执行“升版本 + 降版本”双向演练。
   - 演练报告必须包含：前置条件、回滚触发条件、回滚后数据一致性核验结果。
4. **发布门禁联动**
   - 任一演练未通过时，禁止进入生产发布窗口，优先修复迁移脚本与回滚路径。

---

## 5. 设计时工厂说明（统一入口 + 提供器分发）

`IDesignTimeDbContextFactory<SortingHubDbContext>` 的作用是让 `dotnet ef` 工具在没有宿主进程的情况下也能构建 `SortingHubDbContext`。
本项目当前采用**统一入口工厂**（`MySqlContextFactory`）并支持通过 `-- --provider SqlServer` 分发到 SQL Server 配置路径。

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

本项目的Parcel聚合通过`ParcelPartitionStore`按首次入库周期创建物理表，其他分表治理见`PersistenceServiceCollectionExtensions.cs`。

**迁移只负责基础表结构**，分表变体（如`Parcels_202601`、`Parcels_202602`）由`ParcelPartitionStore`在显式允许且非dry-run时创建。两者职责分离：

| 职责 | 负责方 |
|------|--------|
| 创建/变更表结构（列、索引、约束） | EF Core 迁移 |
| 按周期创建Parcel物理表 | ParcelPartitionStore运行时DDL隔离器 |
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

#### 运行时守卫（EF Core 9，三项检查）

`DatabaseInitializerHostedService.AssertMigrationConsistencyAsync()` 在每次启动时执行三项检查：

| 检查项 | 行为 |
|--------|------|
| `GetPendingMigrationsAsync()` 不为空 | **输出 Critical 日志，列出具体迁移名称** — 说明迁移未全部应用，不阻止程序启动 |
| 代码迁移 ≠ `__EFMigrationsHistory` 记录 | **输出 Critical 日志，分别列出"在代码中但未应用"与"已应用但代码中不存在"的迁移名称** — 精确定位不一致来源 |
| `HasPendingModelChanges()` 返回 `true` | **输出 Critical 日志** — 说明代码实体模型与最新迁移快照不一致（实体类已修改但未执行 `dotnet ef migrations add`），需立即生成新迁移 |

> **`HasPendingModelChanges()` 为 EF Core 9 新增 API**：可检测出手工修改实体类/配置后遗漏执行 `dotnet ef migrations add` 的情况，是 EF Core 8 所不具备的模型级一致性检测能力。本项目已升级至 EF Core 9.0.14，该检查已启用。

#### 手工 DDL 修改的正确处理方式

1. **切勿直接修改数据库表结构**（`ALTER TABLE`、`DROP TABLE` 等）
2. 若已发生，通过 `dotnet ef migrations add <Name>` 生成新迁移来描述"将当前 DB 对齐到代码模型"的变更
3. 提交迁移文件，部署时 `MigrateAsync()` 自动执行

### Q：部署时数据库还未就绪怎么办？

`DatabaseInitializerHostedService` 内置 Polly 重试策略（最多 6 次，指数退避），数据库容器延迟启动时仍可稳定连接。重试耗尽后，数据库异常会记录为 `Critical` 日志；行为由 `Persistence:Migration:FailStartupOnError` 决定：

- `false`（默认）：程序继续运行（降级模式）
- `true`：程序终止启动（fail-fast）

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
- 当 `Persistence:Migration:FailStartupOnError=false` 时，数据库异常不会导致程序崩溃，最坏情况是降级运行并输出 `Critical` 日志

### Q：如何针对 SQL Server 使用迁移？

统一设计时工厂支持通过 `-- --provider SqlServer` 切换到 SQL Server 配置路径（连接字符串读取 `ConnectionStrings:SqlServer`）。使用以下命令生成 SQL Server 迁移：

```bash
# 1. 通过进程环境变量 ConnectionStrings__SqlServer 指定目标库连接字符串
# 2. 在解决方案根目录运行（工厂会自动找到 Zeye.Sorting.Hub.Host/appsettings.json）
dotnet ef migrations add <迁移名称> \
  --project Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations \
  --startup-project Zeye.Sorting.Hub.Host \
  --output-dir Migrations \
  --context SortingHubDbContext \
  -- --provider SqlServer
```

运行时 SQL Server 路径通过`Persistence:Provider = SqlServer`配置启用；MySQL迁移位于Infrastructure的`Persistence/Migrations/`，SQL Server迁移位于独立项目的`Migrations/`。

### Q：如何接入第三种数据库（如 SQLite、PostgreSQL）？

请参阅 [`新数据库提供程序接入指南.md`](./新数据库提供程序接入指南.md)，文档以 SQLite 为例，逐步说明接入新提供器时需要修改的所有文件，并附接入核查清单。
