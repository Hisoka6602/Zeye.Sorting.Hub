# PR-长期数据库底座 P 检查台账：报表查询隔离与只读副本预留

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-P 切片，在 PR-O 备份、恢复、校验与演练底座完成后继续补齐报表查询隔离、只读副本预留与预算守卫基础能力。  
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
| PR-O 备份、恢复、校验与演练底座 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/`、`Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs` |
| PR-P 报表查询隔离与只读副本预留 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/`、`Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReadOnlyDatabaseOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReadOnlyDbContextFactorySelector.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReadOnlyRouteProbeResult.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReportingQueryGuard.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReportingQueryBudget.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/ReportingQueryIsolationTests.cs`
- `检查台账/PR-长期数据库底座P-检查台账.md`

### 修改文件
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

1. 新增 `ReadOnlyDatabaseOptions`、`ReadOnlyDbContextFactorySelector`、`ReadOnlyRouteProbeResult`、`ReportingQueryGuard` 与 `ReportingQueryBudget`，统一建立只读副本开关、主库回退策略、路由探测结果、报表时间范围预算、最大返回行数限制与默认关闭总数统计的基础能力。
2. 调整 `PersistenceServiceCollectionExtensions.cs`，新增 `RegisterReadOnlyDatabaseServices` 与 Provider 级 DbContext 选项复用入口，避免只读副本与主库链路重复拼装 EF Core 选项。
3. 新增 `ReadOnlyDatabaseHealthCheck` 并调整 `Program.cs`，将只读副本可用性、主库回退状态与当前报表查询路由目标接入 `/health/ready`。
4. 调整 `appsettings.json`，补齐 `Persistence:ReadOnlyDatabase` 默认配置与 `ConnectionStrings:MySqlReadOnly` / `ConnectionStrings:SqlServerReadOnly` 预留键，保持后续接入只读副本时无需再修改结构。
5. 新增 `ReportingQueryIsolationTests.cs`，覆盖预算超限、行数裁剪、只读副本缺失时的主库回退与直接拒绝路径，并同步修正 README、更新记录、文件清单基线与 PR-P 台账。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --filter "FullyQualifiedName~ReportingQueryIsolationTests" -v minimal` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `./.github/scripts/validate-database-foundation-rules.sh` ✅

---

## 五、PR-P 断点摘要

### 已完成
- 只读数据库配置底座
- 报表查询预算
- 报表查询隔离守卫

### 保留能力
- 当前已支持按 MySQL / SQL Server 配置独立只读副本连接字符串，并在只读副本缺失或不可用时按配置执行主库回退或直接拒绝。
- 报表查询预算已统一约束本地时间范围、最大返回行数，并默认关闭总数统计。
- `/health/ready` 已补充只读副本状态探针，可直接暴露当前路由目标与退化状态。

### 未完成但已预留
- PR-Q 租户 / 站点 / 设备维度数据边界预留
- 后续可在当前底座上接入真实报表查询服务，但必须复用 `ReportingQueryGuard` 与 `ReadOnlyDbContextFactorySelector`

### 下一 PR 入口
- 下一 PR 从 PR-Q“租户 / 站点 / 设备维度数据边界预留”开始
- 后续不要重复实现报表查询时间范围校验、只读副本回退与总数统计关闭逻辑，应复用 `ReportingQueryGuard` 与 `ReadOnlyDbContextFactorySelector`
