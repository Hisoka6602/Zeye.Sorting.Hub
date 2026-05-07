# PR-长期数据库底座 N 检查台账：数据保留策略与自动清理治理

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-N 切片，在 PR-M Inbox 幂等消费底座完成后继续补齐数据保留策略、dry-run 自动清理治理与运行期健康探测基础能力。  
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
| PR-M Inbox 幂等消费底座 | 已完成 | `Zeye.Sorting.Hub.Domain/Aggregates/Events/InboxMessage.cs`、`Zeye.Sorting.Hub.Application/Services/Events/InboxMessageGuardService.cs` |
| PR-N 数据保留策略与自动清理治理 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/`、`Zeye.Sorting.Hub.Host/HostedServices/DataRetentionHostedService.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionPolicy.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionPlanner.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionExecutor.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionAuditRecord.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/DataRetentionHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/DataRetentionHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/DataRetentionTests.cs`
- `检查台账/PR-长期数据库底座N-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileStore.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `DataRetentionOptions` 与 `DataRetentionPolicy`，为 WebRequestAuditLog、OutboxMessage、InboxMessage、IdempotencyRecord、ArchiveTask、DeadLetterWriteEntry、SlowQueryProfile 提供统一的批次上限、轮询间隔与保留天数配置入口。
2. 新增 `DataRetentionPlanner` 与 `DataRetentionExecutor`，按策略统计各类长期数据的 dry-run 候选数量，并将最近一次治理结果写入 `DataRetentionAuditRecord` 供后台服务与健康检查复用。
3. 新增 `DataRetentionHostedService` 与 `DataRetentionHealthCheck`，将数据保留治理接入运行期后台循环与 `/health/ready`，保证失败仅记日志和状态，不影响主服务运行。
4. 扩展 `SlowQueryProfileStore` 的保留候选计数能力，并调整 `PersistenceServiceCollectionExtensions`、`Program.cs` 与 `appsettings.json`，完成配置校验、依赖注入、托管服务与健康检查接线。
5. 新增 `DataRetentionTests.cs`，覆盖多策略候选统计、dry-run 执行记录与健康检查状态，并同步修正 README、更新记录与文件清单基线。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v minimal` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.DataRetentionTests' -v minimal` ✅

---

## 五、PR-N 断点摘要

### 已完成
- 数据保留策略配置
- 多策略 dry-run 候选统计
- 数据保留治理后台服务
- 数据保留治理健康检查

### 保留能力
- 当前已统一纳管 WebRequestAuditLog、Outbox、Inbox、幂等记录、归档任务、死信与慢查询画像的保留策略入口
- 后台服务默认仅执行 dry-run，不会触发真实删除，失败仅写入日志与状态记录
- `/health/ready` 已可输出最近一次治理时间、各策略候选数量与 dry-run 状态

### 未完成但已预留
- PR-O 备份、恢复、校验与演练底座
- 后续接入危险动作隔离器驱动的真实批量删除与审计落盘

### 下一 PR 入口
- 下一 PR 从 PR-O“备份、恢复、校验与演练底座”开始
- 后续不要重复实现数据保留策略名归一化、候选统计与运行期状态缓存，应复用 `DataRetentionPolicy`、`DataRetentionPlanner` 与 `DataRetentionExecutor`
