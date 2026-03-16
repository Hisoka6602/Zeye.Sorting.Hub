# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .github（Copilot 仓库级指令目录）
│   └── copilot-instructions.md（Copilot 自定义指令：禁止 UTC、统一本地时间）
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
│   │   │   └── MySqlContextFactory.cs（设计时 DbContext 工厂）
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
- `项目完成度与推进计划.md`：项目阶段评估与路线图文档。

### `.github/`：Copilot 仓库级指令目录
- `copilot-instructions.md`：Copilot 自定义指令，硬性要求禁止 UTC 时间 API，统一使用本地时间语义。

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
- `Program.cs`：应用入口与 Host 构建流程。
- `Worker.cs`：后台轮询任务示例服务。
- `Zeye.Sorting.Hub.Host.csproj`：Host 项目定义。
- `appsettings.json`：默认运行配置。
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
- `AutoTuningConfigurationHelper.cs`：配置读取公共辅助类，集中提供 `GetPositiveIntOrDefault`、`GetNonNegativeDecimalOrDefault`、`GetTimeOfDayOrDefault` 等七个方法，消除三处影分身副本。
- `MySqlSessionBootstrapConnectionInterceptor.cs`：MySQL 连接会话初始化拦截器。
- `SlowQueryAutoTuningPipeline.cs`：慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道。
- `SlowQueryCommandInterceptor.cs`：EF Core 慢查询采集拦截器。
- `SlowQuerySample.cs`：慢查询采样记录模型。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/`：EF 设计时支持目录
- `MySqlContextFactory.cs`：设计时 DbContext 工厂（迁移工具使用）。

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
5. `IParcelRepository`、`ParcelScannedEventArgs`、`ParcelChuteAssignedEventArgs`、`MySqlContextFactory` 等占位类后续应补充完整实现。
