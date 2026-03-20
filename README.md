# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .github（Copilot 仓库级指令目录）
│   ├── copilot-instructions.md（Copilot 自定义指令：禁止 UTC、统一本地时间）
│   └── workflows（CI 工作流目录）
│       └── ef-migration-validation.yml（EF 迁移验收流水线：MySQL+SQL Server 双 Provider 执行 dotnet ef list/update/script）
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
│   ├── Events（领域事件载荷目录）
│   │   └── Parcels（包裹相关领域事件载荷目录）
│   │       ├── ParcelChuteAssignedEventArgs.cs（包裹分配格口事件载荷）
│   │       └── ParcelScannedEventArgs.cs（包裹扫描事件载荷）
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
│   │   ├── ParcelExceptionType.cs（包裹异常类型枚举）
│   │   ├── ParcelStatus.cs（包裹状态枚举）
│   │   ├── ParcelType.cs（包裹类别枚举）
│   │   ├── VideoNodeType.cs（视频节点类型枚举）
│   │   └── VolumeSourceType.cs（体积来源类型枚举）
│   ├── Primitives（领域基础类型目录）
│   │   └── AuditableEntity.cs（可审计实体基类）
│   ├── Repositories（领域仓储契约目录）
│   │   ├── IParcelRepository.cs（包裹仓储接口）
│   │   └── Models（Parcel 仓储查询与分页模型目录）
│   │       ├── Filters（查询过滤模型目录）
│   │       │   └── ParcelQueryFilter.cs（Parcel 查询过滤模型）
│   │       ├── Paging（通用分页模型目录）
│   │       │   ├── PageRequest.cs（通用分页请求模型）
│   │       │   └── PageResult.cs（通用分页结果模型）
│   │       ├── ReadModels（查询读模型目录）
│   │       │   └── ParcelSummaryReadModel.cs（Parcel 列表摘要读模型）
│   │       ├── Results（仓储结果模型目录）
│   │       │   └── RepositoryResult.cs（仓储统一结果模型，含泛型版本）
│   │       └── Validation（查询校验模型目录）
│   │           └── MaxTimeRangeAttribute.cs（查询时间跨度限制特性，默认不超过 3 个月）
│   └── Zeye.Sorting.Hub.Domain.csproj（Domain 项目定义）
├── Zeye.Sorting.Hub.Host（宿主层）
│   ├── Enums（宿主层枚举目录）
│   │   └── MigrationFailureMode.cs（数据库迁移失败策略枚举：FailFast/Degraded，含 Description）
│   ├── HostedServices（托管服务目录）
│   │   ├── AutoTuningLoggerObservability.cs（自动调优观测默认日志实现）
│   │   ├── DatabaseAutoTuningHostedService.cs（数据库自动调谐托管服务（闭环阶段流转、执行隔离、自动验证标准化输出与回滚审计；分表命中/跨表占比/热点倾斜改为全量慢 SQL 口径，并在自动索引建议前做覆盖/重复/低价值过滤））
│   │   └── DatabaseInitializerHostedService.cs（数据库初始化与迁移托管服务（含分表治理基线、Runbook 审计、PerDay 手工预建窗口守卫；新增“配置清单 + 物理分表存在性”双重校验））
│   ├── Program.cs（应用入口与 Host 构建流程）
│   ├── Properties（运行调试属性目录）
│   │   └── launchSettings.json（本地启动配置）
│   ├── Worker.cs（后台轮询任务示例服务）
│   ├── Zeye.Sorting.Hub.Host.csproj（Host 项目定义）
│   ├── nlog.config（NLog 日志配置：双路落盘，低开销异步写盘）
│   ├── appsettings.Development.json（开发环境配置）
│   └── appsettings.json（默认运行配置（含分表策略结构化 Observation 与 PerDay 预建日期清单示例））
├── Zeye.Sorting.Hub.Host.Tests（自动调优行为测试工程）
│   ├── AutoTuningProductionControlTests.cs（自动调优生产可控能力测试：dry-run/隔离器/告警恢复/普通与严重回归/探针双路径/闭环链路；含分表策略评估与 PerDay 预建守卫联动测试）
│   ├── ParcelRepositoryTests.cs（Parcel 仓储第一阶段能力测试：分页过滤、详情与邻近查询、写操作与过期清理）
│   └── Zeye.Sorting.Hub.Host.Tests.csproj（xUnit 测试项目定义）
├── Zeye.Sorting.Hub.Infrastructure（基础设施层）
│   ├── DependencyInjection（依赖注入扩展目录）
│   │   └── PersistenceServiceCollectionExtensions.cs（持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、分表规则与覆盖守卫；Parcel 主表始终按 CreatedTime 路由，时间/容量/混合策略决策由统一评估器驱动））
│   ├── EntityConfigurations（EF Core 映射配置目录）
│   │   ├── BagInfoEntityTypeConfiguration.cs（BagInfo 映射配置）
│   │   └── ParcelEntityTypeConfiguration.cs（Parcel 映射配置）
│   ├── Persistence（持久化核心目录）
│   │   ├── AutoTuning（自动调谐核心目录）
│   │   │   ├── AutoTuningAbstractions.cs（自动调优观测抽象、标准化验证结果、隔离/回滚策略与可观测执行计划探针）
│   │   │   ├── AutoTuningConfigurationHelper.cs（配置读取与本地时间语义归一化/配置键拼装公共辅助类，统一 AutoTuning 键名与时间语义）
│   │   │   ├── MySqlSessionBootstrapConnectionInterceptor.cs（MySQL 连接会话初始化拦截器，直连类型判断，无额外转发）
│   │   │   ├── SlowQueryAutoTuningPipeline.cs（慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道；新增主表提取公共方法供 AutoTuning 主链路复用）
│   │   │   ├── SlowQueryCommandInterceptor.cs（EF Core 慢查询采集拦截器）
│   │   │   └── SlowQuerySample.cs（慢查询采样记录模型）
│   │   ├── Sharding（分表策略与治理决策目录）
│   │   │   ├── ParcelShardingStrategyEvaluator.cs（Parcel 分表策略评估器：配置解析、结构化校验、容量观测输入收敛、阈值决策、finer-granularity 扩展规划与统一决策快照）
│   │   │   └── Enums（分表策略枚举目录）
│   │   │       ├── ParcelFinerGranularityMode.cs（PerDay 仍过热时下一层细粒度模式枚举：None/PerHour/BucketedPerDay）
│   │   │       ├── ParcelFinerGranularityPlanLifecycle.cs（finer-granularity 扩展规划生命周期枚举：PlanOnly/AlertOnly/FutureExecutable）
│   │   │       ├── ParcelAggregateShardingRuleKind.cs（Parcel 聚合分表规则类别枚举：Date/Hash）
│   │   │       ├── ParcelShardingStrategyMode.cs（分表模式枚举：Time/Volume/Hybrid）
│   │   │       ├── ParcelTimeShardingGranularity.cs（时间粒度枚举：PerMonth/PerDay）
│   │   │       └── ParcelVolumeThresholdAction.cs（容量阈值动作枚举：AlertOnly/SwitchToPerDay）
│   │   ├── DatabaseDialects（数据库方言目录）
│   │   │   ├── DatabaseProviderExceptionHelper.cs（数据库异常错误码提取与方言共享索引构造辅助类）
│   │   │   ├── IDatabaseDialect.cs（数据库方言接口）
│   │   │   ├── IShardingPhysicalTableProbe.cs（分表物理表存在性探测接口，仅负责“表是否存在”）
│   │   │   ├── IBatchShardingPhysicalTableProbe.cs（批量分表物理表存在性探测接口，支持一次性探测多张表是否存在）
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
│   │   │   ├── 20260317062930_SplitParcelStatusAndExceptionType.cs（Parcel 状态拆分与异常类型字段迁移）
│   │   │   ├── 20260317062930_SplitParcelStatusAndExceptionType.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260318024421_OptimizeParcelAggregateQueryIndexes.cs（Parcel 聚合高频查询索引优化迁移）
│   │   │   ├── 20260318024421_OptimizeParcelAggregateQueryIndexes.Designer.cs（迁移元数据，自动生成）
│   │   │   └── SortingHubDbContextModelSnapshot.cs（当前模型快照，自动生成）
│   │   └── SortingHubDbContext.cs（EF Core DbContext）
│   ├── Repositories（仓储基类与结果模型目录）
│   │   ├── MemoryCacheRepositoryBase.cs（缓存仓储基类）
│   │   ├── ParcelRepository.cs（Parcel 仓储第一阶段实现）
│   │   └── RepositoryBase.cs（通用仓储基类）
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
├── 数据库读写压力测试计划.md（MySQL + EFCore.Sharding 分表架构的读写压测方案与验收模板）
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
- `NewDatabaseProvider-Guide.md`：新数据库提供器接入指南（MySQL / SQL Server 切换、设计时工厂、方言扩展点）。
- `数据库读写压力测试计划.md`：针对 MySQL + EFCore.Sharding 分表架构的数据库读写压力测试计划，覆盖纯写入、纯读取、混合读写、长时稳定性 4 大场景，含梯度加压方案、通过/失败验收矩阵、监控采集命令与结果记录模板。

### `.github/`：Copilot 仓库级指令目录
- `copilot-instructions.md`：Copilot 自定义指令，硬性要求禁止 UTC 时间 API，统一使用本地时间语义。

### `.github/workflows/`：CI 工作流目录
- `ef-migration-validation.yml`：EF 迁移验收流水线（MySQL + SQL Server 容器环境），真实执行 `dotnet ef migrations list`、`dotnet ef database update`、`dotnet ef migrations script` 三项门禁命令。

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

#### `Zeye.Sorting.Hub.Domain/Events/`：领域事件载荷目录

##### `Zeye.Sorting.Hub.Domain/Events/Parcels/`：包裹相关领域事件载荷目录
- `ParcelChuteAssignedEventArgs.cs`：包裹分配格口事件载荷（`readonly record struct`，不可变值语义）。
- `ParcelScannedEventArgs.cs`：包裹扫描事件载荷（`readonly record struct`，不可变值语义）。

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
- `ParcelExceptionType.cs`：包裹异常类型枚举定义（分拣异常细分原因）。
- `ParcelStatus.cs`：包裹状态枚举定义。
- `ParcelType.cs`：包裹类别枚举定义。
- `VideoNodeType.cs`：视频节点类型枚举定义。
- `VolumeSourceType.cs`：体积来源类型枚举定义。

#### `Zeye.Sorting.Hub.Domain/Primitives/`：领域基础类型目录
- `AuditableEntity.cs`：可审计实体基类（创建/修改信息等）。

#### `Zeye.Sorting.Hub.Domain/Repositories/`：领域仓储契约目录
- `IParcelRepository.cs`：包裹仓储接口（第一阶段可落地契约：基础读写、分页查询、邻近查询）。

##### `Zeye.Sorting.Hub.Domain/Repositories/Models/`：Parcel 仓储查询模型目录

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/`：查询过滤模型目录
- `ParcelQueryFilter.cs`：Parcel 第一阶段列表过滤参数模型（BagCode、WorkstationName、Status、Chute、扫码时间范围等），并通过特性限制时间跨度默认不超过 3 个月。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/`：通用分页模型目录
- `PageRequest.cs`：通用分页请求参数（含页码/页大小归一化）。
- `PageResult.cs`：通用分页结果模型（Items、页码、页大小、总数）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/`：查询读模型目录
- `ParcelSummaryReadModel.cs`：Parcel 列表摘要读模型（包含 Parcel 全部扁平化字段，用于分页列表）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/`：仓储结果模型目录
- `RepositoryResult.cs`：仓储统一结果模型（`RepositoryResult` / `RepositoryResult<T>`），供 Domain 契约与 Infrastructure 实现共用，避免重复类型。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Validation/`：查询校验模型目录
- `MaxTimeRangeAttribute.cs`：时间范围校验特性（限制起止时间跨度，默认不超过 3 个月）。

### `Zeye.Sorting.Hub.Host/`：宿主层（程序入口、后台服务、启动配置）
- `Program.cs`：应用入口与 Host 构建流程；使用 NLog 替换默认日志提供器，任何启动期异常均记录后再退出。
- `Worker.cs`：后台轮询任务示例服务。
- `Zeye.Sorting.Hub.Host.csproj`：Host 项目定义。
- `nlog.config`：NLog 日志配置，双路落盘（`logs/app-*.log` 全量 + `logs/database-*.log` 数据库专属），低开销设计（异步队列 + keepFileOpen + optimizeBufferReuse），保留 30 天。
- `appsettings.json`：默认运行配置（包含连接字符串、迁移失败策略分环境配置、分表治理守卫、Time/Volume/Hybrid 双策略配置、结构化容量观测入口 Observation、PerDay 预建日期清单、结构化扩容计划、日志级别与自动调优参数）。
- `appsettings.Development.json`：开发环境配置覆盖文件。

#### `Zeye.Sorting.Hub.Host/Enums/`：宿主层枚举目录
- `MigrationFailureMode.cs`：数据库迁移失败策略枚举（`FailFast` / `Degraded`，带 `Description` 标记），供初始化服务与测试复用，避免枚举内嵌导致扩展困难。

#### `Zeye.Sorting.Hub.Host/HostedServices/`：启动/常驻托管服务目录
- `AutoTuningLoggerObservability.cs`：自动调优观测默认日志实现（统一日志 + 指标抽象默认落地）。
- `DatabaseAutoTuningHostedService.cs`：数据库自动调谐托管服务（显式闭环阶段迁移、执行隔离、标准化自动验证结果、回滚触发与审计日志；分表观测指标基于全量慢 SQL 解析并覆盖子查询/集合运算场景；自动索引建议在执行前统一执行覆盖、语义重复、低价值过滤）。
- `DatabaseInitializerHostedService.cs`：数据库初始化与迁移托管服务（支持生产/非生产迁移失败策略分流：FailFast/Degraded；启动期执行分表治理程序化守卫，新增 Time/Volume/Hybrid 策略配置校验与审计输出；PerDay 手工预建守卫升级为“配置日期清单完整性 + 物理分表存在性”双重校验，且仅做探测/阻断/审计，不自动建表）。

#### `Zeye.Sorting.Hub.Host/Properties/`：项目运行调试属性目录
- `launchSettings.json`：本地启动配置（Profile、环境变量等）。

### `Zeye.Sorting.Hub.Infrastructure/`：基础设施层（EF Core 持久化、仓储实现、DI 注册、数据库方言）
- `Zeye.Sorting.Hub.Infrastructure.csproj`：Infrastructure 项目定义。

#### `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/`：依赖注入扩展目录
- `PersistenceServiceCollectionExtensions.cs`：持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、Parcel 主表保持按 `CreatedTime` 分表；分表时间粒度由 Time/Volume/Hybrid 统一策略决策驱动，Parcel 关联值对象规则继续复用声明式清单与覆盖守卫）。

#### `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/`：EF Core 实体映射配置目录
- `BagInfoEntityTypeConfiguration.cs`：BagInfo 映射配置。
- `ParcelEntityTypeConfiguration.cs`：Parcel 聚合映射配置。

#### `Zeye.Sorting.Hub.Infrastructure/Persistence/`：持久化核心目录（DbContext、方言、设计时工厂）
- `SortingHubDbContext.cs`：EF Core DbContext（实体集与模型构建入口）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/`：数据库方言抽象与实现目录
- `DatabaseProviderExceptionHelper.cs`：数据库异常错误码提取与方言共享索引列归一化/索引名构造辅助类。
- `IDatabaseDialect.cs`：数据库方言抽象接口。
- `IShardingPhysicalTableProbe.cs`：分表物理表存在性探测抽象（最小职责：判断目标物理表是否存在）。
- `IBatchShardingPhysicalTableProbe.cs`：分表物理表批量探测抽象（最小职责：一次性返回缺失物理表集合）。
- `MySqlDialect.cs`：MySQL 方言实现（自动调优 SQL + INFORMATION_SCHEMA.TABLES 物理分表探测）。
- `SqlServerDialect.cs`：SQL Server 方言实现（自动调优 SQL + sys.tables/sys.schemas 物理分表探测）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/`：自动调谐核心目录
- `AutoTuningAbstractions.cs`：自动调优观测抽象、闭环阶段模型、危险动作隔离策略、自动回滚决策、标准化验证结果构造器与可观测执行计划探针（含 provider-aware 真实 probe 扩展约定，默认实现仍 logging-only）。
- `AutoTuningConfigurationHelper.cs`：配置读取公共辅助类，集中提供 `GetPositiveIntOrDefault`、`GetNonNegativeIntOrDefault`、`GetNonNegativeDecimalOrDefault`、`GetDecimalInRangeOrDefault`、`GetDecimalClampedOrDefault`、`GetBoolOrDefault`、`GetPositiveSecondsAsTimeSpanOrDefault`、`GetTimeOfDayOrDefault`，并统一 `BuildAutoTuningKey`、`BuildAutonomousKey` 与 `NormalizeToLocalTime`，消除重复键拼装与时间归一化实现。
- `MySqlSessionBootstrapConnectionInterceptor.cs`：MySQL 连接会话初始化拦截器（类型判断逻辑内联，移除无意义 helper）。
- `SlowQueryAutoTuningPipeline.cs`：慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道（配置键拼装复用 `AutoTuningConfigurationHelper`，并提供主表提取公共方法供 HostedService 与建议编排共用）。
- `SlowQueryCommandInterceptor.cs`：EF Core 慢查询采集拦截器。
- `SlowQuerySample.cs`：慢查询采样记录模型。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/`：分表策略与治理决策目录
- `ParcelShardingStrategyEvaluator.cs`：Parcel 分表策略评估器（分表模式/时间粒度/容量阈值/阈值动作配置解析，结构化校验，容量观测输入统一收敛为 Observation 对象，输出含 finer-granularity 扩展规划的统一决策结果，复用于注册入口与启动审计守卫）。

###### `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/Enums/`：分表策略枚举目录
- `ParcelFinerGranularityMode.cs`：PerDay 仍过热时下一层细粒度模式枚举（`None` / `PerHour` / `BucketedPerDay`，含 `Description`）。
- `ParcelFinerGranularityPlanLifecycle.cs`：finer-granularity 扩展规划生命周期枚举（`PlanOnly` / `AlertOnly` / `FutureExecutable`，含 `Description`）。
- `ParcelAggregateShardingRuleKind.cs`：Parcel 聚合分表规则类别枚举（`Date` / `Hash`，含 `Description`），用于区分日期分表规则与哈希分表规则。
- `ParcelShardingStrategyMode.cs`：分表模式枚举（`Time` / `Volume` / `Hybrid`，含 `Description`）。
- `ParcelTimeShardingGranularity.cs`：时间分表粒度枚举（`PerMonth` / `PerDay`，含 `Description`）。
- `ParcelVolumeThresholdAction.cs`：容量阈值动作枚举（`AlertOnly` / `SwitchToPerDay`，含 `Description`）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/`：EF 设计时支持目录
- `MySqlContextFactory.cs`：MySQL 设计时 DbContext 工厂（实现 `IDesignTimeDbContextFactory<SortingHubDbContext>`），供 `dotnet ef migrations add/remove/list` 等 CLI 命令在无宿主进程时构建 DbContext；连接字符串从 `appsettings.json` 的 `ConnectionStrings:MySql` 读取，版本采用 AutoDetect → 兜底 8.0 两级策略。
- `SqlServerContextFactory.cs`：SQL Server 设计时 DbContext 构建器（由统一设计时工厂按 provider 分发调用），提供 SQL Server 连接字符串搜索与 `DbContextOptions` 组装能力。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/`：EF Core 迁移文件目录
- `20260316184030_InitialCreate.cs`：初始迁移，包含全部表（Parcels、Bags 及各值对象属性表）的 `Up`（建表）与 `Down`（回滚）逻辑。
- `20260316184030_InitialCreate.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260317024345_UseAttributeBasedIndexesAndPrecision.cs`：索引/精度特征标记对齐迁移（空 `Up/Down`，用于同步模型快照）。
- `20260317024345_UseAttributeBasedIndexesAndPrecision.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260317062930_SplitParcelStatusAndExceptionType.cs`：Parcel 状态三态收敛后新增 `ExceptionType` 可空字段迁移。
- `20260317062930_SplitParcelStatusAndExceptionType.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260318024421_OptimizeParcelAggregateQueryIndexes.cs`：Parcel 聚合高频查询索引优化迁移（离散条件 + 时间范围复合索引）。
- `20260318024421_OptimizeParcelAggregateQueryIndexes.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `SortingHubDbContextModelSnapshot.cs`：当前模型快照，EF Core 用于计算下次迁移的差量（自动生成，勿手动修改）。

#### `Zeye.Sorting.Hub.Infrastructure/Repositories/`：仓储基类与结果模型目录
- `MemoryCacheRepositoryBase.cs`：带内存缓存失效逻辑的仓储基类。
- `ParcelRepository.cs`：Parcel 仓储第一阶段实现（复用 `RepositoryBase` 与 `IDbContextFactory`，提供基础读写、分页查询、邻近查询与过期清理；写操作统一返回共享仓储结果模型）。
- `RepositoryBase.cs`：通用仓储基类（增删改查 + 自动持久化实现）。

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
- `AutoTuningProductionControlTests.cs`：覆盖 dry-run、危险动作隔离、告警防抖与恢复、普通/严重回归、unavailable 指标处理、执行计划探针 available/unavailable 双路径、闭环链路与分表覆盖守卫校验、迁移失败策略分环境解析、结构化扩容计划解析、Time/Volume/Hybrid 分表策略评估、PerDay 预建守卫（配置+物理探测）与分表观测口径/自动索引过滤规则回归。
- `ParcelRepositoryTests.cs`：Parcel 仓储第一阶段能力测试，覆盖分页过滤、详情与邻近查询、新增/更新/删除、过期清理与批量新增。

## 本次更新内容（新增 Parcel 属性操作指南文档）

1. **新增 `Parcel属性新增操作指南.md`**：详细说明当 Parcel 聚合需要新增属性时，需要修改哪些文件及如何修改。文档涵盖三种情形：①在聚合根主表新增标量属性（修改 `Parcel.cs` + `ParcelEntityTypeConfiguration.cs` + 执行迁移）；②在现有值对象中新增属性（修改对应值对象文件 + `ParcelEntityTypeConfiguration.cs` + 执行迁移）；③新增全新值对象（新建值对象文件 + 修改 `Parcel.cs` + 修改 `ParcelEntityTypeConfiguration.cs` + 更新 `README.md` + 执行迁移）。附完整检查清单与当前 Parcel 主表字段一览。

## 本次更新内容（分表治理与观测补强）

1. **分表自动创建默认关闭但制度化补强**：`Persistence:Sharding:CreateShardingTableOnStarting` 默认 `false`，并在启动期审计输出预建窗口（`Governance:PrebuildWindowHours`）与 Runbook（`Governance:Runbook`），降低“路由命中但物理表缺失”风险。
2. **哈希分片模数配置化并沉淀扩容计划**：`ParcelRelatedHashShardingMod` 从硬编码改为配置驱动（默认 16）；新增 `HashSharding:ExpansionTriggerRatio` 与 `HashSharding:ExpansionPlan`，显式记录 16→32 的触发阈值与切换步骤。
3. **分表观测指标显式沉淀**：`DatabaseAutoTuningHostedService` 新增三项指标输出：`autotuning.sharding.hit_rate`、`autotuning.sharding.cross_table_query_ratio`、`autotuning.sharding.hot_table_skew`，用于持续观察命中率、跨分表查询占比与热点倾斜。
4. **治理与容量预测绑定策略文档化**：通过 `appsettings.json` 的 Sharding/Governance/HashSharding 段将扩容触发阈值、预建窗口与 Runbook 配置显式化，便于纳入演练制度。
5. **新增自动审查守卫（EF + 分表联动）**：在现有 `HasPendingModelChanges()` 守卫基础上，新增 `AssertParcelAggregateShardingCoverage`（启动期 + 测试期双校验），当 ValueObjects 新增 `*Info` 类型但未配置分表规则时立即失败，阻断遗漏进入运行期。

## 后续可完善点（分表治理）

1. 在 CI 中增加“下周期分表预建校验”门禁（按 PrebuildWindowHours 自动检查）。
2. 为 `ExpansionPlan` 增加结构化字段（阶段、窗口、回滚脚本路径），替代纯文本描述。
3. 将三项分表指标接入 Prometheus/Grafana 告警面板，补齐阈值化运营闭环。

## 本次更新内容（分表双策略可配置治理）

1. **新增分表策略配置模型与决策骨架**：在 `Persistence:Sharding:Strategy` 下支持 `Mode(Time/Volume/Hybrid)`、`Time.Granularity(PerMonth/PerDay)`、`Volume.MaxRowsPerShard`、`Volume.HotThresholdRatio`、`Volume.ActionOnThreshold(AlertOnly/SwitchToPerDay)`，并提供观测值字段用于阈值决策。
2. **统一策略评估入口（不新增平行框架）**：新增 `ParcelShardingStrategyEvaluator`，统一输出结构化校验结果与决策快照；`PersistenceServiceCollectionExtensions.ConfigureParcelAggregateSharding` 与 `DatabaseInitializerHostedService` 均复用该入口，避免影分身逻辑。
3. **注册入口收敛到现有分表链路**：继续使用 `ConfigureParcelAggregateSharding + ParcelAggregateShardingRules` 注册，不复制新框架；在保持 Parcel 主表分片字段为 `CreatedTime` 的前提下，根据策略决策动态选择 `PerMonth/PerDay`，并同步驱动日期型值对象分表粒度。
4. **治理守卫与审计补强**：`DatabaseInitializerHostedService` 新增分表策略校验失败日志与启动期守卫阻断（结构化错误清单），并在治理基线日志输出当前策略模式、阈值动作、阈值命中状态与最终粒度决策。
5. **边界保持不变（显式声明）**：
   - Bootstrap SQL 仍保持“失败告警后继续（degraded）”语义，本次未改为 fail-fast。
   - Parcel 主表仍按 `CreatedTime` 作为主分表依据，本次未改为 `ScannedTime`。

## 后续可完善点（分表双策略）

1. 将 `CurrentEstimatedRowsPerShard` / `CurrentObservedHotRatio` 从手工配置升级为真实观测源（数据库统计表或可观测指标），减少人工维护成本。
2. 在隔离器框架下补充“阈值命中后的自动切换编排（开关 + dry-run + 审计 + 回滚脚本）”，逐步从决策骨架演进到安全可控的自动化治理。
3. 为 Time/Volume/Hybrid 策略增加分环境差异化模板（生产更保守、压测环境更激进）与 CI 配置校验门禁。

## 本次更新内容（剩余问题收口 PR）

1. **PerDay 守卫闭环**：在 `DatabaseInitializerHostedService` 既有治理守卫中补齐 “`CreateShardingTableOnStarting=false` + `EffectiveDateMode=PerDay`” 场景校验，要求预建窗口内目标日表日期清单（`Governance:PrebuiltPerDayDates`）完整，避免策略切换到按天后物理日表未预建。
2. **容量观测输入收敛**：`ParcelShardingStrategyEvaluator` 新增结构化 `Volume:Observation` 入口（`Source` / `EstimatedRowsPerShard` / `ObservedHotRatio`），并兼容 legacy 字段，实现“可配置输入 -> 统一观测对象 -> 阈值决策”的单入口收口，便于未来接入数据库统计/监控采集。
3. **Volume/Hybrid 语义澄清**：在策略决策日志、注释与配置说明中明确：`Volume/Hybrid` 是“容量阈值驱动的时间粒度治理”，当前**不是**独立的按数据量物理分表平台。
4. **扩展点预留**：策略决策中显式输出 `RequiresFinerGranularityExtension`，为“单天仍过大”后续扩展（如 `PerHour`/日内 bucket）保留入口；本次不引入新分表引擎。
5. **联动测试补强**：新增覆盖结构化 Observation 优先级、PerDay 预建日期格式校验、PerDay 策略在手工预建模式下的守卫阻断行为，确保治理边界可验证。
6. **有意保留项（本次明确不改）**：
   - Bootstrap SQL 仍保持“失败告警后继续”语义（不改为 fail-fast）。
   - Parcel 主表仍按 `CreatedTime` 分表（不改为 `ScannedTime`）。

## 后续可完善点（剩余问题收口 PR）

1. 将 `Governance:PrebuiltPerDayDates` 从静态清单升级为“数据库实际分表存在性检查 + 发布前自动门禁”，减少人工维护日期的负担。
2. 在不改变当前治理语义的前提下，评估引入 `PerHour` 或日内 hash bucket 作为 “PerDay 仍过大” 的下一层细粒度策略。
3. 将 `Volume:Observation:Source` 与可观测平台打通，沉淀来源标签与采样时间戳，进一步提高阈值决策可审计性。

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
5. `IParcelRepository`、`ParcelScannedEventArgs`、`ParcelChuteAssignedEventArgs` 等占位结构体后续应按业务事件扩充字段定义与触发链路。

## 本次更新内容（Copilot 限制规则全量整改）

1. **事件载荷规范化**：将 `DomainEvents/Parcels` 下事件参数迁移到 `Domain/Events/Parcels`，并统一改为 `readonly record struct`，满足“事件载荷必须定义在 `Events` 子目录且使用不可变值语义”的规则。
2. **枚举规范补齐**：补齐缺失的枚举类型注释；为 `AutoTuningUnavailableReason` 全部成员补齐 `Description` 与成员注释，避免语义漂移。
3. **异常日志补齐**：`RepositoryBase` 中所有 `OperationCanceledException` 分支补充 `LogWarning`，确保异常路径均可观测。
4. **注释覆盖补齐**：为生产代码中缺失的类字段与方法补齐 XML 注释，统一满足“字段必须有注释、方法必须有注释”的规则。

## 后续可完善点（规则治理）

1. 增加 CI 静态检查（如 Roslyn Analyzer 或自定义脚本）自动门禁“字段/方法注释、事件载荷目录与类型、枚举 Description 完整性”，避免回归。
2. 为事件载荷结构体补充业务字段与对应单元测试，替换当前最小占位定义。

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
3. **发布策略配置化**：新增 `Persistence:Migration:FailStartupOnError`，支持“迁移失败降级运行（false）/ 失败即阻断启动（true）”双模式；发布前 EF 验收未通过仍阻断发布。
4. **新增 CI 门禁工作流**：新增 `.github/workflows/ef-migration-validation.yml`，在 MySQL + SQL Server 容器上真实执行 `dotnet ef migrations list` / `dotnet ef database update` / `dotnet ef migrations script`。
5. **索引与精度特征标记收敛**：`Parcel`、`BagInfo` 索引改为类级别 `[Index]` 声明，`decimal(18,3)` 统一改为 `[Precision(18,3)]`；同步生成 `UseAttributeBasedIndexesAndPrecision` 迁移以消除 PendingModelChanges。

## 后续可完善点（迁移治理）

1. 若后续 SQL Server 与 MySQL 的迁移演进差异持续扩大，可升级为“独立迁移程序集”策略，进一步降低跨提供器误用风险。
2. 在流水线上增加迁移脚本归档（artifact）与 DBA 审批节点，形成可追溯发布审计链路。
3. 将“月度回滚演练 / 季度灾备升降级演练”接入流水线门禁，自动校验演练记录完备性。

## 本次更新内容（Parcel 状态拆分 + 异常类型细分）

1. **`ParcelStatus` 三态收敛**：`ParcelStatus` 仅保留 `Pending`（待操作）、`Completed`（已完成）、`SortingException`（分拣异常）三个状态，避免状态语义与异常原因混用。
2. **新增 `ParcelExceptionType` 枚举**：在 `Zeye.Sorting.Hub.Domain/Enums/ParcelExceptionType.cs` 增加 12 类分拣异常原因（接口响应异常、等待 DWS 数据超时、等待目标格口超时、无效目标格口、速度不匹配、锁格、叠包、灰度仪响应异常、位置检测异常、包裹丢失、机械故障、飘格），并补齐 `Description` 与注释。
3. **`Parcel` 聚合新增异常类型字段**：新增 `ExceptionType`（可空），仅在 `Status=SortingException` 时赋值；新增 `MarkSortingException` 领域方法统一维护状态与异常类型一致性。
4. **新增迁移与测试验证**：补充字段变更迁移，并在现有测试工程中增加状态枚举与异常状态流转的最小回归测试。

## 后续可完善点（Parcel 异常治理）

1. 在 Application/Contracts 层补充 `ParcelExceptionType` 的对外 DTO/查询筛选条件，减少字符串化状态判断。
2. 按异常类型建立告警分级策略（例如机械故障/包裹丢失优先级高于超时类），提升运维响应效率。

## 本次更新内容（数据库读写压力测试计划）

1. **新增 `数据库读写压力测试计划.md`**：针对本项目 MySQL + EFCore.Sharding 分表架构制定完整压测方案，包含：
   - **4 大测试场景**：纯写入（TPS 梯度加压）、纯读取（QPS 梯度加压）、混合读写（70% 写 + 30% 读）、30 分钟长时稳定性。
   - **与项目配置对齐的通过标准**：P99 ≤ 500 ms（对齐 `Persistence:AutoTuning:AlertP99Milliseconds = 500`）、错误率 < 1%（对齐 `Persistence:AutoTuning:AlertTimeoutRatePercent = 1`）、死锁 = 0。
   - **分表专项指标**：`autotuning.sharding.hit_rate ≥ 98%`、`hot_table_skew ≤ 2.0`（最热分片调用量 / 平均调用量，完全均匀时为 1.0）。
   - **完整执行步骤清单、监控 SQL、日志采集命令与结果记录模板**。
2. **更新 README 文件树章节**：在"各层级与各文件作用说明（逐项）"根目录节补充 `数据库读写压力测试计划.md` 与 `NewDatabaseProvider-Guide.md` 两个条目的职责说明（两文件均已存在，此前逐项说明缺失）。

## 后续可完善点（压测治理）

1. 在 CI 中增加"压测数据自动清理"步骤，防止种子数据影响正式环境。
2. 将 sysbench 压测脚本封装为可复用的 Shell/Makefile 目标，纳入仓库 `scripts/` 目录管理。
3. 接入 Prometheus + Grafana，将 AutoTuning 的分表观测指标（命中率、倾斜度、跨表查询占比）可视化，实现压测期间实时大盘监控。

## 本次更新内容（重复代码治理 PR：去重/收口/命名统一）

1. **DatabaseDialects 去重收口**：将 `MySqlDialect.cs` 与 `SqlServerDialect.cs` 重复的索引列归一化和索引名构造逻辑收敛到 `DatabaseProviderExceptionHelper.cs`，方言类仅保留 SQL 拼装差异。
2. **删除无意义转发器**：移除 `PersistenceServiceCollectionExtensions.cs` 中仅参数透传的 `GetPositiveIntOrDefault` 中间层，调用方直接复用 `AutoTuningConfigurationHelper`。
3. **统一配置键拼装入口**：在 `AutoTuningConfigurationHelper.cs` 新增 `BuildAutoTuningKey` 与 `BuildAutonomousKey`，并替换 `DatabaseAutoTuningHostedService.cs`、`SlowQueryAutoTuningPipeline.cs` 内重复本地方法。
4. **统一本地时间归一化入口**：在 `AutoTuningConfigurationHelper.cs` 新增 `NormalizeToLocalTime`，并替换 `SlowQuerySample.cs`、`PersistenceServiceCollectionExtensions.cs` 的重复实现，继续保持“仅本地时间语义”约束。
5. **最小回归测试补充**：在 `AutoTuningProductionControlTests.cs` 增加键拼装、本地时间语义归一化与跨方言索引名哈希一致性的测试，保障去重重构不改变外部行为。
6. **文件树与职责同步**：本次未新增/删除文件，已同步更新本 README 相关条目的职责描述，确保文档与当前实现一致。

## 后续可完善点（重复治理）

1. 可进一步抽取方言层“表名转义/限定名拼装”公共骨架，在不改变方言 SQL 细节的前提下继续降低重复率。
2. 可在 AutoTuning 相关测试中增加配置键拼装的参数化覆盖，减少未来配置项扩展时的回归风险。

## 本次更新内容（PR2：自动调谐与自动索引建议修复）

1. **分表命中率口径修正**：`autotuning.sharding.hit_rate` 改为“可识别主表且不含跨表语义”才计入命中，避免过去“只要有 FROM 就命中”的误判。
2. **跨表查询识别补强**：`autotuning.sharding.cross_table_query_ratio` 在 JOIN 之外，新增子查询多 `FROM`、集合运算（`UNION/INTERSECT/EXCEPT`）与逗号连接识别。
3. **热点与容量样本源修正**：`hot_table_skew` 与容量预测采样改为基于全量慢 SQL 指标聚合，不再只依赖 `TuningCandidates` 子集，降低统计偏差。
4. **自动索引建议前置过滤**：在 AutoTuning 主链路内新增统一过滤规则，先检查模型静态索引覆盖/语义重复，再过滤低价值索引建议，避免冗余建议进入执行与日报输出。
5. **公共逻辑复用与去重**：`SlowQueryAutoTuningPipeline` 新增主表提取公共方法，`DatabaseAutoTuningHostedService` 直接复用，减少重复语义实现。
6. **最小回归测试补充**：新增子查询跨表识别、全量指标热点倾斜口径、自动索引过滤三类测试，确保逻辑修复可验证。
7. **文件树与职责同步**：本次未新增/删除文件；已更新 `README` 中相关文件职责描述与本次更新说明。

## 后续可完善点（自动调谐治理）

1. 在不引入并行执行器的前提下，可继续引入“数据库真实索引元数据（运行时）+ 模型索引（静态）”双源比对，进一步降低跨环境误判率。
2. 对“低价值索引”规则引入按表历史基线学习（例如分位数阈值），减少统一阈值在不同业务负载下的偏差。

## 本次更新内容（PR3：自动迁移 + 分表治理完善）

1. **迁移失败策略明确化**：`DatabaseInitializerHostedService` 新增 `FailureStrategy` 解析逻辑，支持 `Production/NonProduction` 分环境策略；默认行为明确为“生产 FailFast、非生产 Degraded”，并兼容历史 `FailStartupOnError`。
2. **分表预建程序化守卫**：在既有 `DatabaseInitializerHostedService` 启动流程中新增治理守卫，不再仅靠日志提醒；当关闭启动自动建表且启用守卫时，强制校验 Runbook 配置，生产环境额外要求结构化扩容阶段配置。
3. **治理配置结构化收敛**：`appsettings.json` 将 `HashSharding.ExpansionPlan` 从单文本方案升级为结构化字段（`CurrentMod`、`TargetMod`、`Stages[]`），减少“文本计划式字段”。
4. **分表规则注册去膨胀**：`PersistenceServiceCollectionExtensions` 将 Parcel 关联值对象分表规则改为单一声明式清单统一注册，覆盖守卫复用同一清单，避免注册与守卫双列表长期膨胀。
5. **最小回归测试补充**：`AutoTuningProductionControlTests` 新增迁移策略分环境解析、结构化扩容阶段解析、扩容摘要优先级等测试，确保改动行为可验证。

## 后续可完善点（PR3 续项）

1. 当前“分表预建守卫”基于配置完整性与策略边界校验，尚未做到“直接探测未来周期物理分表是否已预建”；后续可结合数据库元数据查询增加实表存在性验证。
2. Parcel 关联值对象分表规则已收敛为单清单，但新增类型仍需补一条声明；后续可评估通过属性标记/约定推断进一步自动化，减少人工维护成本。

## 本次更新内容（PR3 审查问题修复）

1. **守卫异常日志补全**：`DatabaseInitializerHostedService` 将分表治理守卫纳入 `try/catch` 统一启动流程，并对守卫异常追加 `LogCritical` 后再抛出，保证阻断启动场景具备结构化日志。
2. **守卫触发边界修正**：`TargetMod > CurrentMod` 校验改为仅在“手工预建 + 守卫开启”分支执行，避免自动建表场景被误阻断。
3. **枚举归档治理**：`MigrationFailureMode` 从 HostedService 内嵌枚举提取至 `Zeye.Sorting.Hub.Host/Enums/MigrationFailureMode.cs`，并补齐 `Description` 与注释，符合枚举目录化规范。
4. **测试同步修复**：`AutoTuningProductionControlTests` 改为引用独立枚举类型，保持原有策略解析断言不变。

## 后续可完善点（PR3 审查修复）

1. 目前分表治理守卫已使用专用异常类型并完成启动期日志阻断；后续可继续扩展“未来周期物理分表存在性探测”，将治理从配置完整性校验升级为实际建表状态校验。

## 本次更新内容（单天仍超大下一层粒度扩展设计）

1. **布尔扩展信号升级为结构化规划模型**：`ParcelShardingStrategyEvaluator` 将 `RequiresFinerGranularityExtension` 升级为 `ParcelFinerGranularityExtensionPlan`，可表达是否继续细分、推荐模式（`PerHour`/`BucketedPerDay`/`None`）、推荐原因、是否要求预建守卫、生命周期（`PlanOnly`/`AlertOnly`/`FutureExecutable`）。
2. **配置模型收口并支持未来细粒度策略**：在 `Persistence:Sharding:Strategy:Volume:FinerGranularity` 下新增结构化配置（`ModeWhenPerDayStillHot`、`Lifecycle`、`RequirePrebuildGuard`、`Bucket:BucketCount`），避免继续平铺字段导致 evaluator 膨胀。
3. **治理守卫改为消费统一规划结果**：`DatabaseInitializerHostedService` 新增 `ShouldEnforcePerDayPrebuildGuard`，守卫判断从“只认 PerDay 的硬编码分支”收口为“统一决策对象 + 扩展规划结果”驱动，便于后续扩展到更多粒度策略时复用。
4. **收口重复语义实现（去影分身）**：`ParcelShardingStrategyEvaluator` 引入通用枚举解析路径（拒绝数字枚举、统一默认值与错误输出），消除 Mode/Granularity/Action 三处重复解析实现，减少后续扩展重复代码。
5. **边界保持不变（明确声明）**：
   - Bootstrap SQL 仍保持“失败告警后继续（degraded）”语义，本次未改。
   - Parcel 主表仍按 `CreatedTime` 分表，本次未改为 `ScannedTime`。
   - Volume/Hybrid 仍是“容量驱动的时间粒度治理”；finer-granularity 当前属于“设计与治理层能力”，不是完整在线自动执行平台。

## 后续可完善点（单天仍超大扩展）

1. 在隔离器（开关 + dry-run + 审计 + 回滚）边界内，将 `FutureExecutable` 生命周期逐步接入可控执行编排。
2. 为 `BucketedPerDay` 增加更细的策略参数（例如路由字段组合、桶热点重平衡提示），并补充运维演练 Runbook 模板。
3. 在启动守卫中引入“规划目标的物理分表存在性探测”能力，减少纯配置清单方式的人工维护成本。
4. finer-granularity 未来物理执行仍需坚持隔离器（开关 + dry-run + 审计 + 回滚边界），避免直接放开自动执行。
5. 未来可增加真实物理分表存在性探测能力，而不只是配置清单校验。

## 本次更新内容（finer-granularity 配置与 README 一致性修复）

1. **修复 BucketCount 校验误判**：`ParcelShardingStrategyEvaluator` 调整为仅在 `ModeWhenPerDayStillHot=BucketedPerDay` 时强制要求 `BucketCount`，`PerHour/None + 无 BucketCount` 视为合法，同时保留“非 Bucketed 配置 BucketCount 报当前不会生效”的既有校验语义。
2. **修复默认 appsettings 非法示例组合**：`Zeye.Sorting.Hub.Host/appsettings.json` 保留 `ModeWhenPerDayStillHot=PerHour`，移除 `Bucket:BucketCount` 示例，避免启动期分表策略守卫误阻断。
3. **同步修正文档文件树**：补齐 README 顶部“仓库文件结构（当前）”中遗漏的 `数据库读写压力测试计划.md`、`Zeye.Sorting.Hub.Host/Enums/` 与 `MigrationFailureMode.cs`。

## 本次更新内容（自动迁移 / 自动分表 / 自动调谐阶段收尾）

1. **自动迁移（当前阶段已完成）**：
   - 启动自动迁移链路保留（`DatabaseInitializerHostedService` 启动期迁移 + 一致性校验）。
   - 迁移一致性守卫与 `HasPendingModelChanges` 守卫已纳入启动流程。
   - 本次明确边界：当前阶段完成的是“自动迁移与一致性阻断”，**不包含**手工 DDL 自愈执行。
2. **自动分表（当前阶段已完成）**：
   - 主表与值对象分表规则继续通过声明式规则清单统一注册（`ParcelAggregateShardingRuleKind` 明确 `Date` / `Hash` 规则类别语义）。
   - Time / Volume / Hybrid 策略评估与治理审计保持闭环。
   - PerDay 手工预建窗口守卫、PerDay 物理分表存在性探测已完成。
   - 本次收尾修复两项关键问题：  
     a) 治理守卫从统一重试链路中拆出，配置/守卫/物理表明确缺失等确定性错误立即失败，不再被重试放大；  
     b) 批量物理探测接口补齐 `schemaName` 语义，HostedService 显式传参，SQL Server 不再写死 `dbo`。
   - 本次明确边界：当前阶段完成的是“策略决策 + 规则注册 + 治理守卫 + 物理存在性探测”，**不是**完整在线自动扩缩容平台。
3. **自动调谐（当前阶段已完成）**：
   - 慢 SQL 采集、闭环自治（监控/诊断/执行/验证/回滚）链路已具备。
   - 白名单、风险评分、高峰时段限制、危险动作隔离器已具备并默认生效。
   - 自动验证 / 自动回滚链路与审计语义已具备。
   - provider-aware probe 扩展点已具备，默认 probe 仍为 logging-only。
4. **README 同步收尾**：
   - 已补齐 `ParcelAggregateShardingRuleKind.cs` 在“仓库文件结构（当前）”与“各层级与各文件作用说明（逐项）”中的条目与职责说明。

## 后续可继续完善项

1. finer-granularity 真实自动执行仍需严格在隔离器边界内演进（开关 + dry-run + 审计 + 回滚边界全部就绪后再放开）。
2. 真实 `EXPLAIN` / `SHOWPLAN` 计划探针可在现有 provider-aware probe 扩展点上继续接入，默认 logging-only 语义保持不变。
3. 哈希扩容/重分片自动执行仍属于下一阶段能力，不在本轮 PR 内继续膨胀。

## 本次更新内容（IParcelRepository 第一阶段可落地仓储实现）

1. **仓储契约落地**：将 `IParcelRepository` 从注释占位改为可执行契约，仅覆盖第一阶段能力（基础读写、分页列表、按 Bag/工作台/状态/格口查询、邻近查询、过期清理与批量新增），不扩散到统计/报表能力。
2. **新增最小查询模型**：在 `Zeye.Sorting.Hub.Domain/Repositories/Models/` 新增 `ParcelQueryFilter`、`PageRequest`、`PageResult`、`ParcelSummaryReadModel`，实现“列表返回摘要读模型、详情返回聚合根”的职责分离。
3. **新增基础设施实现**：在 `Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelRepository.cs` 新增第一阶段仓储实现，复用 `RepositoryBase` / `IDbContextFactory` 风格，提供列表查询、详情查询、邻近查询与基础写操作。
4. **DI 注册补齐且不破坏现有治理结构**：`PersistenceServiceCollectionExtensions` 新增 `IParcelRepository -> ParcelRepository` 注册，同时补充 `AddPooledDbContextFactory<SortingHubDbContext>` 并与既有 `AddDbContextPool` 复用同一配置方法，保持自动迁移/自动分表/自动调谐主结构不变。
5. **测试补齐**：新增 `ParcelRepositoryTests.cs`，覆盖第一阶段核心路径（分页过滤、详情与邻近查询、写操作与过期清理）。

## 本次更新内容（IParcelRepository 二次收敛）

1. **分页模型复用化**：将 `ParcelPageRequest/ParcelPageResult` 收敛为通用 `PageRequest/PageResult<T>`，以便其他查询场景复用，移除重复分页模型代码。
2. **读模型字段补齐**：`ParcelSummaryReadModel` 扩展为包含 `Parcel` 全部扁平化字段（含审计字段与业务标量字段），避免列表读模型信息缺失。
3. **去除重复查询契约**：删除 `GetDetailByIdAsync`，保留 `GetByIdAsync` 作为“按主键获取完整聚合详情”的唯一入口，避免接口语义重叠。
4. **强制时间段查询参数**：`GetByBagCode/GetByWorkstationName/GetByStatus/GetByChute` 增加必填 `scannedTimeStart/scannedTimeEnd` 参数，确保查询边界明确。
5. **时间跨度治理**：新增 `MaxTimeRangeAttribute` 并应用于 `ParcelQueryFilter`，默认限制扫码时间跨度不超过 3 个月（同时校验结束时间不早于开始时间）。
6. **Models 目录细分**：将 `Models` 细分为 `Filters/Paging/ReadModels/Validation` 子目录，职责清晰，便于后续扩展与复用。
7. **无用代码清理**：删除不再使用的 `ParcelPageRequest.cs` 与 `ParcelPageResult.cs`，同步更新引用与测试，避免冗余实现残留。

## 后续可完善点（IParcelRepository 二次收敛）

1. 若后续仓储增多，可将 `MaxTimeRangeAttribute` 提升为更通用的查询验证组件，并在应用层统一入参校验链路中复用。
2. 对于包含全部扁平字段的 `ParcelSummaryReadModel`，后续可按“接口场景”拆分成轻量/完整两个读模型版本，以平衡带宽与可用性。

## 后续可完善点（Parcel 仓储阶段化演进）

1. 在不破坏当前契约的前提下，为分页列表逐步补充更细粒度排序/筛选选项（保持 summary 模型边界不变）。
2. 当 Application/Contracts 层正式接入查询用例后，再按阶段引入 DTO 映射与用例级验证，避免在仓储阶段过度扩展。
3. 第二阶段再评估统计/报表类能力收敛位置（建议独立读模型查询服务），避免在聚合仓储中混入报表职责。

## 本次更新内容（仓储 Result 类型统一收敛）

1. **统一仓储结果类型**：将 `RepositoryResult/RepositoryResult<T>` 迁移到 `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryResult.cs`，作为 Domain 与 Infrastructure 共用结果模型。
2. **移除重复结果模型**：删除 `Zeye.Sorting.Hub.Infrastructure/Repositories/RepositoryResult.cs`，仓储体系不再保留并行 Result 抽象。
3. **统一写操作契约**：`IParcelRepository` 的 `AddAsync/UpdateAsync/RemoveAsync/AddRangeAsync` 统一返回 `Task<RepositoryResult>`，`RemoveExpiredAsync` 统一返回 `Task<RepositoryResult<int>>`。
4. **去除无意义桥接与重复日志**：`ParcelRepository` 移除显式接口桥接抛异常逻辑，直接复用 `RepositoryBase` 返回语义；`RemoveExpiredAsync` 改为同一 Result 风格并保留批次删除 + 上限告警保护。
5. **测试与 DI 回归同步**：`ParcelRepositoryTests` 新增 `IParcelRepository` DI 解析用例，并将写操作断言统一到共享 `RepositoryResult` 契约。

## 可继续完善的内容（仓储 Result 收敛后）

1. 若后续出现更多聚合仓储，可逐步统一读路径是否也采用 `RepositoryResult<T>` 语义，以便跨仓储错误处理策略一致。
2. 可按业务错误类型对 `RepositoryResult.ErrorMessage` 做结构化编码，减少上层字符串分支判断。

## 本次更新内容（枚举注释与本地时间语义测试修复）

1. **枚举注释与 Description 补齐**：`Zeye.Sorting.Hub.Domain/Enums/ActionIsolationDecision.cs` 已为枚举类型与每个成员补齐中文 XML 注释，并为每个成员补充 `Description` 特性，保持原有成员语义与目录位置不变。
2. **UTC 直连语义移除（测试侧）**：`Zeye.Sorting.Hub.Host.Tests/AutoTuningProductionControlTests.cs` 将原先直接使用 `DateTimeKind.Utc` 的断言改为“非本地时间 Kind 输入触发异常”的等价治理断言，避免源码直接出现被禁 UTC 相关 API，同时保留“拒绝非本地时间语义输入”的测试目标。

## 后续可完善点（枚举与时间语义治理）

1. 可在测试层补充统一的“本地时间语义输入构造约束”测试工具或约定，进一步降低后续引入 UTC 相关 API 的回归风险。
