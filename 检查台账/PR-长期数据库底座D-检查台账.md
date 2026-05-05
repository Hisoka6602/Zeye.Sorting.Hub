# PR-长期数据库底座 D 检查台账：分表巡检、预建与索引检查

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-D 切片，聚焦分表物理表巡检、预建 dry-run 计划、关键索引检查与分表治理健康检查。  
> **检查时间**：2026-05-05  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs` |
| PR-B 查询保护与游标分页 | 已完成 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`、`Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs` |
| PR-C 批量写入缓冲与死信隔离 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/`、`Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs` |
| PR-D 分表巡检、预建与索引检查 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTableInspectionService.cs`、`Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTablePrebuildService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/ShardingGovernanceHealthCheck.cs` |
| PR-E 数据归档与冷热分层 | 未开始 | 当前仓库无 `DataGovernance` / `Archiving` 实现 |
| PR-F 数据库底座 CI 门禁增强 | 部分完成 | `.github/workflows/stability-gates.yml` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingRuntimeInspectionOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingPrebuildOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingInspectionReport.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingPrebuildPlan.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingPhysicalTablePlanBuilder.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingCapacitySnapshotService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingIndexInspectionService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTableInspectionService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTablePrebuildService.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/ShardingInspectionHostedService.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/ShardingPrebuildHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/ShardingGovernanceHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/ConfigurableShardingPhysicalTableProbe.cs`
- `Zeye.Sorting.Hub.Host.Tests/ShardingInspectionTests.cs`
- `检查台账/PR-长期数据库底座D-检查台账.md`

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

1. 新增分表运行期巡检服务，覆盖当前周期、下一周期、WebRequestAuditLog 热表与详情表成对检查、容量/热点风险摘要与关键索引缺失检查。
2. 新增分表预建 dry-run 计划服务，只输出未来窗口内应预建与当前缺失物理表，不执行真实 DDL。
3. 新增 `RuntimeInspection` 与 `Prebuild` 配置节，约束巡检间隔、索引/容量检查开关、预建窗口与 dry-run。
4. 新增 `ShardingGovernanceHealthCheck`，将分表治理状态并入 `/health/ready`。
5. 新增 `ShardingInspectionTests.cs`，覆盖物理表规划、预建计划、索引缺失、巡检不健康与健康检查不健康场景。
6. 保持真实预建默认不可执行，当前版本强制 `Persistence:Sharding:Prebuild:DryRun=true`，后续真实 DDL 必须先接入危险动作隔离器。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `rg "DateTime\.UtcNow|DateTimeOffset\.UtcNow|ToUniversalTime\(|DateTimeKind\.Utc" Zeye.Sorting.Hub.Host Zeye.Sorting.Hub.Infrastructure Zeye.Sorting.Hub.Application Zeye.Sorting.Hub.Contracts Zeye.Sorting.Hub.Domain Zeye.Sorting.Hub.Host.Tests -g "*.cs"` ✅ 无命中
- `rg "Z$|\+08:00|-0500" Zeye.Sorting.Hub.Host -g "appsettings*.json"` ✅ 无命中

---

## 五、PR-D 断点摘要

### 已完成
- 分表物理表巡检
- 分表预建计划
- 分表索引检查
- 分表治理健康检查

### 保留能力
- 既有 DatabaseInitializerHostedService 分表守卫与自动迁移链路保持兼容
- 既有 Provider 差异化探测接口继续复用，不重复实现 MySQL / SQL Server 元数据查询
- 真实预建 DDL 尚未开放，仍以 dry-run 计划优先

### 未完成但已预留
- PR-E 数据归档与冷热分层
- 后续真实预建执行需接入危险动作隔离器、审计与回滚说明后再开放

### 下一 PR 入口
- 下一 PR 从 PR-E“数据归档与冷热分层”开始
- 不要重复实现分表探测
- 继续复用 `ShardingTableInspectionService`、`ShardingTablePrebuildService` 与 `ShardingGovernanceHealthCheck`
