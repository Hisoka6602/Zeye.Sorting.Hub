# PR-长期数据库底座 N 检查台账：数据保留策略与自动清理治理

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-N 切片，在 PR-M Inbox 幂等消费底座完成后继续补齐数据保留策略、自动清理治理与运行期健康检查基础能力。  
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
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/DeadLetterWriteStore.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `DataRetentionOptions`、`DataRetentionPolicy`、`DataRetentionPlanner`、`DataRetentionExecutor` 与 `DataRetentionAuditRecord`，统一治理 `WebRequestAuditLog`、`OutboxMessage`、`InboxMessage`、`IdempotencyRecord`、`ArchiveTask`、`DeadLetterWriteEntry` 与 `SlowQueryProfile` 的保留策略、计划统计与审计记录。
2. 新增 `DataRetentionHostedService` 与 `DataRetentionHealthCheck`，形成“后台轮询执行 → 危险动作守卫决策 → dry-run / 真删 → 最近一次审计暴露到 `/health/ready`”的运行期治理链路。
3. 调整 `PersistenceServiceCollectionExtensions.cs` 与 `Program.cs`，将数据保留治理能力接入基础设施注册与宿主启动流程，并补齐 `Persistence:Retention` 默认配置节。
4. 扩展 `DeadLetterWriteStore` 与 `SlowQueryProfileStore`，集中提供候选统计与受限批次清理接口，避免在治理链路内重复实现同义内存裁剪逻辑。
5. 新增 `DataRetentionTests.cs`，覆盖守卫关闭时的 dry-run、真实执行删除与健康检查禁用场景，并同步修正 README、更新记录、文件清单基线与 PR-N 台账。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --filter "FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.DataRetentionTests" -v minimal` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（287/287）

---

## 五、PR-N 断点摘要

### 已完成
- 数据保留策略
- 清理计划
- dry-run 执行器
- 保留治理健康检查

### 保留能力
- 当前已统一支持数据库内实体与内存态死信/慢查询画像的候选统计与受限批次治理入口
- 危险动作守卫优先于 dry-run，真实清理必须显式放开 `AllowDangerousActionExecution`
- 最近一次治理结果会以审计记录形式缓存在进程内，供健康检查与后续治理观测复用

### 未完成但已预留
- PR-O 备份、恢复、校验与演练底座
- 后续可将数据保留审计记录继续沉淀为可查询的持久化资产或只读接口

### 下一 PR 入口
- 下一 PR 从 PR-O“备份、恢复、校验与演练底座”开始
- 后续不要重复实现保留策略解析、危险动作决策与分策略清理循环，应复用 `DataRetentionExecutor` 与 `DataRetentionPlanner`
