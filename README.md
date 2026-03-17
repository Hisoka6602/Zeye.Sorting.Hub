# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .github（Copilot 仓库级指令目录）
│   ├── copilot-instructions.md（Copilot 自定义指令：禁止 UTC、统一本地时间）
│   └── workflows（CI 工作流目录）
│       └── ef-migration-validation.yml（EF 迁移验收流水线：dotnet ef list/update/script）
├── .gitattributes（Git 属性配置）
├── .gitignore（Git 忽略规则）
├── README.md（仓库总览、结构清单与维护规范）
├── Zeye.Sorting.Hub.Analytics（分析与报表子域，占位工程）
│   ├── Class1.cs（占位类，预留统计指标/报表能力）
│   └── Zeye.Sorting.Hub.Analytics.csproj（Analytics 项目定义）
├── Zeye.Sorting.Hub.Application（应用层，占位工程）
│   ├── Class1.cs（占位类，预留命令/查询/应用服务）
│   └── Zeye.Sorting.Hub.Application.csproj（Application 项目定义）
├── Zeye.Sorting.Hub.Contracts（契约层，占位工程）
│   ├── Class1.cs（占位类，预留请求/响应契约）
│   └── Zeye.Sorting.Hub.Contracts.csproj（Contracts 项目定义）
├── Zeye.Sorting.Hub.Domain（核心领域层）
│   ├── Abstractions（领域抽象接口目录）
│   │   └── IEntity.cs（实体通用接口）
│   ├── Aggregates（领域聚合目录）
│   │   └── Parcels（包裹聚合目录）
│   │       ├── Parcel.cs（包裹聚合根）
│   │       └── ValueObjects（包裹聚合值对象目录）
│   │           ├── ApiRequestInfo.cs（外部接口请求/响应信息值对象）
│   │           ├── BagInfo.cs（袋笼/集包信息值对象）
│   │           ├── BarCodeInfo.cs（条码识别信息值对象）
│   │           ├── ChuteInfo.cs（格口分配信息值对象）
│   │           ├── CommandInfo.cs（设备命令交互信息值对象）
│   │           ├── GrayDetectorInfo.cs（灰度检测结果值对象）
│   │           ├── ImageInfo.cs（图片元数据值对象）
│   │           ├── ParcelDeviceInfo.cs（包裹相关设备信息值对象）
│   │           ├── ParcelPositionInfo.cs（包裹空间/轨迹位置信息值对象）
│   │           ├── SorterCarrierInfo.cs（分拣小车/载体信息值对象）
│   │           ├── StickingParcelInfo.cs（叠包检测结果值对象）
│   │           ├── VideoInfo.cs（视频信息值对象）
│   │           ├── VolumeInfo.cs（体积信息值对象）
│   │           └── WeightInfo.cs（重量信息值对象）
│   ├── DomainEvents（领域事件目录）
│   │   └── Parcels（包裹相关领域事件目录）
│   │       ├── ParcelChuteAssignedEventArgs.cs（包裹分配格口事件参数）
│   │       └── ParcelScannedEventArgs.cs（包裹扫描事件参数）
│   ├── Enums（领域枚举目录）
│   │   ├── ActionType.cs（动作类型枚举）
│   │   ├── ActionIsolationDecision.cs（自动调优危险动作隔离决策枚举）
│   │   ├── ApiRequestStatus.cs（接口请求状态枚举）
│   │   ├── ApiRequestType.cs（接口请求类型枚举）
│   │   ├── AutoTuningClosedLoopStage.cs（自动调优闭环阶段枚举）
│   │   ├── AutoTuningUnavailableReason.cs（自动调优 unavailable 原因枚举与标签扩展）
│   │   ├── BarCodeType.cs（条码类型枚举）
│   │   ├── CommandDirection.cs（命令方向枚举）
│   │   ├── ImageCaptureType.cs（图像采集方式枚举）
│   │   ├── ImageType.cs（图像类型枚举）
│   │   ├── NoReadType.cs（无码/难码类型枚举）
│   │   ├── ParcelStatus.cs（包裹状态枚举）
│   │   ├── ParcelType.cs（包裹类别枚举）
│   │   ├── VideoNodeType.cs（视频节点类型枚举）
│   │   └── VolumeSourceType.cs（体积来源类型枚举）
│   ├── Primitives（领域基础类型目录）
│   │   └── AuditableEntity.cs（可审计实体基类）
│   ├── Repositories（领域仓储契约目录）
│   │   └── IParcelRepository.cs（包裹仓储接口）
│   └── Zeye.Sorting.Hub.Domain.csproj（Domain 项目定义）
├── Zeye.Sorting.Hub.Host（宿主层）
│   ├── HostedServices（托管服务目录）
│   │   ├── AutoTuningLoggerObservability.cs（自动调优观测默认日志实现）
│   │   ├── DatabaseAutoTuningHostedService.cs（数据库自动调谐托管服务（闭环阶段流转、执行隔离、自动验证标准化输出与回滚审计））
│   │   └── DatabaseInitializerHostedService.cs（数据库初始化与迁移托管服务）
│   ├── Program.cs（应用入口与 Host 构建流程）
│   ├── Properties（运行调试属性目录）
│   │   └── launchSettings.json（本地启动配置）
│   ├── Worker.cs（后台轮询任务示例服务）
│   ├── Zeye.Sorting.Hub.Host.csproj（Host 项目定义）
│   ├── nlog.config（NLog 日志配置：双路落盘，低开销异步写盘）
│   ├── appsettings.Development.json（开发环境配置）
│   └── appsettings.json（默认运行配置）
├── Zeye.Sorting.Hub.Host.Tests（自动调优行为测试工程）
│   ├── AutoTuningProductionControlTests.cs（自动调优生产可控能力测试：dry-run/隔离器/告警恢复/普通与严重回归/探针双路径/闭环链路）
│   └── Zeye.Sorting.Hub.Host.Tests.csproj（xUnit 测试项目定义）
├── Zeye.Sorting.Hub.Infrastructure（基础设施层）
│   ├── DependencyInjection（依赖注入扩展目录）
│   │   └── PersistenceServiceCollectionExtensions.cs（持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、性能参数读取配置、Parcel 按 CreatedTime 月分表））
│   ├── EntityConfigurations（EF Core 映射配置目录）
│   │   ├── BagInfoEntityTypeConfiguration.cs（BagInfo 映射配置）
│   │   └── ParcelEntityTypeConfiguration.cs（Parcel 映射配置）
│   ├── Persistence（持久化核心目录）
│   │   ├── AutoTuning（自动调谐核心目录）
│   │   │   ├── AutoTuningAbstractions.cs（自动调优观测抽象、标准化验证结果、隔离/回滚策略与可观测执行计划探针）
│   │   │   ├── AutoTuningConfigurationHelper.cs（配置读取公共辅助类，集中管理正整数/小数/布尔/TimeSpan 等配置解析方法）
│   │   │   ├── MySqlSessionBootstrapConnectionInterceptor.cs（MySQL 连接会话初始化拦截器）
│   │   │   ├── SlowQueryAutoTuningPipeline.cs（慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道）
│   │   │   ├── SlowQueryCommandInterceptor.cs（EF Core 慢查询采集拦截器）
│   │   │   └── SlowQuerySample.cs（慢查询采样记录模型）
│   │   ├── DatabaseDialects（数据库方言目录）
│   │   │   ├── DatabaseProviderExceptionHelper.cs（数据库异常错误码提取辅助类）
│   │   │   ├── IDatabaseDialect.cs（数据库方言接口）
│   │   │   ├── MySqlDialect.cs（MySQL 方言实现）
│   │   │   └── SqlServerDialect.cs（SQL Server 方言实现）
│   │   ├── DesignTime（EF 设计时支持目录）
│   │   │   ├── MySqlContextFactory.cs（统一设计时 DbContext 工厂，支持 --provider 切换 MySql/SqlServer）
│   │   │   └── SqlServerContextFactory.cs（SQL Server 设计时 DbContext 构建器）
│   │   ├── Migrations（EF Core 迁移文件目录）
│   │   │   ├── 20260316184030_InitialCreate.cs（初始迁移：全部表建表与回滚逻辑）
│   │   │   ├── 20260316184030_InitialCreate.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260317024345_UseAttributeBasedIndexesAndPrecision.cs（索引/精度特征标记对齐迁移）
│   │   │   ├── 20260317024345_UseAttributeBasedIndexesAndPrecision.Designer.cs（迁移元数据，自动生成）
│   │   │   └── SortingHubDbContextModelSnapshot.cs（当前模型快照，自动生成）
│   │   └── SortingHubDbContext.cs（EF Core DbContext）
│   ├── Repositories（仓储基类与结果模型目录）
│   │   ├── MemoryCacheRepositoryBase.cs（缓存仓储基类）
│   │   ├── RepositoryBase.cs（通用仓储基类）
│   │   └── RepositoryResult.cs（仓储调用结果封装模型）
│   └── Zeye.Sorting.Hub.Infrastructure.csproj（Infrastructure 项目定义）
├── Zeye.Sorting.Hub.Realtime（实时通信子域，占位工程）
│   ├── Class1.cs（占位类，预留实时推送/订阅能力）
│   └── Zeye.Sorting.Hub.Realtime.csproj（Realtime 项目定义）
├── Zeye.Sorting.Hub.RuleEngine（规则引擎子域，占位工程）
│   ├── Class1.cs（占位类，预留规则执行引擎）
│   └── Zeye.Sorting.Hub.RuleEngine.csproj（RuleEngine 项目定义）
├── Zeye.Sorting.Hub.SharedKernel（共享内核，占位工程）
│   ├── Class1.cs（占位类，预留通用基础能力）
│   └── Zeye.Sorting.Hub.SharedKernel.csproj（SharedKernel 项目定义）
├── Zeye.Sorting.Hub.sln（.NET 解决方案入口）
├── EFCore-Migration.md（EF Core CodeFirst 迁移使用说明文档）
├── EFCore9-UpgradePlan.md（EF Core 8 → 9 升级记录：已完成，EFCore 9.0.14 / Pomelo 9.0.0 / HasPendingModelChanges 守卫已集成）
├── NewDatabaseProvider-Guide.md（接入新数据库提供器（如 SQLite / PostgreSQL）的逐步操作指南）
├── Parcel属性新增操作指南.md（Parcel 聚合新增属性时的文件修改操作指南）
└── 项目完成度与推进计划.md（项目阶段评估与路线图文档）
```

## Copilot 维护规定

- 每次新增文件或删除文件后，必须同步更新本 README 的“仓库文件结构（当前）”章节，保证结构清单与仓库实际内容一致。
- 每次新增文件或删除文件后，必须同步更新本 README 的“各层级与各文件作用说明（逐项）”章节，保证职责说明与仓库实际内容一致。
- 硬性规则：全项目禁止使用 UTC 时间（如 `DateTime.UtcNow`、`DateTimeOffset.UtcNow`、`DateTimeKind.Utc`、`ToUniversalTime` 等），统一使用本地时间语义（如 `DateTime.Now`、`DateTimeKind.Local`）。

## 各层级与各文件作用说明（逐项）

### 根目录（`.`）

- `.`：解决方案根目录，承载多项目分层结构（Host、Domain、Infrastructure、Application、Contracts 等）。
- `.github/`：Copilot 仓库级指令目录。
- `.gitattributes`：Git 属性配置（如行尾规范）。
- `.gitignore`：Git 忽略规则（如 `bin/`、`obj/`、IDE 临时文件）。
- `README.md`：仓库总览、结构清单与维护规范文档。
- `Zeye.Sorting.Hub.sln`：.NET 解决方案入口，聚合全部项目。
- `Parcel属性新增操作指南.md`：当 Parcel 聚合需要新增属性时，需要修改哪些文件、如何修改的操作指南（含三种情形：主表标量属性、现有值对象属性、新增值对象）。
- `项目完成度与推进计划.md`：项目阶段评估与路线图文档。
- `EFCore-Migration.md`：EF Core CodeFirst 迁移使用说明（迁移架构总览、运行时自动迁移、CLI 命令、设计时工厂、分表与迁移关系、常见问题）。
- `EFCore9-UpgradePlan.md`：EF Core 8 → 9 升级记录（**已完成**：EF Core 9.0.14、Pomelo 9.0.0、EFCore.Sharding 9.0.10），包含已升级包清单、版本对照表、`HasPendingModelChanges()` 集成说明及核查清单。

### `.github/`：Copilot 仓库级指令目录
- `copilot-instructions.md`：Copilot 自定义指令，硬性要求禁止 UTC 时间 API，统一使用本地时间语义。

### `.github/workflows/`：CI 工作流目录
- `ef-migration-validation.yml`：EF 迁移验收流水线（MySQL 容器环境），真实执行 `dotnet ef migrations list`、`dotnet ef database update`、`dotnet ef migrations script` 三项门禁命令。

### `Zeye.Sorting.Hub.Analytics/`：分析与报表子域（当前为占位工程）
- `Zeye.Sorting.Hub.Analytics.csproj`：Analytics 项目定义。
- `Class1.cs`：占位类，预留统计指标/报表能力实现位置。

### `Zeye.Sorting.Hub.Application/`：应用层（Use Case 编排层，当前为占位工程）
- `Zeye.Sorting.Hub.Application.csproj`：Application 项目定义。
- `Class1.cs`：占位类，预留命令/查询/应用服务实现位置。

### `Zeye.Sorting.Hub.Contracts/`：契约层（对外 DTO / 接口模型，当前为占位工程）
- `Zeye.Sorting.Hub.Contracts.csproj`：Contracts 项目定义。
- `Class1.cs`：占位类，预留请求/响应契约定义位置。

### `Zeye.Sorting.Hub.Domain/`：核心领域层，存放聚合根、值对象、领域事件、枚举与仓储接口
- `Zeye.Sorting.Hub.Domain.csproj`：Domain 项目定义。

#### `Zeye.Sorting.Hub.Domain/Abstractions/`：领域抽象接口层
- `IEntity.cs`：实体通用接口（定义主键契约）。

#### `Zeye.Sorting.Hub.Domain/Aggregates/`：领域聚合目录

##### `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/`：包裹聚合目录
- `Parcel.cs`：包裹聚合根，承载包裹生命周期状态与行为。

###### `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/`：包裹聚合值对象目录
- `ApiRequestInfo.cs`：外部接口请求/响应信息值对象。
- `BagInfo.cs`：袋笼/集包信息值对象。
- `BarCodeInfo.cs`：条码识别信息值对象。
- `ChuteInfo.cs`：格口分配信息值对象。
- `CommandInfo.cs`：设备命令交互信息值对象。
- `GrayDetectorInfo.cs`：灰度检测结果值对象。
- `ImageInfo.cs`：图片元数据值对象（路径、类型、时间等）。
- `ParcelDeviceInfo.cs`：包裹相关设备信息值对象。
- `ParcelPositionInfo.cs`：包裹空间/轨迹位置信息值对象。
- `SorterCarrierInfo.cs`：分拣小车/载体信息值对象。
- `StickingParcelInfo.cs`：叠包检测结果值对象。
- `VideoInfo.cs`：视频信息值对象。
- `VolumeInfo.cs`：体积信息值对象。
- `WeightInfo.cs`：重量信息值对象。

#### `Zeye.Sorting.Hub.Domain/DomainEvents/`：领域事件目录

##### `Zeye.Sorting.Hub.Domain/DomainEvents/Parcels/`：包裹相关领域事件目录
- `ParcelChuteAssignedEventArgs.cs`：包裹分配格口事件参数（当前占位定义）。
- `ParcelScannedEventArgs.cs`：包裹扫描事件参数（当前占位定义）。

#### `Zeye.Sorting.Hub.Domain/Enums/`：领域枚举与业务语义常量目录
- `ActionType.cs`：动作类型枚举定义。
- `ActionIsolationDecision.cs`：自动调优危险动作隔离决策枚举定义。
- `ApiRequestStatus.cs`：接口请求状态枚举定义。
- `ApiRequestType.cs`：接口请求类型枚举定义。
- `AutoTuningClosedLoopStage.cs`：自动调优闭环阶段枚举定义。
- `AutoTuningUnavailableReason.cs`：自动调优 unavailable 原因枚举与统一标签映射扩展，避免自由字符串漂移。
- `BarCodeType.cs`：条码类型枚举定义。
- `CommandDirection.cs`：命令方向枚举定义。
- `ImageCaptureType.cs`：图像采集方式枚举定义。
- `ImageType.cs`：图像类型枚举定义。
- `NoReadType.cs`：无码/难码类型枚举定义。
- `ParcelStatus.cs`：包裹状态枚举定义。
- `ParcelType.cs`：包裹类别枚举定义。
- `VideoNodeType.cs`：视频节点类型枚举定义。
- `VolumeSourceType.cs`：体积来源类型枚举定义。

#### `Zeye.Sorting.Hub.Domain/Primitives/`：领域基础类型目录
- `AuditableEntity.cs`：可审计实体基类（创建/修改信息等）。

#### `Zeye.Sorting.Hub.Domain/Repositories/`：领域仓储契约目录
- `IParcelRepository.cs`：包裹仓储接口（当前为占位接口定义）。

### `Zeye.Sorting.Hub.Host/`：宿主层（程序入口、后台服务、启动配置）
- `Program.cs`：应用入口与 Host 构建流程；使用 NLog 替换默认日志提供器，任何启动期异常均记录后再退出。
- `Worker.cs`：后台轮询任务示例服务。
- `Zeye.Sorting.Hub.Host.csproj`：Host 项目定义。
- `nlog.config`：NLog 日志配置，双路落盘（`logs/app-*.log` 全量 + `logs/database-*.log` 数据库专属），低开销设计（异步队列 + keepFileOpen + optimizeBufferReuse），保留 30 天。
- `appsettings.json`：默认运行配置（包含连接字符串、持久化参数、日志级别）。
- `appsettings.Development.json`：开发环境配置覆盖文件。

#### `Zeye.Sorting.Hub.Host/HostedServices/`：启动/常驻托管服务目录
- `AutoTuningLoggerObservability.cs`：自动调优观测默认日志实现（统一日志 + 指标抽象默认落地）。
- `DatabaseAutoTuningHostedService.cs`：数据库自动调谐托管服务（显式闭环阶段迁移、执行隔离、标准化自动验证结果、回滚触发与审计日志）。
- `DatabaseInitializerHostedService.cs`：数据库初始化与迁移托管服务。

#### `Zeye.Sorting.Hub.Host/Properties/`：项目运行调试属性目录
- `launchSettings.json`：本地启动配置（Profile、环境变量等）。

### `Zeye.Sorting.Hub.Infrastructure/`：基础设施层（EF Core 持久化、仓储实现、DI 注册、数据库方言）
- `Zeye.Sorting.Hub.Infrastructure.csproj`：Infrastructure 项目定义。

#### `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/`：依赖注入扩展目录
- `PersistenceServiceCollectionExtensions.cs`：持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、性能参数读取配置、Parcel 按 CreatedTime 月分表）。

#### `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/`：EF Core 实体映射配置目录
- `BagInfoEntityTypeConfiguration.cs`：BagInfo 映射配置。
- `ParcelEntityTypeConfiguration.cs`：Parcel 聚合映射配置。

#### `Zeye.Sorting.Hub.Infrastructure/Persistence/`：持久化核心目录（DbContext、方言、设计时工厂）
- `SortingHubDbContext.cs`：EF Core DbContext（实体集与模型构建入口）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/`：数据库方言抽象与实现目录
- `DatabaseProviderExceptionHelper.cs`：数据库异常错误码提取辅助类。
- `IDatabaseDialect.cs`：数据库方言抽象接口。
- `MySqlDialect.cs`：MySQL 方言实现。
- `SqlServerDialect.cs`：SQL Server 方言实现。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/`：自动调谐核心目录
- `AutoTuningAbstractions.cs`：自动调优观测抽象、闭环阶段模型、危险动作隔离策略、自动回滚决策、标准化验证结果构造器与可观测执行计划探针。
- `AutoTuningConfigurationHelper.cs`：配置读取公共辅助类，集中提供 `GetPositiveIntOrDefault`、`GetNonNegativeIntOrDefault`、`GetNonNegativeDecimalOrDefault`、`GetDecimalInRangeOrDefault`、`GetDecimalClampedOrDefault`、`GetBoolOrDefault`、`GetPositiveSecondsAsTimeSpanOrDefault`、`GetTimeOfDayOrDefault` 共八个方法，消除三处影分身副本。
- `MySqlSessionBootstrapConnectionInterceptor.cs`：MySQL 连接会话初始化拦截器。
- `SlowQueryAutoTuningPipeline.cs`：慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道。
- `SlowQueryCommandInterceptor.cs`：EF Core 慢查询采集拦截器。
- `SlowQuerySample.cs`：慢查询采样记录模型。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/`：EF 设计时支持目录
- `MySqlContextFactory.cs`：MySQL 设计时 DbContext 工厂（实现 `IDesignTimeDbContextFactory<SortingHubDbContext>`），供 `dotnet ef migrations add/remove/list` 等 CLI 命令在无宿主进程时构建 DbContext；连接字符串从 `appsettings.json` 的 `ConnectionStrings:MySql` 读取，版本采用 AutoDetect → 兜底 8.0 两级策略。
- `SqlServerContextFactory.cs`：SQL Server 设计时 DbContext 构建器（由统一设计时工厂按 provider 分发调用），提供 SQL Server 连接字符串搜索与 `DbContextOptions` 组装能力。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/`：EF Core 迁移文件目录
- `20260316184030_InitialCreate.cs`：初始迁移，包含全部表（Parcels、Bags 及各值对象属性表）的 `Up`（建表）与 `Down`（回滚）逻辑。
- `20260316184030_InitialCreate.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260317024345_UseAttributeBasedIndexesAndPrecision.cs`：索引/精度特征标记对齐迁移（空 `Up/Down`，用于同步模型快照）。
- `20260317024345_UseAttributeBasedIndexesAndPrecision.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `SortingHubDbContextModelSnapshot.cs`：当前模型快照，EF Core 用于计算下次迁移的差量（自动生成，勿手动修改）。

#### `Zeye.Sorting.Hub.Infrastructure/Repositories/`：仓储基类与结果模型目录
- `MemoryCacheRepositoryBase.cs`：带内存缓存失效逻辑的仓储基类。
- `RepositoryBase.cs`：通用仓储基类（增删改查 + 自动持久化实现）。
- `RepositoryResult.cs`：仓储调用结果封装模型。

### `Zeye.Sorting.Hub.Realtime/`：实时通信子域（当前为占位工程）
- `Zeye.Sorting.Hub.Realtime.csproj`：Realtime 项目定义。
- `Class1.cs`：占位类，预留实时推送/订阅能力实现位置。

### `Zeye.Sorting.Hub.RuleEngine/`：规则引擎子域（当前为占位工程）
- `Zeye.Sorting.Hub.RuleEngine.csproj`：RuleEngine 项目定义。
- `Class1.cs`：占位类，预留规则执行引擎实现位置。

### `Zeye.Sorting.Hub.SharedKernel/`：跨模块共享内核（当前为占位工程）
- `Zeye.Sorting.Hub.SharedKernel.csproj`：SharedKernel 项目定义。
- `Class1.cs`：占位类，预留通用基础能力实现位置。

### `Zeye.Sorting.Hub.Host.Tests/`：自动调优测试层
- `Zeye.Sorting.Hub.Host.Tests.csproj`：xUnit 测试项目定义。
- `AutoTuningProductionControlTests.cs`：覆盖 dry-run、危险动作隔离、告警防抖与恢复、普通/严重回归、unavailable 指标处理、执行计划探针 available/unavailable 双路径与闭环链路。

## 本次更新内容（新增 Parcel 属性操作指南文档）

1. **新增 `Parcel属性新增操作指南.md`**：详细说明当 Parcel 聚合需要新增属性时，需要修改哪些文件及如何修改。文档涵盖三种情形：①在聚合根主表新增标量属性（修改 `Parcel.cs` + `ParcelEntityTypeConfiguration.cs` + 执行迁移）；②在现有值对象中新增属性（修改对应值对象文件 + `ParcelEntityTypeConfiguration.cs` + 执行迁移）；③新增全新值对象（新建值对象文件 + 修改 `Parcel.cs` + 修改 `ParcelEntityTypeConfiguration.cs` + 更新 `README.md` + 执行迁移）。附完整检查清单与当前 Parcel 主表字段一览。

## 本次更新内容（代码质量审查与缺陷修复）

1. **RepositoryBase（逻辑 Bug 修复）**：`AddAsync`/`AddRangeAsync`/`UpdateAsync`/`RemoveAsync` 原本在创建 `DbContext` 后不调用 `SaveChangesAsync` 即释放，所有变更静默丢失。现已在每个变更操作结束前正确调用 `await db.SaveChangesAsync(cancellationToken)`，确保数据持久化。
2. **SlowQueryAutoTuningPipeline.Collect（死代码移除）**：`while` 循环结束后的 `if (_slowQueries.Count >= _maxQueueSize)` 分支永远不可达（循环保证退出时队列 Count < max），已移除该冗余分支。
3. **AutoTuningClosedLoopTracker（内存泄漏修复）**：`_stages` 列表在长时运行场景下无上限增长，每天可累积数万条记录。现增加 `MaxStageHistory = 1000` 上限，超出时从列表头部淘汰最旧记录，防止内存泄漏。
4. **Parcel（注释与错误消息对调修复）**：`TargetChuteId` 的 XML 注释为"实际落格 Id"（实为"目标格口"）、`ActualChuteId` 的注释为"理论落格 Id"（实为"实际落格"），两者语义相反；`Create()` 工厂方法中对应的校验错误消息同样互换。现已修正为正确语义："目标格口 Id（系统路由分配的理论落格位置）"与"实际落格 Id（包裹实际到达的格口位置）"。
5. **Parcel.Create（CreatedTime 未初始化修复）**：`AuditableEntity.CreatedTime` 为 `protected set` 属性，但 `Parcel.Create()` 工厂方法从未赋值，导致所有新建包裹的 `CreatedTime` 永远为 `DateTime.MinValue`。现已在工厂方法中补充 `CreatedTime = DateTime.Now` 赋值。
6. **SlowQueryAutoTuningPipeline.GetDecimalOrDefault（文化敏感与语义修复）**：原方法使用不带 `CultureInfo` 的 `decimal.TryParse`（文化敏感），在非英文系统可能解析失败；且 `parsed > 0` 条件错误拒绝 0 值，与同类方法（`GetNonNegativeDecimalOrDefault`）行为不一致。现已修正为 `NumberStyles.Number, CultureInfo.InvariantCulture` 且条件改为 `>= 0m`。

## 本次更新内容（代码质量全面审查——逻辑/性能/冗余/死代码/影分身）

1. **影分身代码消除（Issue #3）**：新增 `AutoTuningConfigurationHelper.cs`，将 `GetPositiveIntOrDefault`、`GetNonNegativeIntOrDefault`、`GetNonNegativeDecimalOrDefault`、`GetDecimalInRangeOrDefault`、`GetDecimalClampedOrDefault`、`GetBoolOrDefault`、`GetPositiveSecondsAsTimeSpanOrDefault`、`GetTimeOfDayOrDefault` 等八个配置辅助方法统一集中，彻底消除原先散落在 `SlowQueryAutoTuningPipeline.cs`、`DatabaseAutoTuningHostedService.cs`、`PersistenceServiceCollectionExtensions.cs` 三处的相同副本（影分身代码）。
2. **冗余同步机制消除（Issue #1 性能/冗余）**：`SlowQueryAutoTuningPipeline` 的 `_slowQueries` 字段原为 `ConcurrentQueue<SlowQuerySample>`，但所有读写路径均已包裹在 `lock (_queueSync)` 内，并发队列的无锁内部同步完全冗余。改为 `Queue<SlowQuerySample>` 并保留现有锁，消除双重同步开销，代码意图更清晰。同步移除因此不再需要的 `using System.Collections.Concurrent` 与 `using System.Globalization` 死引用。
3. **异步资源管理修复（Issue #2）**：`DatabaseAutoTuningHostedService.ExecuteSqlAsync` 使用 `using var scope = _scopeFactory.CreateScope()` 做同步释放，若 DI 容器中注册了 `IAsyncDisposable` 服务则无法正确异步销毁。改为 `await using var scope = _scopeFactory.CreateAsyncScope()`，确保异步资源完整释放。
4. **容量预测逻辑解耦（Issue #4 逻辑）**：`UpdateAutonomousSignals` 在 `_enableFullAutomation = false` 时直接返回，导致 `_enableCapacityPrediction = true` 的容量趋势预警被静默跳过，两个独立功能错误地相互绑定。重构后：表热度更新仅受 `_enableFullAutomation` 控制，容量快照与趋势告警独立受 `_enableCapacityPrediction` 控制，两者互不干扰。
5. **死代码清理（Issue #5）**：`RepositoryBase.cs` 中 `using System.Text` 从未被任何代码引用，已移除。

## 后续可继续完善项

1. 接入真实数据库执行计划视图（MySQL `EXPLAIN ANALYZE` / SQL Server Query Store）替代默认日志探针，减少 unavailable 占比。
2. 将自动验证 snapshot diff 输出落地到结构化审计表（而非仅日志），支持长周期追踪与可视化报表。
3. 引入按表/按业务域的动态阈值学习（结合历史分位数），降低统一阈值在不同负载模型下的误报率。
4. 为闭环动作增加端到端压测回放（离线流量）验证门禁，进一步提升生产变更安全性。
5. `IParcelRepository`、`ParcelScannedEventArgs`、`ParcelChuteAssignedEventArgs` 等占位类后续应补充完整实现。

## 本次更新内容（EF Core CodeFirst 迁移完成 + 数据库日志落盘 + SqlServer 设计时支持）

1. **`MySqlContextFactory` 实现（`IDesignTimeDbContextFactory<SortingHubDbContext>`）**：将原本的空占位类补充为完整的设计时工厂实现，连接字符串从 `appsettings.json` 的 `ConnectionStrings:MySql` 读取（按目录树搜索），版本采用 AutoDetect → 兜底 8.0 两级策略。
2. **`SqlServerContextFactory` 新建**：新增 SQL Server 设计时工厂，连接字符串从 `appsettings.json` 的 `ConnectionStrings:SqlServer` 读取，确保两种数据库均可通过 `dotnet ef` CLI 生成迁移。
3. **初始迁移 `InitialCreate` 生成**：执行 `dotnet ef migrations add InitialCreate` 生成三个迁移文件（`20260316184030_InitialCreate.cs`、`20260316184030_InitialCreate.Designer.cs`、`SortingHubDbContextModelSnapshot.cs`），覆盖全部实体表（Parcels 主表及 14 个值对象属性表），建表与回滚逻辑完整。
4. **NLog 双路日志落盘**：使用 NLog 替换 Serilog，`logs/app-*.log` 记录全量日志，`logs/database-*.log` 记录数据库专属日志（EF Core 迁移、`DatabaseInitializerHostedService`、`DatabaseAutoTuningHostedService`、Persistence 层），按天归档，保留 30 天。低开销设计：异步队列 + keepFileOpen + optimizeBufferReuse。任何数据库异常均记录，不导致程序崩溃。
5. **CodeFirst 迁移一致性守卫（`AssertMigrationConsistencyAsync`）**：每次启动后检测未应用迁移与迁移历史差异，输出具体差异的迁移名称（在代码中但未应用 / 已应用但代码中不存在），不抛出异常，不阻止程序运行。
6. **`EFCore-Migration.md` 新增**：创建迁移使用说明文档，涵盖迁移架构总览、运行时自动迁移流程、CLI 命令速查、设计时工厂说明、分表与迁移的职责分离说明、数据库日志落盘说明及常见问题。
7. **`NewDatabaseProvider-Guide.md` 新增**：以 SQLite 为例，逐步说明接入第三种数据库提供器时需修改的文件（NuGet 包、方言实现、设计时工厂、DI 注册、appsettings、迁移生成），并附接入核查清单与常见注意事项。
8. **`README.md` 同步更新**：文件树与逐项说明反映上述所有新增文件。

## 本次更新内容（ORM 特征标记、appsettings 真实连接参数、EF Core 9 升级计划）

1. **appsettings.json 连接字符串改为真实参数格式**：`ConnectionStrings:MySql` / `ConnectionStrings:SqlServer` 使用本地开发默认账密（`root`/`Admin@1234`、`sa`/`Admin@1234`），与设计时工厂 Fallback 值一致；说明注释更新为"私有库，由专属技术人员维护"。
2. **数据模型添加 `[MaxLength]` ORM 特征标记**：在 `AuditableEntity`、`Parcel` 聚合根及所有值对象（`BagInfo`、`BarCodeInfo`、`WeightInfo`、`VolumeInfo`、`ParcelDeviceInfo`、`GrayDetectorInfo`、`StickingParcelInfo`、`ApiRequestInfo`、`CommandInfo`、`ImageInfo`、`VideoInfo`）的字符串属性上添加 `System.ComponentModel.DataAnnotations.MaxLength` 特征标记，无需在 Domain 层引入 EF Core 依赖。
3. **实体配置精简**：`ParcelEntityTypeConfiguration`（468 行 → 203 行）和 `BagInfoEntityTypeConfiguration` 移除所有冗余的 `HasColumnName()`（列名与属性名相同）、`IsRequired()`（非可空类型自动推断）、`HasMaxLength()`（已由 Domain 层特征标记承担）配置，仅保留 EF Core 专属配置（影子属性、关系、索引、表名）。`decimal` 精度现统一由 Domain 层 `[Precision(18,3)]` 特征标记声明（需 `Microsoft.EntityFrameworkCore.Abstractions` 依赖）。
4. **`EFCore9-UpgradePlan.md` 新增**：详细说明 EF Core 8 → 9 的升级计划，包含可行性结论（EF Core 9 支持 .NET 8，无需升级运行时框架）、受影响 NuGet 包列表、升级步骤、`HasPendingModelChanges()` 守卫增强代码示例、重要变更说明及回滚方案。


## 本次更新内容（EF Core 9 升级完成 + HasPendingModelChanges() 守卫增强 + ORM 特征标记精简）

1. **EF Core 9 升级完成**：`Zeye.Sorting.Hub.Infrastructure.csproj` 中所有 EF Core 相关包升级至 9.0.14（EF Core 核心 + SqlServer + Design），Pomelo 升级至 9.0.0，EFCore.Sharding 升级至 9.0.10；同时显式覆盖 `Pomelo.EntityFrameworkCore.MySql.NetTopologySuite 9.0.0` 消除传递依赖版本警告。运行时框架仍为 `.NET 8`，无需升级框架。
2. **`HasPendingModelChanges()` 守卫集成**：`DatabaseInitializerHostedService.AssertMigrationConsistencyAsync()` 新增第三项检查：调用 EF Core 9 专属 API `db.Database.HasPendingModelChanges()`，检测代码实体模型是否存在尚未通过 `dotnet ef migrations add` 生成迁移的变更。一旦检测到差异，立即输出 Critical 日志，提示执行 `dotnet ef migrations add` 对齐模型。
3. **迁移快照 `ProductVersion` 更新**：`SortingHubDbContextModelSnapshot.cs` 与 `20260316184030_InitialCreate.Designer.cs` 的 `ProductVersion` 注解从 `8.0.23` 更新至 `9.0.14`，与安装的 EF Core 版本保持一致。
4. **`EFCore9-UpgradePlan.md` 更新**：状态从"计划"更新为"✅ 已完成"，附实际升级前后版本对照表，第三项守卫检查代码示例替换为实际集成代码，核查清单全部标注为已完成。
5. **`EFCore-Migration.md` 更新**：CodeFirst 守卫说明从"两项检查"更新为"三项检查（EF Core 9）"，新增 `HasPendingModelChanges()` 说明行，移除"EF Core 8 局限性"提示。
6. **`[Precision(18,3)]` 精度特征标记收敛**：所有 `decimal` 字段（`Parcel`、`VolumeInfo`、`WeightInfo`、`SorterCarrierInfo`、`ParcelPositionInfo` 共 24 个属性）统一改为 EF Core 原生 `[Precision(18,3)]` 标注；`ParcelEntityTypeConfiguration` 移除全部 `HasPrecision(18, 3)` 调用，配置文件进一步精简，迁移快照与 Designer.cs 同步更新。

## 本次更新内容（SQL Server 迁移策略澄清 + 发布策略澄清 + EF 验收流水线）

1. **设计时工厂冲突修复**：保留单一 `IDesignTimeDbContextFactory<SortingHubDbContext>` 入口（`MySqlContextFactory`），支持通过 `-- --provider SqlServer` 切换到 SQL Server，避免双工厂导致 `dotnet ef` 枚举 `DbContext` 时报键冲突。
2. **SQL Server 迁移策略明确**：`EFCore-Migration.md` 明确当前采用“单迁移目录（同程序集）”策略，迁移统一放在 `Persistence/Migrations/`；通过 `-- --provider SqlServer` / `-- --provider MySql` 区分执行路径。
3. **发布策略明确**：文档明确“运行时迁移失败不阻断启动（降级运行）”，但“发布前 EF 验收未通过则阻断发布”。
4. **新增 CI 门禁工作流**：新增 `.github/workflows/ef-migration-validation.yml`，在 MySQL 容器上真实执行 `dotnet ef migrations list` / `dotnet ef database update` / `dotnet ef migrations script`。
5. **索引与精度特征标记收敛**：`Parcel`、`BagInfo` 索引改为类级别 `[Index]` 声明，`decimal(18,3)` 统一改为 `[Precision(18,3)]`；同步生成 `UseAttributeBasedIndexesAndPrecision` 迁移以消除 PendingModelChanges。

## 后续可完善点（迁移治理）

1. 若后续 SQL Server 与 MySQL 的迁移演进差异持续扩大，可升级为“独立迁移程序集”策略，进一步降低跨提供器误用风险。
2. 在流水线上增加迁移脚本归档（artifact）与 DBA 审批节点，形成可追溯发布审计链路。
