# PR-长期数据库底座 O 检查台账：备份、恢复、校验与演练底座

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-O 切片，在 PR-N 数据保留策略与自动清理治理完成后继续补齐备份、恢复、校验与演练底座。  
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
| PR-O 备份、恢复、校验与演练底座 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/`、`Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupPlan.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupExecutionRecord.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/IBackupProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/MySqlBackupProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/SqlServerBackupProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupFileNamePolicy.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupConnectionStringValueReader.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupCommandTextFormatter.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/RestoreDrillPlanner.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupVerificationService.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/BackupGovernanceTests.cs`
- `检查台账/PR-长期数据库底座O-检查台账.md`

### 修改文件
- `.gitignore`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `BackupOptions`、`BackupPlan`、`BackupExecutionRecord`、`IBackupProvider`、`MySqlBackupProvider`、`SqlServerBackupProvider` 与 `BackupCommandTextFormatter`，统一建立备份治理配置、数据库名解析、Provider 级命令生成、Shell 参数 / SQL 字面量转义与备份文件命名规则。
2. 新增 `BackupVerificationService` 与 `RestoreDrillPlanner`，形成“禁用时短路 → 生成备份计划 → 校验最新备份文件 → 输出恢复 Runbook / 演练记录 → 缓存最近一次执行记录”的治理链路，并将资产写入改为临时文件 + 原子替换。
3. 新增 `BackupHostedService` 与 `BackupHealthCheck`，将备份治理能力接入宿主后台轮询与 `/health/ready` 健康探针，缺失备份文件或备份超龄时返回 Degraded。
4. 调整 `.gitignore`、`PersistenceServiceCollectionExtensions.cs`、`Program.cs` 与 `appsettings.json`，为 `Persistence/Backup/` 源码目录解除误忽略并补齐 `Persistence:Backup` 默认配置与 DI 接线，支持按 MySQL / SQL Server 分别注册备份 Provider。
5. 新增 `BackupGovernanceTests.cs`，覆盖 MySQL/SQL Server Provider 命令生成安全性、禁用场景无连接串、最新备份文件校验、Runbook/演练记录输出与健康检查状态，并同步修正 README、更新记录与文件清单基线。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter "FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.BackupGovernanceTests" -v normal` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build --logger "trx;LogFileName=/tmp/zeye-full-tests.trx" -v quiet` ✅（291/291）
- `./.github/scripts/validate-database-foundation-rules.sh` ✅

---

## 五、PR-O 断点摘要

### 已完成
- 备份计划
- Provider 备份命令生成
- 备份校验
- 恢复演练记录

### 保留能力
- 当前已按 MySQL / SQL Server 分别生成备份命令，不在运行期自动执行真实备份或真实恢复。
- 最近一次备份治理结果会缓存在进程内，供健康检查直接读取最新备份文件、Runbook 与演练记录状态。
- 恢复 Runbook 与演练记录默认按配置目录落盘，生产恢复仍保持“仅生成指导文档，不自动执行危险恢复”的边界。

### 未完成但已预留
- PR-P 报表查询隔离与只读副本预留
- 后续可在当前底座上继续补充真实备份执行器、多副本校验与归档上传，但不得突破人工恢复门禁

### 下一 PR 入口
- 下一 PR 从 PR-P“报表查询隔离与只读副本预留”开始
- 后续不要重复实现数据库名解析、Provider 命令拼装、备份文件命名与恢复文档输出，应复用 `IBackupProvider`、`BackupVerificationService` 与 `RestoreDrillPlanner`
