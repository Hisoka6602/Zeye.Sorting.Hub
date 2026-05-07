# PR-长期数据库底座 L 检查台账：Outbox 事件底座与业务事件持久化

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-L 切片，在 PR-K 写入幂等完成后继续补齐 Outbox 事件持久化、状态推进、失败隔离与健康探测基础能力。  
> **检查时间**：2026-05-06  
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
| PR-L Outbox 事件底座与业务事件持久化 | 本次已完成 | `Zeye.Sorting.Hub.Domain/Aggregates/Events/OutboxMessage.cs`、`Zeye.Sorting.Hub.Host/HostedServices/OutboxDispatchHostedService.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Domain/Aggregates/Events/OutboxMessage.cs`
- `Zeye.Sorting.Hub.Domain/Enums/Events/OutboxMessageStatus.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/IOutboxMessageRepository.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/OutboxMessageHealthSnapshotReadModel.cs`
- `Zeye.Sorting.Hub.Application/Services/Events/AppendOutboxMessageCommandService.cs`
- `Zeye.Sorting.Hub.Application/Services/Events/DispatchOutboxMessageCommandService.cs`
- `Zeye.Sorting.Hub.Application/Services/Events/GetOutboxMessagePagedQueryService.cs`
- `Zeye.Sorting.Hub.Application/Services/Events/OutboxMessageContractMapper.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Events/OutboxMessageCreateRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Events/OutboxMessageListRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Events/OutboxMessageListResponse.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Events/OutboxMessageResponse.cs`
- `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/OutboxMessageEntityTypeConfiguration.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/OutboxMessageRepository.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/OutboxDispatchHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/OutboxHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/OutboxMessageTests.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260506175929_AddOutboxMessageSupport.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260506175929_AddOutboxMessageSupport.Designer.cs`
- `检查台账/PR-长期数据库底座L-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Host/Routing/DataGovernanceApiRouteExtensions.cs`
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

1. 新增 `OutboxMessage` 聚合、`OutboxMessageStatus` 枚举与 `IOutboxMessageRepository` 仓储契约，统一定义事件类型、JSON 载荷、重试次数、失败消息、状态推进与死信隔离边界。
2. 新增 `AppendOutboxMessageCommandService`、`DispatchOutboxMessageCommandService`、`GetOutboxMessagePagedQueryService` 与 `OutboxMessageContractMapper`，形成“独立写入 → 原子领取 → 日志派发模拟 → 成功/失败/死信回写”的可复用 Outbox 链路。
3. 调整 `DataGovernanceApiRouteExtensions` 与 `Program.cs`，新增 `/api/data-governance/outbox-messages` 写入/查询接口，并接入 `OutboxDispatchHostedService` 与 `OutboxHealthCheck` 到运行期主链路。
4. 新增 `OutboxMessageEntityTypeConfiguration`、`OutboxMessageRepository` 与 EF 迁移 `20260506175929_AddOutboxMessageSupport.*`，补齐 `OutboxMessages` 表、状态并发令牌与状态/事件类型索引。
5. 新增 `OutboxMessageTests.cs`，覆盖写入、分页、状态推进、非法 JSON 死信与健康检查，并同步修正 README、更新记录与文件清单基线。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.OutboxMessageTests' -v minimal` ✅
- `dotnet ef migrations add AddOutboxMessageSupport --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Infrastructure --context SortingHubDbContext --output-dir Persistence/Migrations -- --provider MySql` ✅

---

## 五、PR-L 断点摘要

### 已完成
- OutboxMessage 聚合
- Outbox 仓储
- Outbox 状态流转
- Outbox 健康检查

### 保留能力
- 当前已提供独立写入入口与后台日志派发模拟，不依赖外部 MQ
- 可派发消息通过仓储原子领取，避免同一消息被重复推进到 Processing
- 非法 JSON 载荷会在有限重试后进入死信，避免无限重试

### 未完成但已预留
- PR-M Inbox 幂等消费底座
- 后续将 Outbox 写入并入真实业务事务的同 DbContext 协同链路

### 下一 PR 入口
- 下一 PR 从 PR-M“Inbox 幂等消费底座”开始
- 后续不要重复实现 Outbox 状态推进与健康快照查询，应复用 `DispatchOutboxMessageCommandService`、`GetOutboxMessagePagedQueryService` 与 `IOutboxMessageRepository`
