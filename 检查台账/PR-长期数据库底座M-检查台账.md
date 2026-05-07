# PR-长期数据库底座 M 检查台账：Inbox 幂等消费底座

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-M 切片，在 PR-L Outbox 事件底座完成后继续补齐 Inbox 幂等消费、失败重试与过期治理基础能力。  
> **检查时间**：2026-05-07  
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
| PR-M Inbox 幂等消费底座 | 本次已完成 | `Zeye.Sorting.Hub.Domain/Aggregates/Events/InboxMessage.cs`、`Zeye.Sorting.Hub.Application/Services/Events/InboxMessageGuardService.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Domain/Aggregates/Events/InboxMessage.cs`
- `Zeye.Sorting.Hub.Domain/Enums/Events/InboxMessageStatus.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/IInboxMessageRepository.cs`
- `Zeye.Sorting.Hub.Application/Services/Events/InboxMessageGuardService.cs`
- `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/InboxMessageEntityTypeConfiguration.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/InboxMessageRepository.cs`
- `Zeye.Sorting.Hub.Host.Tests/InboxMessageTests.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260507021744_AddInboxMessageSupport.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260507021744_AddInboxMessageSupport.Designer.cs`
- `检查台账/PR-长期数据库底座M-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryErrorCodes.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/SortingHubDbContextModelSnapshot.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `InboxMessage` 聚合、`InboxMessageStatus` 枚举与 `IInboxMessageRepository` 仓储契约，统一定义 `SourceSystem + MessageId` 唯一消息键、消费状态、失败消息、重试次数与过期治理时间。
2. 新增 `InboxMessageGuardService`，形成“首次创建 Pending → 进入 Processing → 成功回放 / 处理中拒绝 / 失败重试”的可复用 Inbox 幂等消费守卫链路。
3. 新增 `InboxMessageRepository` 与 `InboxMessageEntityTypeConfiguration`，补齐唯一消息键冲突识别、状态更新与过期治理候选查询能力，并复用共享重复键检测工具避免影分身实现。
4. 调整 `PersistenceServiceCollectionExtensions.cs` 与 `Program.cs`，将 Inbox 仓储与守卫服务接入运行期容器，保证后续外部事件消费链路可直接复用。
5. 新增 EF 迁移 `20260507021744_AddInboxMessageSupport.*` 与 `InboxMessageTests.cs`，覆盖首次消费、成功回放、处理中拒绝、失败重试与过期治理候选，并同步修正 README、更新记录与文件清单基线。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.InboxMessageTests' -v minimal` ✅
- `dotnet ef migrations add AddInboxMessageSupport --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Infrastructure --context SortingHubDbContext --output-dir Persistence/Migrations -- --provider MySql` ✅

---

## 五、PR-M 断点摘要

### 已完成
- InboxMessage 聚合
- Inbox 幂等消费记录
- 消费状态治理
- 过期治理候选查询

### 保留能力
- 当前已提供 `SourceSystem + MessageId` 唯一消息键保护，不依赖外部 MQ 即可建立 Inbox 消费底座
- 重复消息在成功态下支持回放已有结果，处理中消息会明确拒绝，失败消息可在重试上限内重新接管消费
- 记录已内置 `ExpiresAt` 本地时间字段，后续 PR-N 可直接复用过期治理候选查询结果规划清理任务

### 未完成但已预留
- PR-N 数据保留策略与自动清理治理
- 后续将 Inbox 守卫接入真实外部事件消费链路与运行期调度链路

### 下一 PR 入口
- 下一 PR 从 PR-N“数据保留策略与自动清理治理”开始
- 后续不要重复实现消息键唯一校验、消费状态流转与过期候选查询，应复用 `InboxMessageGuardService` 与 `IInboxMessageRepository`
