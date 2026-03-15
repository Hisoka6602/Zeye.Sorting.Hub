# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .gitattributes
├── .gitignore
├── README.md
├── Zeye.Sorting.Hub.Analytics
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Analytics.csproj
├── Zeye.Sorting.Hub.Application
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Application.csproj
├── Zeye.Sorting.Hub.Contracts
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Contracts.csproj
├── Zeye.Sorting.Hub.Domain
│   ├── Abstractions
│   │   └── IEntity.cs
│   ├── Aggregates
│   │   └── Parcels
│   │       ├── Parcel.cs
│   │       └── ValueObjects
│   │           ├── ApiRequestInfo.cs
│   │           ├── BagInfo.cs
│   │           ├── BarCodeInfo.cs
│   │           ├── ChuteInfo.cs
│   │           ├── CommandInfo.cs
│   │           ├── GrayDetectorInfo.cs
│   │           ├── ImageInfo.cs
│   │           ├── ParcelDeviceInfo.cs
│   │           ├── ParcelPositionInfo.cs
│   │           ├── SorterCarrierInfo.cs
│   │           ├── StickingParcelInfo.cs
│   │           ├── VideoInfo.cs
│   │           ├── VolumeInfo.cs
│   │           └── WeightInfo.cs
│   ├── DomainEvents
│   │   └── Parcels
│   │       ├── ParcelChuteAssignedEventArgs.cs
│   │       └── ParcelScannedEventArgs.cs
│   ├── Enums
│   │   ├── ActionType.cs
│   │   ├── ApiRequestStatus.cs
│   │   ├── ApiRequestType.cs
│   │   ├── BarCodeType.cs
│   │   ├── CommandDirection.cs
│   │   ├── ImageCaptureType.cs
│   │   ├── ImageType.cs
│   │   ├── NoReadType.cs
│   │   ├── ParcelStatus.cs
│   │   ├── ParcelType.cs
│   │   ├── VideoNodeType.cs
│   │   └── VolumeSourceType.cs
│   ├── Primitives
│   │   └── AuditableEntity.cs
│   ├── Repositories
│   │   └── IParcelRepository.cs
│   └── Zeye.Sorting.Hub.Domain.csproj
├── Zeye.Sorting.Hub.Host
│   ├── HostedServices
│   │   └── DatabaseInitializerHostedService.cs
│   ├── Program.cs
│   ├── Properties
│   │   └── launchSettings.json
│   ├── Worker.cs
│   ├── Zeye.Sorting.Hub.Host.csproj
│   ├── appsettings.Development.json
│   └── appsettings.json
├── Zeye.Sorting.Hub.Infrastructure
│   ├── DependencyInjection
│   │   └── PersistenceServiceCollectionExtensions.cs
│   ├── EntityConfigurations
│   │   ├── BagInfoEntityTypeConfiguration.cs
│   │   └── ParcelEntityTypeConfiguration.cs
│   ├── Persistence
│   │   ├── DatabaseDialects
│   │   │   ├── IDatabaseDialect.cs
│   │   │   ├── MySqlDialect.cs
│   │   │   └── SqlServerDialect.cs
│   │   ├── DesignTime
│   │   │   └── MySqlContextFactory.cs
│   │   └── SortingHubDbContext.cs
│   ├── Repositories
│   │   ├── MemoryCacheRepositoryBase.cs
│   │   ├── RepositoryBase.cs
│   │   └── RepositoryResult.cs
│   └── Zeye.Sorting.Hub.Infrastructure.csproj
├── Zeye.Sorting.Hub.Realtime
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Realtime.csproj
├── Zeye.Sorting.Hub.RuleEngine
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.RuleEngine.csproj
├── Zeye.Sorting.Hub.SharedKernel
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.SharedKernel.csproj
├── Zeye.Sorting.Hub.sln
└── 项目完成度与推进计划.md
```

## Copilot 维护规定

- 每次新增文件或删除文件后，必须同步更新本 README 的“仓库文件结构（当前）”章节，保证结构清单与仓库实际内容一致。
- 每次新增文件或删除文件后，必须同步更新本 README 的“各层级与各文件作用说明（逐项）”章节，保证职责说明与仓库实际内容一致。

## 各层级与各文件作用说明（逐项）

### 根目录（`.`）

- 作用：解决方案根目录，承载多项目分层结构（Host、Domain、Infrastructure、Application、Contracts 等）。
- `.gitattributes`：Git 属性配置（如行尾规范）。
- `.gitignore`：Git 忽略规则（如 `bin/`、`obj/`、IDE 临时文件）。
- `README.md`：仓库总览、结构清单与维护规范文档。
- `Zeye.Sorting.Hub.sln`：.NET 解决方案入口，聚合全部项目。
- `项目完成度与推进计划.md`：项目阶段评估与路线图文档。

### `Zeye.Sorting.Hub.Analytics/`

- 作用：分析与报表子域（当前为占位工程）。
- `Zeye.Sorting.Hub.Analytics.csproj`：Analytics 项目定义。
- `Class1.cs`：占位类，预留统计指标/报表能力实现位置。

### `Zeye.Sorting.Hub.Application/`

- 作用：应用层（Use Case 编排层，当前为占位工程）。
- `Zeye.Sorting.Hub.Application.csproj`：Application 项目定义。
- `Class1.cs`：占位类，预留命令/查询/应用服务实现位置。

### `Zeye.Sorting.Hub.Contracts/`

- 作用：契约层（对外 DTO / 接口模型，当前为占位工程）。
- `Zeye.Sorting.Hub.Contracts.csproj`：Contracts 项目定义。
- `Class1.cs`：占位类，预留请求/响应契约定义位置。

### `Zeye.Sorting.Hub.Domain/`

- 作用：核心领域层，存放聚合根、值对象、领域事件、枚举与仓储接口。
- `Zeye.Sorting.Hub.Domain.csproj`：Domain 项目定义。

#### `Zeye.Sorting.Hub.Domain/Abstractions/`

- 作用：领域抽象接口层。
- `IEntity.cs`：实体通用接口（定义主键契约）。

#### `Zeye.Sorting.Hub.Domain/Aggregates/`

- 作用：领域聚合目录。

##### `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/`

- 作用：包裹聚合目录。
- `Parcel.cs`：包裹聚合根，承载包裹生命周期状态与行为。

###### `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/`

- 作用：包裹聚合值对象目录。
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

#### `Zeye.Sorting.Hub.Domain/DomainEvents/`

- 作用：领域事件目录。

##### `Zeye.Sorting.Hub.Domain/DomainEvents/Parcels/`

- 作用：包裹相关领域事件目录。
- `ParcelChuteAssignedEventArgs.cs`：包裹分配格口事件参数（当前占位定义）。
- `ParcelScannedEventArgs.cs`：包裹扫描事件参数（当前占位定义）。

#### `Zeye.Sorting.Hub.Domain/Enums/`

- 作用：领域枚举与业务语义常量目录。
- `ActionType.cs`：动作类型枚举定义。
- `ApiRequestStatus.cs`：接口请求状态枚举定义。
- `ApiRequestType.cs`：接口请求类型枚举定义。
- `BarCodeType.cs`：条码类型枚举定义。
- `CommandDirection.cs`：命令方向枚举定义。
- `ImageCaptureType.cs`：图像采集方式枚举定义。
- `ImageType.cs`：图像类型枚举定义。
- `NoReadType.cs`：无码/难码类型枚举定义。
- `ParcelStatus.cs`：包裹状态枚举定义。
- `ParcelType.cs`：包裹类别枚举定义。
- `VideoNodeType.cs`：视频节点类型枚举定义。
- `VolumeSourceType.cs`：体积来源类型枚举定义。

#### `Zeye.Sorting.Hub.Domain/Primitives/`

- 作用：领域基础类型目录。
- `AuditableEntity.cs`：可审计实体基类（创建/修改信息等）。

#### `Zeye.Sorting.Hub.Domain/Repositories/`

- 作用：领域仓储契约目录。
- `IParcelRepository.cs`：包裹仓储接口（当前为占位接口定义）。

### `Zeye.Sorting.Hub.Host/`

- 作用：宿主层（程序入口、后台服务、启动配置）。
- `Program.cs`：应用入口与 Host 构建流程。
- `Worker.cs`：后台轮询任务示例服务。
- `Zeye.Sorting.Hub.Host.csproj`：Host 项目定义。
- `appsettings.json`：默认运行配置。
- `appsettings.Development.json`：开发环境配置覆盖文件。

#### `Zeye.Sorting.Hub.Host/HostedServices/`

- 作用：启动/常驻托管服务目录。
- `DatabaseInitializerHostedService.cs`：数据库初始化与迁移托管服务。

#### `Zeye.Sorting.Hub.Host/Properties/`

- 作用：项目运行调试属性目录。
- `launchSettings.json`：本地启动配置（Profile、环境变量等）。

### `Zeye.Sorting.Hub.Infrastructure/`

- 作用：基础设施层（EF Core 持久化、仓储实现、DI 注册、数据库方言）。
- `Zeye.Sorting.Hub.Infrastructure.csproj`：Infrastructure 项目定义。

#### `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/`

- 作用：依赖注入扩展目录。
- `PersistenceServiceCollectionExtensions.cs`：持久化服务注册扩展（数据库提供器选择、DbContext 注册等）。

#### `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/`

- 作用：EF Core 实体映射配置目录。
- `BagInfoEntityTypeConfiguration.cs`：BagInfo 映射配置。
- `ParcelEntityTypeConfiguration.cs`：Parcel 聚合映射配置。

#### `Zeye.Sorting.Hub.Infrastructure/Persistence/`

- 作用：持久化核心目录（DbContext、方言、设计时工厂）。
- `SortingHubDbContext.cs`：EF Core DbContext（实体集与模型构建入口）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/`

- 作用：数据库方言抽象与实现目录。
- `IDatabaseDialect.cs`：数据库方言抽象接口。
- `MySqlDialect.cs`：MySQL 方言实现。
- `SqlServerDialect.cs`：SQL Server 方言实现。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/`

- 作用：EF 设计时支持目录。
- `MySqlContextFactory.cs`：设计时 DbContext 工厂（迁移工具使用）。

#### `Zeye.Sorting.Hub.Infrastructure/Repositories/`

- 作用：仓储基类与结果模型目录。
- `MemoryCacheRepositoryBase.cs`：带内存缓存失效逻辑的仓储基类。
- `RepositoryBase.cs`：通用仓储基类（增删改查基础实现）。
- `RepositoryResult.cs`：仓储调用结果封装模型。

### `Zeye.Sorting.Hub.Realtime/`

- 作用：实时通信子域（当前为占位工程）。
- `Zeye.Sorting.Hub.Realtime.csproj`：Realtime 项目定义。
- `Class1.cs`：占位类，预留实时推送/订阅能力实现位置。

### `Zeye.Sorting.Hub.RuleEngine/`

- 作用：规则引擎子域（当前为占位工程）。
- `Zeye.Sorting.Hub.RuleEngine.csproj`：RuleEngine 项目定义。
- `Class1.cs`：占位类，预留规则执行引擎实现位置。

### `Zeye.Sorting.Hub.SharedKernel/`

- 作用：跨模块共享内核（当前为占位工程）。
- `Zeye.Sorting.Hub.SharedKernel.csproj`：SharedKernel 项目定义。
- `Class1.cs`：占位类，预留通用基础能力实现位置。
