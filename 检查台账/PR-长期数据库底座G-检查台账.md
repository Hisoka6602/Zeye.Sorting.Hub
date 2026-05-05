# PR-长期数据库底座 G 检查台账：数据库迁移治理与回滚资产

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-G 切片，先核对长期数据库底座当前完成度，再补齐迁移治理预演、脚本归档、危险迁移识别与健康检查。  
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
| PR-G 数据库迁移治理与回滚资产 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/`、`Zeye.Sorting.Hub.Host/HostedServices/MigrationGovernanceHostedService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/MigrationGovernanceHealthCheck.cs` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationPlan.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationExecutionRecord.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationGovernanceStateStore.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationScriptArchiveService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationRollbackScriptProvider.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationSafetyEvaluator.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/MigrationGovernanceHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/MigrationGovernanceHealthCheck.cs`
- `Zeye.Sorting.Hub.Host.Tests/MigrationGovernanceTests.cs`
- `检查台账/PR-长期数据库底座G-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/DatabaseInitializerHostedService.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增迁移治理目录，补齐迁移计划、执行记录、状态存储、危险 SQL 识别、正向脚本归档与人工回滚参考脚本生成能力。
2. 新增 `MigrationGovernanceHostedService`，启动期读取当前 EF 迁移列表、计算待执行迁移、生成正向脚本、识别危险 SQL，并将归档路径与执行决策写入运行期状态。
3. 新增 `MigrationGovernanceHealthCheck` 并接入 `/health/ready`，当存在待执行迁移但处于 dry-run / 生产危险迁移阻断状态时返回 Degraded；预演失败时返回 Unhealthy。
4. 调整 `DatabaseInitializerHostedService` 复用迁移治理预演结果，避免重复迁移检测；在 dry-run 或危险迁移阻断时跳过真实 `MigrateAsync`，并在成功/失败后回写结构化执行记录。
5. 新增 6 个迁移治理测试，覆盖健康检查、危险 SQL 识别、dry-run 决策、脚本归档路径与预演异常记录。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（247 通过）
- `bash .github/scripts/validate-database-foundation-rules.sh` ✅
- `bash .github/scripts/validate-copilot-rules.sh` ✅

---

## 五、PR-G 断点摘要

### 已完成
- 迁移计划生成
- 迁移脚本归档
- 危险迁移识别
- 迁移治理健康检查

### 保留能力
- 当前回滚脚本仅作为人工审核与归档参考资产，不自动执行不可逆回滚
- 迁移治理结果已接入 `DatabaseInitializerHostedService`，避免重复实现迁移检测
- 所有新增时间字段与时间格式继续保持本地时间语义

### 未完成但已预留
- PR-H 种子数据、基线数据与配置一致性校验
- 后续可将迁移脚本归档继续接入 CI artifact / DBA 审批链路

### 下一 PR 入口
- 下一 PR 从 PR-H“种子数据、基线数据与配置一致性校验”开始
- 后续不要重复实现迁移检测，应复用当前迁移治理状态存储、脚本归档与危险 SQL 识别能力
