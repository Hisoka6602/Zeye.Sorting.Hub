# PR-长期数据库底座 H 检查台账：种子数据、基线数据与配置一致性校验

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-H 切片，在 PR-G 迁移治理完成后继续补齐基线数据校验、配置一致性校验与可选幂等种子入口。  
> **检查时间**：2026-05-05  
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
| PR-H 种子数据、基线数据与配置一致性校验 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/`、`Zeye.Sorting.Hub.Host/HostedServices/BaselineDataValidationHostedService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/BaselineDataHealthCheck.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataValidationResult.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataValidator.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataSeeder.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/BaselineDataValidationHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/BaselineDataHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/BaselineDataTests.cs`
- `检查台账/PR-长期数据库底座H-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `Zeye.Sorting.Hub.Host.Tests/AutoTuningProductionControlTests.cs`
- `README.md`
- `数据库底座门禁说明.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `BaselineDataValidator`，集中校验必要配置节点、Provider 与连接字符串匹配、关键枚举 `Description`、本地时间配置及默认参考数据目录。
2. 新增 `BaselineDataValidationHostedService`，启动期执行基线校验，并在 Degraded / FailFast 两种失败模式下输出中文日志与状态。
3. 新增 `BaselineDataHealthCheck` 并接入 `/health/ready`，把基线校验结果、失败模式、错误/告警数量与种子执行状态暴露给就绪探针。
4. 新增 `BaselineDataSeeder` 统一可选幂等种子入口；当前实现为 no-op，但已为后续持久化默认数据写入预留统一接入点。
5. 新增 `BaselineDataTests`，并修复 `AutoTuningProductionControlTests` 中无效连接字符串示例，恢复当前分支测试稳定通过。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（254 通过）
- `bash .github/scripts/validate-database-foundation-rules.sh` ✅
- `bash .github/scripts/validate-copilot-rules.sh` ✅

---

## 五、PR-H 断点摘要

### 已完成
- 基线数据校验
- 配置一致性校验
- 可选幂等种子数据入口

### 保留能力
- 默认只校验，不自动写入；需显式开启 `Persistence:BaselineData:IsSeedEnabled=true`
- 当前种子入口为幂等 no-op，实现后续真实默认数据写入前不会污染运行库
- 本地时间配置仍严格禁止 `Z` 与 offset

### 未完成但已预留
- PR-I 慢查询指纹聚合与查询画像
- 后续可将默认参考数据目录升级为真实持久化默认数据写入与审计回滚链路

### 下一 PR 入口
- 下一 PR 从 PR-I“慢查询指纹聚合与查询画像”开始
- 后续不要重复实现基础配置/时间语义/枚举 Description 基线校验，应复用当前 `BaselineDataValidator`
