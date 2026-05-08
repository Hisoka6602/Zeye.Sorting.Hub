# PR-长期数据库底座 R 检查台账：业务模块接入模板与代码生成规范

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-R 切片，在 PR-Q 运营边界建模完成后继续补齐业务模块接入模板、统一结果模型与路由约定。  
> **检查时间**：2026-05-08  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs` |
| PR-B 查询保护与游标分页 | 已完成 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`、`Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs` |
| PR-C 批量写入缓冲与死信隔离 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/`、`Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs` |
| PR-D 分表巡检、预建与索引检查 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTableInspectionService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/ShardingGovernanceHealthCheck.cs` |
| PR-E 数据归档与冷热分层 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/`、`Zeye.Sorting.Hub.Host/Routing/DataGovernanceApiRouteExtensions.cs` |
| PR-F 数据库底座 CI 门禁增强 | 已完成 | `.github/workflows/database-foundation-gates.yml`、`.github/scripts/validate-database-foundation-rules.sh` |
| PR-G 数据库迁移治理与回滚资产 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/`、`Zeye.Sorting.Hub.Host/HostedServices/MigrationGovernanceHostedService.cs` |
| PR-H 种子数据、基线数据与配置一致性校验 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/`、`Zeye.Sorting.Hub.Host/HostedServices/BaselineDataValidationHostedService.cs` |
| PR-I 慢查询指纹聚合与查询画像 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileStore.cs`、`Zeye.Sorting.Hub.Host/Routing/DiagnosticsApiRouteExtensions.cs` |
| PR-J 查询模板治理与索引建议闭环 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/`、`Zeye.Sorting.Hub.Host/HostedServices/QueryGovernanceReportHostedService.cs` |
| PR-K 写入幂等、去重与重复键治理 | 已完成 | `Zeye.Sorting.Hub.Application/Services/Idempotency/IdempotencyGuardService.cs`、`Zeye.Sorting.Hub.Infrastructure/Repositories/IdempotencyRepository.cs` |
| PR-L Outbox 事件底座与业务事件持久化 | 已完成 | `Zeye.Sorting.Hub.Domain/Aggregates/Events/OutboxMessage.cs`、`Zeye.Sorting.Hub.Host/HostedServices/OutboxDispatchHostedService.cs` |
| PR-M Inbox 幂等消费底座 | 已完成 | `Zeye.Sorting.Hub.Domain/Aggregates/Events/InboxMessage.cs`、`Zeye.Sorting.Hub.Application/Services/Events/InboxMessageGuardService.cs` |
| PR-N 数据保留策略与自动清理治理 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/`、`Zeye.Sorting.Hub.Host/HostedServices/DataRetentionHostedService.cs` |
| PR-O 备份、恢复、校验与演练底座 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/`、`Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs` |
| PR-P 报表查询隔离与只读副本预留 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/`、`Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs` |
| PR-Q 租户 / 站点 / 设备维度数据边界预留 | 已完成 | `Zeye.Sorting.Hub.Domain/ValueObjects/OperationalScope.cs`、`Zeye.Sorting.Hub.Application/Utilities/OperationalScopeNormalizer.cs`、`Zeye.Sorting.Hub.Host.Tests/OperationalScopeTests.cs` |
| PR-R 业务模块接入模板与代码生成规范 | 本次已完成 | `业务模块接入规范.md`、`Copilot-业务模块新增模板.md`、`Zeye.Sorting.Hub.Application/Utilities/ApplicationResult.cs`、`Zeye.Sorting.Hub.Host/Routing/EndpointRouteBuilderConventionExtensions.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `业务模块接入规范.md`
- `Copilot-业务模块新增模板.md`
- `Zeye.Sorting.Hub.Application/Utilities/ApplicationErrorCodes.cs`
- `Zeye.Sorting.Hub.Application/Utilities/ApplicationResult.cs`
- `Zeye.Sorting.Hub.Host/Routing/EndpointRouteBuilderConventionExtensions.cs`
- `Zeye.Sorting.Hub.Host.Tests/BusinessModuleTemplateRulesTests.cs`
- `检查台账/PR-长期数据库底座R-检查台账.md`

### 修改文件
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `业务模块接入规范.md`，统一定义业务模块目录结构、DDD 分层边界、查询/写入治理、Outbox / Inbox / WriteBuffer 复用与统一错误处理要求。
2. 新增 `Copilot-业务模块新增模板.md`，沉淀新增模块时应直接复用的任务模板，要求先核对长期数据库底座断点，再按统一规则接入模块。
3. 新增 `ApplicationErrorCodes` 与 `ApplicationResult`，统一应用层稳定错误码、失败状态码与 ProblemDetails 标题，避免后续模块重复设计错误协议。
4. 新增 `EndpointRouteBuilderConventionExtensions`，集中处理业务模块路由组标签、端点名称/说明声明与应用层失败结果到统一 ProblemDetails 的映射。
5. 新增 `BusinessModuleTemplateRulesTests.cs`，覆盖统一结果模型、路由约定与模板文档关键规则，并同步更新 README、更新记录、文件清单基线与 PR-R 台账。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj -v quiet --filter "FullyQualifiedName~BusinessModuleTemplateRulesTests"` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `./.github/scripts/validate-database-foundation-rules.sh` ✅

---

## 五、PR-R 断点摘要

### 已完成
- 业务模块标准结构规范
- Copilot 新增模块模板
- 应用层统一结果模型
- Endpoint 路由约定

### 保留能力
- 当前已统一提供 `ApplicationResult`、`ApplicationErrorCodes` 与 `EndpointRouteBuilderConventionExtensions`，后续业务模块可直接复用统一错误协议与路由声明。
- `业务模块接入规范.md` 与 `Copilot-业务模块新增模板.md` 已明确要求复用 `OperationalScopeNormalizer`、`IdempotencyGuardService`、WriteBuffer、Outbox、Inbox 与只读副本预算守卫。

### 未完成但已预留
- PR-S 压测工程与性能基线报告
- 后续真实业务模块仍需结合具体场景补充聚合、仓储与合同，但必须沿用本次模板与接入规范

### 下一 PR 入口
- 下一 PR 从 PR-S“压测工程与性能基线报告”开始
- 后续不要重复定义业务模块私有错误协议、路由约定与新增模块模板，应复用 `ApplicationResult`、`ApplicationErrorCodes` 与 `EndpointRouteBuilderConventionExtensions`
