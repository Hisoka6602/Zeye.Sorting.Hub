# PR-长期数据库底座 O 检查台账：备份、恢复、校验与演练底座

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-O 切片，在 PR-N 数据保留策略与自动清理治理完成后继续补齐备份计划、命令生成、备份校验与恢复演练底座。  
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
| PR-N 数据保留策略与自动清理治理 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/`、`Zeye.Sorting.Hub.Host/HostedServices/DataRetentionHostedService.cs` |
| PR-O 备份、恢复、校验与演练底座 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/`、`Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupPlan.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupExecutionRecord.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/IBackupProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupConnectionStringParser.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/MySqlBackupProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/SqlServerBackupProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupVerificationService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/RestoreDrillPlanner.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/BackupGovernanceTests.cs`
- `检查台账/PR-长期数据库底座O-检查台账.md`

### 修改文件
- `.github/workflows/stability-gates.yml`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `BackupOptions`、`BackupPlan`、`BackupExecutionRecord`、`IBackupProvider` 与 `BackupConnectionStringParser`，统一定义备份目录、文件名前缀、最近备份时效、演练目录、备份命令生成与恢复 Runbook 入口。
2. 新增 `MySqlBackupProvider` 与 `SqlServerBackupProvider`，分别生成 Provider 专属的备份命令与恢复 Runbook，并在命令文本中使用 `<PASSWORD>` 占位避免泄露敏感信息。
3. 新增 `BackupVerificationService` 与 `RestoreDrillPlanner`，统一校验最近备份文件、最近恢复演练记录与当前治理状态；当前阶段默认 dry-run，尚未接入真实危险恢复执行。
4. 新增 `BackupHostedService` 与 `BackupHealthCheck`，将备份治理接入运行期后台循环与 `/health/ready`；缺少恢复演练记录或真实备份超期时返回 Degraded，满足“备份失败必须 Degraded / 超过预期未备份必须告警”约束。
5. 调整 `PersistenceServiceCollectionExtensions.cs`、`Program.cs`、`appsettings.json` 与 `stability-gates.yml`，补齐配置校验、依赖注入、托管服务、健康检查与稳定性门禁中的备份治理关键配置检查。
6. 新增 `BackupGovernanceTests.cs`，覆盖 MySQL/SQL Server 命令生成、dry-run 成功路径与健康检查降级路径，并同步修正 README、更新记录与文件清单基线。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v minimal` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.BackupGovernanceTests' -v minimal` ✅

---

## 五、PR-O 断点摘要

### 已完成
- 备份计划
- Provider 备份命令生成
- 备份校验
- 恢复演练记录校验

### 保留能力
- 当前已按 Provider 区分 MySQL / SQL Server 备份命令与恢复 Runbook 生成逻辑
- 当前默认仅执行 dry-run，不会自动覆盖生产库或触发危险恢复动作
- `/health/ready` 已可输出预期备份文件、最近备份文件、演练记录与治理状态

### 未完成但已预留
- PR-P 报表查询隔离与只读副本预留
- 后续接入危险动作隔离器驱动的真实备份执行、备份结果落盘与归档校验

### 下一 PR 入口
- 下一 PR 从 PR-P“报表查询隔离与只读副本预留”开始
- 后续不要重复实现连接字符串解析、备份命令生成与演练记录查找，应复用 `BackupConnectionStringParser`、`IBackupProvider` 与 `RestoreDrillPlanner`
