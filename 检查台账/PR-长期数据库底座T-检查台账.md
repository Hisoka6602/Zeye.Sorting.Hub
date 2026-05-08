# PR-长期数据库底座 T 检查台账：生产运行 Runbook、应急预案与最终底座验收

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-T 切片，在 PR-S 压测工程与性能基线报告完成后，继续补齐生产运行 Runbook、故障应急预案、分表治理手册、备份恢复演练资料与最终底座验收清单。  
> **检查时间**：2026-05-08  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs` |
| PR-B 查询保护与游标分页 | 已完成 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`、`Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs` |
| PR-C 批量写入缓冲与死信隔离 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/`、`Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs` |
| PR-D 分表巡检、预建与索引检查 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/`、`Zeye.Sorting.Hub.Host/HealthChecks/ShardingGovernanceHealthCheck.cs` |
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
| PR-O 备份、恢复、校验与演练底座 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/`、`Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs` |
| PR-P 报表查询隔离与只读副本预留 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/`、`Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs` |
| PR-Q 租户 / 站点 / 设备维度数据边界预留 | 已完成 | `Zeye.Sorting.Hub.Domain/ValueObjects/OperationalScope.cs`、`Zeye.Sorting.Hub.Application/Utilities/OperationalScopeNormalizer.cs` |
| PR-R 业务模块接入模板与代码生成规范 | 已完成 | `业务模块接入规范.md`、`Copilot-业务模块新增模板.md`、`Zeye.Sorting.Hub.Application/Utilities/ApplicationResult.cs` |
| PR-S 压测工程与性能基线报告 | 已完成 | `performance/README.md`、`性能基线报告.md`、`.github/workflows/performance-smoke-test.yml` |
| PR-T 生产运行 Runbook、应急预案与最终底座验收 | 本次已完成 | `生产运行Runbook.md`、`数据库故障应急预案.md`、`分表治理Runbook.md`、`备份恢复演练Runbook.md`、`业务接入前底座验收清单.md`、`无人值守运行检查清单.md` |

---

## 二、本次新增与修改文件

### 新增文件

- `生产运行Runbook.md`
- `数据库故障应急预案.md`
- `分表治理Runbook.md`
- `备份恢复演练Runbook.md`
- `业务接入前底座验收清单.md`
- `无人值守运行检查清单.md`
- `Zeye.Sorting.Hub.Host.Tests/ProductionReadinessRulesTests.cs`
- `检查台账/PR-长期数据库底座T-检查台账.md`

### 修改文件

- `.github/workflows/stability-gates.yml`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件

- 无

---

## 三、本次实现结果

1. 执行前先核对《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》、PR-R/S 台账与现有仓库状态，确认当前已完成到 PR-S，再集中补齐 PR-T 缺口。
2. 新增 `生产运行Runbook.md`，覆盖服务启动失败、数据库连接失败、连接池耗尽、慢查询暴增、写入队列积压、死信堆积、分表缺失、索引缺失、磁盘空间不足、备份失败、迁移失败、归档任务失败、审计日志过大、查询 P99 升高、内存持续增长、CPU 持续过高、数据重复写入、幂等冲突、Outbox 堆积、Inbox 重复消费共 20 个场景。
3. 新增 `数据库故障应急预案.md`、`分表治理Runbook.md`、`备份恢复演练Runbook.md` 与 `无人值守运行检查清单.md`，将数据库底座能力沉淀为可交接、可演练、可巡检的生产运行资料。
4. 新增 `业务接入前底座验收清单.md`，把 PR-A ～ PR-T 当前完成度核对与最终验收条目集中沉淀，作为正式业务模块开发前的统一放行依据。
5. 新增 `ProductionReadinessRulesTests.cs`，并调整 `stability-gates.yml` 触发路径，避免 PR-T 生产运行资料、验收清单与门禁上下文发生回退。
6. 同步更新 README、更新记录、文件清单基线与 PR-T 台账，保证长期数据库底座多 PR 路线图可在 PR-T 处完整收口。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build -v quiet --filter "FullyQualifiedName~ProductionReadinessRulesTests"` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `./.github/scripts/validate-database-foundation-rules.sh` ✅

---

## 五、PR-T 断点摘要

### 已完成

- 生产运行 Runbook
- 数据库故障应急预案
- 分表治理 Runbook
- 备份恢复演练 Runbook
- 业务接入前底座验收清单
- 无人值守运行检查清单

### 保留能力

- 当前已将数据库底座的运行、排障、演练、验收入口统一沉淀为独立文档，后续业务模块接入时无需重复设计同义运行手册。
- `ProductionReadinessRulesTests.cs` 与 `stability-gates.yml` 已对 PR-T 资料建立最小回归约束，后续修改运行资料时仍会触发门禁检查。

### 未完成但已预留

- 长期数据库底座多 PR 路线图已完成；后续进入正式业务模块开发时，仍需持续补充季度演练记录与真实运行复盘。

### 下一 PR 入口

- 长期数据库底座路线图已在 PR-T 收口，后续从具体业务模块开发开始。
- 开始业务模块开发前，必须先通过 `业务接入前底座验收清单.md` 并遵守 `业务模块接入规范.md`。
