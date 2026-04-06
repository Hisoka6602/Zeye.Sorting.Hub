# DDD 分层接口与实现放置规范

## 1. 目标

本规范用于统一 DDD 项目中接口定义位置、实现类放置位置、依赖方向与目录结构，确保以下目标成立：

- 分层边界清晰
- 依赖方向稳定
- 抽象归属明确
- 避免接口乱放、重复定义、职责重叠
- 便于 Copilot、开发人员、审查人员统一执行

---

## 2. 总体依赖方向

项目必须严格遵守以下依赖方向：

```text
Host -> Infrastructure -> Application -> Domain
```

### 2.1 依赖约束

- `Domain` 不允许依赖 `Application`、`Infrastructure`、`Host`
- `Application` 不允许依赖 `Infrastructure`、`Host`
- `Infrastructure` 可以依赖 `Domain` 和 `Application`
- `Host` 作为组合根，负责组装所有模块

---

## 3. 接口定义归属总原则

接口定义放在哪一层，不取决于“哪层实现方便”，而取决于“这个抽象本质上属于哪一层”。

### 3.1 归属判断规则

#### 规则 A：表达领域能力边界的接口
放在 `Domain` 层。

#### 规则 B：表达应用编排、外部协作能力的接口
放在 `Application` 层。

#### 规则 C：表达基础设施内部技术细节的接口
放在 `Infrastructure` 层，仅供基础设施内部使用，不允许上浮成业务契约。

---

## 4. 实现归属总原则

### 4.1 Domain 层接口的实现

- 纯业务规则实现：放在 `Domain`
- 依赖数据库、缓存、文件、网络、设备等外部资源的实现：放在 `Infrastructure`

### 4.2 Application 层接口的实现

- 纯应用层编排逻辑：放在 `Application`
- 依赖外部资源的实现：放在 `Infrastructure`

### 4.3 Infrastructure 层接口的实现

- 只能放在 `Infrastructure`

---

## 5. 严禁事项

### 5.1 禁止把所有接口统一定义到 Application 层
接口必须按语义归属到对应分层，不能为了省事全部放到 `Application`。

### 5.2 禁止 Domain 依赖 Application
`Domain` 不允许引用 `Application` 中的抽象或 DTO。

### 5.3 禁止重复定义同一职责接口
如果已经存在语义一致的接口，不允许再定义一套职责相同、命名不同的重复接口。

### 5.4 禁止基础设施实现细节泄漏到上层
EF、Redis、HTTP、MQ、文件系统、串口、TCP、报文格式等细节不允许进入 `Domain`。

### 5.5 禁止 Host 承载核心业务实现
`Host` 只负责启动、适配、注册和接入层逻辑，不承载核心业务规则和基础设施实现。

---

## 6. 各层职责边界

## 6.1 Domain 层职责

`Domain` 只承载核心业务模型与业务规则。

### 允许包含

- 聚合根
- 实体
- 值对象
- 领域事件
- 领域服务
- 领域规则
- 仓储接口
- 领域策略接口
- 领域规格接口
- 领域工厂接口
- 共享领域抽象

### 禁止包含

- `DbContext`
- EF Core 映射
- SQL
- Redis
- `HttpClient`
- 文件系统实现
- MQ 实现
- 第三方 SDK 调用
- 当前登录用户实现
- 配置读取实现
- 驱动器通信实现

---

## 6.2 Application 层职责

`Application` 负责用例编排与系统协作抽象。

### 允许包含

- Command / Query / Handler
- DTO / Request / Response
- 应用服务
- 用例编排
- 查询接口
- 当前用户上下文接口
- 权限检查接口
- 本地化接口
- 文件存储接口
- 导入导出接口
- 消息发布接口
- 第三方系统网关接口
- 业务侧设备能力抽象

### 禁止包含

- Repository 具体实现
- `DbContext`
- SQL 实现
- `HttpClient` 具体实现
- Redis 具体实现
- 文件系统具体实现
- 驱动通信具体实现
- 报文编解码具体实现

---

## 6.3 Infrastructure 层职责

`Infrastructure` 承载技术实现，负责实现 `Domain` 与 `Application` 定义的抽象。

### 允许包含

- Repository 实现
- UnitOfWork 实现
- `DbContext`
- EF Core 配置
- 外部 HTTP 网关实现
- Redis 实现
- 文件存储实现
- MQ 实现
- 本地化实现
- 当前用户上下文实现
- 第三方系统集成实现
- 设备驱动实现
- 协议编解码器
- 串口/TCP 通信实现
- CRC 计算器
- 协议帧解析器

---

## 6.4 Host 层职责

`Host` 是项目组合根与应用入口。

### 允许包含

- `Program.cs`
- 控制器
- SignalR Hub
- 中间件
- DI 注册
- 配置绑定
- 后台服务启动入口
- Swagger 配置

### 禁止包含

- 领域规则实现
- 仓储实现
- 外部网关实现
- 驱动实现
- 协议编解码实现
- 大量业务编排逻辑

---

## 7. 接口分类与放置规范

## 7.1 仓储接口（Repository）

### 定义层
`Domain`

### 实现层
`Infrastructure`

### 适用范围
围绕聚合根、实体、领域对象持久化边界的抽象。

### 命名规范

```text
I{AggregateName}Repository
```

### 示例

```csharp
/// <summary>
/// 订单仓储
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(OrderNumber orderNumber, CancellationToken cancellationToken);
}
```

### 目录建议

定义位置：

```text
YourProject.Domain/Repositories/IOrderRepository.cs
```

实现位置：

```text
YourProject.Infrastructure/Persistence/Repositories/OrderRepository.cs
```

### 约束

- 仓储接口必须面向领域对象，而不是面向数据库表
- 不允许在仓储接口中暴露 `IQueryable`
- 查询逻辑若明显属于读模型，应转入 Application 查询接口

---

## 7.2 领域服务接口（Domain Service）

### 定义层
通常为 `Domain`

### 实现层

- 纯业务规则实现：`Domain`
- 依赖外部资源时：接口在 `Domain`，外部资源由 `Application` 编排或 `Infrastructure` 支撑

### 适用范围
跨实体、值对象、聚合的核心业务规则。

### 命名规范

```text
I{BusinessConcept}DomainService
```

### 示例

```csharp
/// <summary>
/// 包裹路由领域服务
/// </summary>
public interface IParcelRoutingDomainService
{
    ChuteId ResolveTargetChute(Parcel parcel, SortingPlan plan);
}
```

### 目录建议

定义位置：

```text
YourProject.Domain/Services/IParcelRoutingDomainService.cs
```

实现位置：

```text
YourProject.Domain/Services/ParcelRoutingDomainService.cs
```

### 约束

- 领域服务是业务规则，不是 CRUD 包装器
- 不允许出现 EF、HTTP、Redis、文件系统等技术细节

---

## 7.3 领域策略 / 规格 / 规则接口

### 定义层
`Domain`

### 实现层

- 纯规则实现：`Domain`
- 依赖外部数据时：由 `Application` 先准备数据，或由 `Infrastructure` 提供支撑

### 适用范围
可替换的领域规则、策略、规格判定。

### 命名规范

```text
I{BusinessConcept}Policy
I{BusinessConcept}Specification
I{BusinessConcept}Strategy
```

### 示例

```csharp
/// <summary>
/// 格口分配策略
/// </summary>
public interface IChuteAssignmentPolicy
{
    bool CanAssign(Parcel parcel, Chute chute);
}
```

### 目录建议

```text
YourProject.Domain/Policies/IChuteAssignmentPolicy.cs
YourProject.Domain/Specifications/IParcelMatchSpecification.cs
```

---

## 7.4 领域工厂接口（Factory）

### 定义层
`Domain`

### 实现层

- 简单工厂：`Domain`
- 创建过程涉及复杂外部依赖时：由 `Application` 编排后调用

### 适用范围
聚合创建逻辑复杂且需要保证领域不变式时。

### 命名规范

```text
I{AggregateName}Factory
```

### 示例

```csharp
/// <summary>
/// 发运单工厂
/// </summary>
public interface IShipmentFactory
{
    Shipment Create(CreateShipmentContext context);
}
```

### 目录建议

```text
YourProject.Domain/Factories/IShipmentFactory.cs
```

---

## 7.5 UnitOfWork 接口

### 定义层
推荐放在 `Application/Abstractions/Persistence`

### 实现层
`Infrastructure`

### 说明
事务边界通常由用例驱动，因此推荐放在 `Application`。

### 命名规范

```text
IUnitOfWork
```

### 示例

```csharp
/// <summary>
/// 工作单元
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

### 目录建议

定义位置：

```text
YourProject.Application/Abstractions/Persistence/IUnitOfWork.cs
```

实现位置：

```text
YourProject.Infrastructure/Persistence/UnitOfWork.cs
```

### 约束

- 项目必须统一，不允许一部分放 `Domain`，另一部分放 `Application`
- 若已有旧定义被新规范覆盖，必须删除旧定义

---

## 7.6 查询服务 / 读模型接口（Query / Read Service）

### 定义层
`Application`

### 实现层
通常为 `Infrastructure`

### 适用范围
复杂列表查询、报表查询、分页检索、聚合只读模型。

### 命名规范

```text
I{BusinessConcept}QueryService
I{BusinessConcept}ReadService
```

### 示例

```csharp
/// <summary>
/// 包裹查询服务
/// </summary>
public interface IPackageQueryService
{
    Task<PagedResult<PackageDto>> QueryAsync(
        PackageQueryFilter filter,
        CancellationToken cancellationToken);
}
```

### 目录建议

定义位置：

```text
YourProject.Application/Abstractions/Queries/IPackageQueryService.cs
```

实现位置：

```text
YourProject.Infrastructure/Queries/PackageQueryService.cs
```

### 约束

- 查询接口不允许放在 `Domain`
- 查询 DTO 不允许放在 `Domain`
- 查询逻辑可直接面向读模型，不强制通过聚合仓储

---

## 7.7 当前用户 / 租户 / 权限上下文接口

### 定义层
`Application`

### 实现层
`Infrastructure`

### 适用范围
当前用户、当前租户、权限判定、请求上下文等应用协作能力。

### 命名规范

```text
ICurrentUserContext
ICurrentTenantContext
IPermissionChecker
```

### 示例

```csharp
/// <summary>
/// 当前用户上下文
/// </summary>
public interface ICurrentUserContext
{
    long? UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }
}
```

### 目录建议

```text
YourProject.Application/Abstractions/Security/ICurrentUserContext.cs
YourProject.Infrastructure/Security/HttpCurrentUserContext.cs
```

---

## 7.8 本地化、时间、标识生成等系统抽象

### 7.8.1 ILocalizationProvider

#### 定义层
`Application`

#### 实现层
`Infrastructure`

#### 说明
本地化属于应用交互能力，不属于领域核心概念。

### 7.8.2 IClock / IDateTimeProvider

#### 定义层
优先放 `Domain` 或 `Domain/SharedKernel`

#### 实现层
`Infrastructure`

#### 说明
时间往往参与领域规则，因此时间抽象更适合放在领域层。

### 7.8.3 IIdGenerator / ISequenceGenerator / IGuidGenerator

#### 定义层

- 用于领域对象标识生成：放 `Domain`
- 用于应用流水号、临时数据处理：放 `Application`

#### 实现层
`Infrastructure`

---

## 7.9 文件存储 / 导入导出接口

### 定义层
`Application`

### 实现层
`Infrastructure`

### 适用范围
文件上传、文件下载、Excel 导入导出、PDF 导出、对象存储等。

### 命名规范

```text
IFileStorageService
IExcelExporter
IExcelImporter
IPdfExportService
```

### 目录建议

```text
YourProject.Application/Abstractions/Storage/IFileStorageService.cs
YourProject.Application/Abstractions/Export/IExcelExporter.cs
YourProject.Infrastructure/Storage/LocalFileStorageService.cs
YourProject.Infrastructure/Export/NpoiExcelExporter.cs
```

---

## 7.10 集成事件 / 消息发布接口

### 定义层
`Application`

### 实现层
`Infrastructure`

### 适用范围
消息总线、集成事件、通知下发、跨系统消息传递。

### 命名规范

```text
IIntegrationEventPublisher
IMessageBus
INotificationSender
```

### 目录建议

```text
YourProject.Application/Abstractions/Messaging/IIntegrationEventPublisher.cs
YourProject.Infrastructure/Messaging/RabbitMqIntegrationEventPublisher.cs
```

---

## 7.11 第三方系统网关接口

### 定义层
`Application`

### 实现层
`Infrastructure`

### 适用范围
WMS、MES、ERP、设备平台、第三方 HTTP API、外部 SDK 适配等。

### 命名规范

```text
I{ExternalSystemName}Gateway
I{ExternalSystemName}Client
```

### 示例

```csharp
/// <summary>
/// WMS 网关
/// </summary>
public interface IWmsGateway
{
    Task<WmsAllocateChuteResult> AllocateChuteAsync(
        WmsAllocateChuteRequest request,
        CancellationToken cancellationToken);
}
```

### 目录建议

```text
YourProject.Application/Abstractions/Integrations/IWmsGateway.cs
YourProject.Infrastructure/Integrations/Wms/WmsGateway.cs
```

### 约束

- 外部系统 DTO 不允许泄漏到 `Domain`
- `Application` 不允许直接写 `HttpClient` 实现细节

---

## 7.12 缓存接口

### 情况 A：业务侧协作能力

#### 定义层
`Application`

#### 实现层
`Infrastructure`

#### 适用示例

```text
ICacheService
IDistributedLockService
```

### 情况 B：基础设施内部性能优化

#### 定义层
`Infrastructure`

#### 实现层
`Infrastructure`

#### 说明
仅供内部使用的缓存抽象不允许上浮到业务层。

---

## 7.13 设备驱动 / 协议编解码 / 通信适配接口

该类接口在工业控制与设备接入项目中必须严格区分业务抽象与技术抽象。

### 7.13.1 面向业务的设备能力抽象

#### 定义层
通常为 `Application`

#### 实现层
`Infrastructure`

#### 适用范围
业务编排需要调用的设备能力，例如信号塔、扫码器、轴控制器、提升机客户端等。

#### 命名示例

```text
ISignalTower
IBarcodeReaderManager
IAxisManager
IElevatorApiClient
```

#### 说明
业务层只面向“能力”，不面向报文和通信细节。

### 7.13.2 协议编解码接口

#### 定义层
`Infrastructure`

#### 实现层
`Infrastructure`

#### 适用范围
帧拼装、报文解析、CRC 计算、Modbus/TCP/串口协议封装等。

#### 命名规范

```text
I{Name}FrameCodec
I{Name}ProtocolParser
ICrcCalculator
```

#### 目录建议

```text
YourProject.Infrastructure/Devices/Protocols/Abstractions/IInfraredDriverFrameCodec.cs
YourProject.Infrastructure/Devices/Protocols/Codecs/LeadshineInfraredDriverFrameCodec.cs
```

### 约束

- 业务层不允许直接依赖 Hex、帧结构、CRC、原始寄存器地址
- 报文编解码、校验、帧构造必须留在 `Infrastructure`

---

## 8. 实现类放置规范

## 8.1 Domain 层实现类

### 允许放置

- 领域服务实现
- 领域规则实现
- 领域工厂实现
- 领域规格实现
- 值对象行为实现

### 禁止放置

- EF Repository
- `DbContext`
- Redis 实现
- 文件系统实现
- HTTP 网关实现
- 消息中间件实现
- 驱动通信实现
- 报文编解码实现

---

## 8.2 Application 层实现类

### 允许放置

- CommandHandler
- QueryHandler
- ApplicationService
- 用例协调器
- DTO 映射器
- 不依赖基础设施细节的纯应用逻辑

### 禁止放置

- SQL 实现
- Repository 实现
- `HttpClient` 具体实现
- Redis 具体实现
- 文件读写实现
- 串口/TCP 驱动实现

---

## 8.3 Infrastructure 层实现类

### 允许放置

- Repository 实现
- UnitOfWork 实现
- `DbContext`
- EF Core 配置
- 外部 HTTP API 实现
- 文件存储实现
- Redis 实现
- MQ 实现
- 本地化实现
- 权限实现
- 当前用户上下文实现
- 第三方 SDK 适配实现
- 设备驱动实现
- 协议帧编解码器
- CRC 计算器
- 通信客户端适配器

---

## 8.4 Host 层实现类

### 允许放置

- Controller
- Hub
- HostedService 启动编排
- 中间件
- DI 扩展
- 启动入口

### 禁止放置

- Repository 实现
- 驱动实现
- 网关实现
- 报文编解码实现
- 核心领域规则

---

## 9. 目录结构规范

## 9.1 Domain 层目录

```text
YourProject.Domain/
├── Aggregates/
├── Entities/
├── ValueObjects/
├── Events/
├── Services/
├── Policies/
├── Specifications/
├── Repositories/
├── Factories/
├── SharedKernel/
└── Exceptions/
```

---

## 9.2 Application 层目录

```text
YourProject.Application/
├── Abstractions/
│   ├── Persistence/
│   ├── Queries/
│   ├── Security/
│   ├── Storage/
│   ├── Messaging/
│   ├── Localization/
│   ├── Integrations/
│   ├── Export/
│   ├── Import/
│   └── Devices/
├── Commands/
├── Queries/
├── Dtos/
├── Services/
├── Mappers/
└── Behaviors/
```

---

## 9.3 Infrastructure 层目录

```text
YourProject.Infrastructure/
├── Persistence/
│   ├── DbContexts/
│   ├── Repositories/
│   ├── Configurations/
│   └── Migrations/
├── Queries/
├── Security/
├── Storage/
├── Messaging/
├── Localization/
├── Integrations/
├── Devices/
│   ├── Protocols/
│   │   ├── Abstractions/
│   │   ├── Codecs/
│   │   ├── Parsers/
│   │   └── Checksums/
│   ├── Drivers/
│   └── Adapters/
├── Caching/
└── DependencyInjection/
```

---

## 9.4 Host 层目录

```text
YourProject.Host/
├── Controllers/
├── Hubs/
├── HostedServices/
├── Middleware/
├── Options/
├── Extensions/
└── Program.cs
```

---

## 10. 命名规范

### 10.1 Repository

```text
I{Name}Repository
```

### 10.2 Query / Read Service

```text
I{Name}QueryService
I{Name}ReadService
```

### 10.3 Gateway / Client

```text
I{Name}Gateway
I{Name}Client
```

### 10.4 Domain Policy / Specification / Strategy

```text
I{Name}Policy
I{Name}Specification
I{Name}Strategy
```

### 10.5 Factory

```text
I{Name}Factory
```

### 10.6 协议编解码接口

```text
I{Name}FrameCodec
I{Name}ProtocolParser
```

---

## 11. Copilot 必须遵守的硬性规则

### 11.1 关于接口定义

1. 涉及聚合、实体、值对象持久化边界的接口，必须定义在 `Domain/Repositories`
2. 涉及领域规则、领域服务、领域策略、规格、工厂的接口，必须定义在 `Domain`
3. 涉及查询、权限、当前用户、本地化、文件存储、导入导出、消息发布、第三方网关、业务设备能力的接口，必须定义在 `Application/Abstractions`
4. 涉及报文编解码、CRC、协议解析、通信细节的接口，必须定义在 `Infrastructure`
5. 不允许为了方便实现而将 `Domain` 抽象上移到 `Application`

### 11.2 关于实现类

1. `Domain` 接口的技术实现必须放在 `Infrastructure`，纯规则实现可放在 `Domain`
2. `Application` 抽象的具体实现必须放在 `Infrastructure`，纯应用逻辑实现可放在 `Application`
3. `Host` 不允许承载 Repository、网关、驱动、编解码器实现
4. Controller、Hub、HostedService 只能依赖 `Application`，不允许直接依赖 Repository 实现类

### 11.3 关于依赖

1. `Domain` 不允许引用 `Application`
2. `Application` 不允许引用 `Infrastructure`
3. `Infrastructure` 只能实现抽象，不反向定义上层业务契约
4. `Host` 只负责启动、注册、适配和接入层逻辑

### 11.4 关于重复代码与迁移

如果新增代码会覆盖原有某些实现，必须同时执行以下动作：

1. 删除旧的重复接口
2. 删除旧的重复实现
3. 更新所有依赖注入注册
4. 更新所有调用方引用
5. 禁止新旧两套职责并存

---

## 12. 推荐统一基线

### 12.1 放在 Domain 的接口

- `IRepository`
- `I***Repository`
- `I***DomainService`
- `I***Policy`
- `I***Specification`
- `I***Factory`
- `IClock`
- `IIdGenerator`（用于领域标识时）

### 12.2 放在 Application 的接口

- `IUnitOfWork`
- `I***QueryService`
- `I***ReadService`
- `ICurrentUserContext`
- `ICurrentTenantContext`
- `IPermissionChecker`
- `ILocalizationProvider`
- `IFileStorageService`
- `IExcelExporter`
- `IExcelImporter`
- `IPdfExportService`
- `IIntegrationEventPublisher`
- `IMessageBus`
- `I***Gateway`
- `I***Client`
- `ICacheService`
- `IDistributedLockService`
- 业务设备抽象，例如 `ISignalTower`

### 12.3 放在 Infrastructure 的接口

- `I***FrameCodec`
- `I***ProtocolParser`
- `ICrcCalculator`
- `IModbusTransport`
- `ITcpPacketAssembler`
- 仅供基础设施内部协作的技术抽象

---

## 13. 可直接给 Copilot 的规则文本

```text
在本项目中严格遵守 DDD 分层规则：

1. Domain 层只定义核心领域模型与领域契约：
   - 聚合根、实体、值对象、领域事件、领域服务、领域策略、规格、工厂、仓储接口
   - 涉及聚合/实体持久化边界的接口必须定义在 Domain/Repositories
   - 领域规则相关接口必须定义在 Domain/Services、Policies、Specifications、Factories
   - Domain 不允许依赖 Application、Infrastructure、Host

2. Application 层只定义用例编排与外部协作抽象：
   - Command、Query、Handler、DTO、ApplicationService
   - 当前用户、权限、本地化、文件存储、导入导出、消息发布、第三方系统调用、查询服务等接口必须定义在 Application/Abstractions
   - Application 不允许依赖 Infrastructure

3. Infrastructure 层实现 Domain 与 Application 定义的抽象：
   - Repository、UnitOfWork、DbContext、EF 配置、HTTP 网关、缓存、文件存储、MQ、设备驱动、协议编解码器
   - 报文编解码、CRC、协议解析等技术抽象必须定义或保留在 Infrastructure 内部
   - 不允许把基础设施细节泄漏到 Domain 或 Application

4. Host 层只负责启动与组装：
   - Program、Controller、Hub、HostedService、DI 注册、中间件
   - 不允许在 Host 中实现 Repository、网关、驱动或核心业务规则

5. 若新增代码会覆盖旧实现，必须同步删除旧接口、旧实现和旧 DI 注册，禁止重复职责并存。

6. 输出代码时必须明确说明文件所属项目、分层和目录位置。
```

---

## 14. 落地要求

后续所有新增代码、重构代码、Copilot 生成代码、人工编写代码都必须遵守本规范。

如果项目中已存在以下问题：

- 仓储接口定义在 `Application`
- 领域策略接口定义在 `Application`
- 协议帧接口定义在 `Application`
- `Host` 中存在 Repository 或网关实现
- 同一职责存在多套接口和多套实现

则必须在后续重构中按本规范统一整改。

整改时必须执行：

1. 迁移接口到正确分层
2. 迁移实现到正确分层
3. 删除旧接口与旧实现
4. 更新 DI 注册
5. 更新全部调用方引用

禁止新旧结构长期并存。
