# Copilot 限制规则

1. 全项目禁止使用 UTC 时间语义和 UTC 相关 API。
2. 任何新增或修改涉及时间的代码，必须保持本地时间语义一致，不得引入 UTC 转换链路。读取配置中的时间字符串时，默认按本地时间解析；示例配置不得使用 `Z` 或 offset（如 `+08:00`）。
3. 每次新增文件或删除文件后，必须同步更新仓库根目录 `README.md` 中用于逐项说明目录/文件职责的章节（当前标题为“解决方案文件树与职责”），保证职责说明与仓库实际内容一致。
4. 所有从 doc/pdf 文档解析到 md 的内容都必须能在原文档中找到出处。
5. 所有的方法都需要有注释，复杂的实现方法必须要有步骤注释。
6. 全局禁止代码重复（影分身代码/复制粘贴代码）。
7. 小工具类尽量代码简洁和做到高性能、高复用。
8. 所有枚举都需要定义在 `Zeye.Sorting.Hub.Domain.Enums` 的子目录下。
9. 所有枚举都必须包含 `Description` 和注释。
10. 所有事件载荷都必须定义在 `Events` 的子目录下。
11. 事件载荷需要使用 `readonly record struct`（确保不可变、值语义与更优内存性能）。
12. 所有的异常都必须输出日志。
13. Copilot的回答/描述/交流都需要使用中文
14. 所有的类的字段都必须有注释
15. 日志只能使用Nlog,日志不能影响程序性能(无论日志输出多频繁)
16. Copilot任务默认由 Copilot 创建拉取请求（PR）
17. Copilot 每次修改代码后都需要检查是否影分身代码，如果有则需要删除修复
18. 严格划分结构层级边界，尽量做到0入侵（非常重要）
19. 有性能更高的特性标记需要尽量使用，追求极致性能
20. 注释中禁止出现第二人称的字眼
21. 对字段、类型、文件、项目的命名有严格要求，必须符合专业领域术语
22. 历史更新记录不要写在README.md中
23. 相同意义的工具代码需要提取集中,不可以到处实现
24. swagger的所有参数、方法、枚举项都必须要有中文注释
25. 每个类都需要独立的文件,不能多个类放在同一个文件内
26. md文件除README.md外,其他md文件都需要使用中文命名
27. 禁止使用过时标记去标记代码,如果代码已过时则必须删除,调用新的实现
28. 项目内所有代码文件的命名空间必须与物理目录层级严格一致
29. 禁止在热路径读写配置文件和数据库
30. 每个配置项的注释都需要写明可填写的范围，枚举类型需要列出所有枚举项
31. 存盘日志文件大小不能超过 10 MB，超过必须触发轮转；NLog 所有落盘 File target 必须设置 `archiveAboveSize="10485760"`，归档编号必须使用 `DateAndSequence` 以支持同天多次轮转。
# Copilot Repository Instructions

## 时间处理硬性规则

- 全项目禁止使用 UTC 时间语义和 UTC 相关 API，包括但不限于：
  - `DateTime.UtcNow`
  - `DateTimeOffset.UtcNow`
  - `DateTimeKind.Utc`
  - `ToUniversalTime()`
  - `UtcDateTime`
  - `DateTimeStyles.AssumeUniversal`
  - `DateTimeStyles.AdjustToUniversal`
- 统一使用本地时间语义，包括但不限于：
  - `DateTime.Now`
  - `DateTimeKind.Local`
  - `DateTimeStyles.AssumeLocal`

## 代码修改要求

- 任何新增或修改涉及时间的代码，必须保持本地时间语义一致，不得引入 UTC 转换链路。
- 如果读取配置中的时间字符串，默认按本地时间解析；示例配置不得使用 `Z` 或 offset（如 `+08:00`）。
- 每次新增文件或删除文件后，必须同步更新仓库根目录 `README.md` 中用于逐项说明目录/文件职责的章节（当前标题为“各层级与各文件作用说明（逐项）”），保证职责说明与仓库实际内容一致。
- 所有代码文件的命名空间必须与项目内目录路径保持一致；新建/移动文件时必须同步修正 namespace 与对应 using 引用。

## 日志落盘规范

- 所有业务日志、异常日志、后台任务日志、数据库相关日志都必须落盘，禁止仅输出到控制台不落盘。
- 存盘日志文件大小不能超过 10 MB，超过必须触发轮转（即滚动写入新文件）。
- NLog 所有 `xsi:type="File"` target 必须同时满足：
  - `archiveAboveSize="10485760"`（10 MB 尺寸上限）
  - `archiveNumbering="DateAndSequence"`（支持同天多次轮转，避免覆盖）
  - 同时保留 `archiveEvery="Day"`（同天多次+每日归档双保险）

## DDD 分层接口与实现放置规范（强制）

- 必须严格遵守依赖方向：`Host -> Infrastructure -> Application -> Domain`。
- `Domain` 不允许依赖 `Application`、`Infrastructure`、`Host`；`Application` 不允许依赖 `Infrastructure`、`Host`。
- 接口定义必须按语义归属放置，禁止“为了实现方便”上移或下沉。

### 抽象位置（接口定义）强约束

- `Domain`（领域抽象）：
  - 仓储接口：`Domain/Repositories`
  - 领域服务接口：`Domain/Services`
  - 领域策略/规格/规则接口：`Domain/Policies`、`Domain/Specifications`
  - 领域工厂接口：`Domain/Factories`
  - 领域时间/标识等共享抽象：`Domain/SharedKernel`
- `Application/Abstractions`（应用协作抽象）：
  - `Persistence`：`IUnitOfWork`
  - `Queries`：查询/读模型接口
  - `Security`：当前用户/租户/权限上下文接口
  - `Localization`：本地化接口
  - `Storage` / `Export` / `Import`：文件与导入导出接口
  - `Messaging`：消息发布/通知接口
  - `Integrations`：第三方系统网关/客户端接口
  - `Devices`：面向业务的设备能力抽象
- `Infrastructure`（技术细节抽象，仅限内部）：
  - 协议编解码接口、协议解析接口、CRC 计算接口、通信传输接口
  - 基础设施内部缓存/性能抽象（不得上浮为业务契约）

### 实现位置（类放置）强约束

- `Domain` 允许：纯领域规则实现（领域服务、策略、规格、工厂、值对象行为）。
- `Domain` 禁止：`DbContext`、EF/SQL、Redis、HTTP、文件系统、MQ、驱动通信、协议编解码实现。
- `Application` 允许：Command/Query Handler、ApplicationService、用例编排、DTO 映射。
- `Application` 禁止：仓储实现、SQL/EF 具体实现、`HttpClient` 具体实现、Redis/文件/驱动通信实现。
- `Infrastructure` 负责实现 `Domain` 与 `Application` 抽象：Repository、UnitOfWork、DbContext、网关、缓存、消息、文件、设备驱动、协议编解码。
- `Host` 仅允许入口与组装代码（Program、Controller、Hub、HostedService、DI、中间件），禁止承载仓储实现、网关实现、驱动实现、协议编解码实现和核心领域规则实现。

### 命名规则（必须遵守）

- 仓储接口：`I{Name}Repository`
- 领域服务接口：`I{Name}DomainService`
- 领域策略/规格/策略接口：`I{Name}Policy` / `I{Name}Specification` / `I{Name}Strategy`
- 领域工厂接口：`I{Name}Factory`
- 查询/读服务接口：`I{Name}QueryService` / `I{Name}ReadService`
- 外部网关/客户端接口：`I{Name}Gateway` / `I{Name}Client`
- 协议编解码/解析接口：`I{Name}FrameCodec` / `I{Name}ProtocolParser`

### 目录与分层边界建议（默认采用）

- `Domain`：`Aggregates/`、`Entities/`、`ValueObjects/`、`Events/`、`Services/`、`Policies/`、`Specifications/`、`Repositories/`、`Factories/`、`SharedKernel/`、`Exceptions/`
- `Application`：`Abstractions/`（按 Persistence/Queries/Security/Storage/Messaging/Localization/Integrations/Export/Import/Devices 子目录细分）、`Commands/`、`Queries/`、`Dtos/`、`Services/`、`Mappers/`、`Behaviors/`
- `Infrastructure`：`Persistence/`（DbContexts/Repositories/Configurations/Migrations）、`Queries/`、`Security/`、`Storage/`、`Messaging/`、`Localization/`、`Integrations/`、`Devices/`（Protocols/Abstractions/Codecs/Parsers/Checksums）、`Caching/`、`DependencyInjection/`
- `Host`：`Controllers/`、`Hubs/`、`HostedServices/`、`Middleware/`、`Options/`、`Extensions/`、`Program.cs`

### 明确禁止项（强制）

- 禁止将所有接口统一放到 `Application`。
- 禁止 `Domain` 引用 `Application` 抽象或 DTO。
- 禁止基础设施实现细节（EF、Redis、HTTP、MQ、文件系统、串口/TCP、报文格式）泄漏到 `Domain` 或 `Application`。
- 禁止 `Host` 承载核心业务实现或基础设施实现。
- 禁止仓储接口暴露 `IQueryable`。
- 查询接口与查询 DTO 禁止放在 `Domain`。
- 严禁重复职责并存：若新增代码覆盖旧实现，必须同时删除旧接口、旧实现、旧 DI 注册并更新调用方引用。
- Controller、Hub、HostedService 只能依赖 `Application` 抽象，禁止直接依赖仓储实现类或基础设施实现细节。

## PR 交付门禁（必须全部满足）

- 先输出“实施计划（Plan）”，再改代码；每完成一步更新进度。
- 所有改动文件必须通过编译；若无法编译，必须说明阻塞原因与替代验证。
- 每个 PR 必须附“验收清单（Checklist）”，逐条标注 [x]/[ ]。
- 涉及数据库 DDL、批量删除、批量更新、外部调用重试策略等危险动作，必须走隔离器（开关 + dry-run + 审计 + 回滚脚本）。
- 可使用 `var` 的地方优先使用 `var`（不降低可读性前提下）。
- 新增/删除文件后，必须更新 README 的文件树与逐文件职责，并新增“本次更新内容 / 后续可完善点”。
- 若需求不明确，先提出“待确认项”；未确认项不得默认实现。

## 禁止事项：
- 禁止创建 XxxManager / XxxHelper / XxxWrapper / XxxAdapter / XxxFacade，除非确实消除了重复并降低复杂度
- 禁止新增只做一层调用转发的方法
- 禁止把旧实现保留，再新建一套同义实现
- 禁止用“复制一份再微调”的方式支持 MySQL/SQL Server
- 禁止为了通过当前任务而牺牲整体结构一致性

## 前置检查

- 每3次PR必须做一次 .github/copilot-instructions.md 约束的违规检查,并处理代码的违规项
