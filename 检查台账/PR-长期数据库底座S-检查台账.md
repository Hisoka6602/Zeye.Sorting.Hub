# PR-长期数据库底座 S 检查台账：压测工程与性能基线报告

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-S 切片，在 PR-R 业务模块接入模板完成后继续补齐压测工程、性能基线报告模板与轻量 smoke workflow。  
> **检查时间**：2026-05-08  
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
| PR-P 报表查询隔离与只读副本预留 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/`、`Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs` |
| PR-Q 租户 / 站点 / 设备维度数据边界预留 | 已完成 | `Zeye.Sorting.Hub.Domain/ValueObjects/OperationalScope.cs`、`Zeye.Sorting.Hub.Application/Utilities/OperationalScopeNormalizer.cs`、`Zeye.Sorting.Hub.Host.Tests/OperationalScopeTests.cs` |
| PR-R 业务模块接入模板与代码生成规范 | 已完成 | `业务模块接入规范.md`、`Copilot-业务模块新增模板.md`、`Zeye.Sorting.Hub.Application/Utilities/ApplicationResult.cs`、`Zeye.Sorting.Hub.Host/Routing/EndpointRouteBuilderConventionExtensions.cs` |
| PR-S 压测工程与性能基线报告 | 本次已完成 | `performance/README.md`、`performance/k6/`、`性能基线报告.md`、`.github/workflows/performance-smoke-test.yml`、`Zeye.Sorting.Hub.Host.Tests/PerformanceBaselineRulesTests.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `.github/workflows/performance-smoke-test.yml`
- `performance/README.md`
- `performance/k6/common.js`
- `performance/k6/parcel-cursor-query.js`
- `performance/k6/parcel-batch-buffer-write.js`
- `performance/k6/audit-query.js`
- `performance/results/.gitkeep`
- `性能基线报告.md`
- `Zeye.Sorting.Hub.Host.Tests/PerformanceBaselineRulesTests.cs`
- `检查台账/PR-长期数据库底座S-检查台账.md`

### 修改文件
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `performance/README.md`，统一记录压测工程目录结构、覆盖范围映射、环境变量、手动执行命令、指标采集要求与结果沉淀约定。
2. 新增 `performance/k6/common.js` 与 3 个 k6 脚本，分别覆盖 Parcel 游标/普通分页、批量缓冲写入、审计查询/健康检查/慢查询画像链路，并统一使用无 `Z`、无 offset 的本地时间字符串。
3. 新增 `性能基线报告.md`，沉淀 PR-S 强制指标、场景摘要、执行命令、环境快照与结果分析模板。
4. 新增 `.github/workflows/performance-smoke-test.yml`，只执行 `PerformanceBaselineRulesTests` 的轻量校验，不在 CI 中触发真实压测。
5. 新增 `PerformanceBaselineRulesTests.cs`，覆盖压测文档、脚本、workflow 与基线报告模板关键约束，并同步更新 README、更新记录、文件清单基线与 PR-S 台账。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj -v quiet --filter "FullyQualifiedName~PerformanceBaselineRulesTests"` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `./.github/scripts/validate-database-foundation-rules.sh` ✅

---

## 五、PR-S 断点摘要

### 已完成
- 压测脚本
- 性能基线报告模板
- 轻量性能 smoke test

### 保留能力
- 当前已提供 `performance/k6/common.js`、3 个压测脚本与 `性能基线报告.md`，后续业务模块可沿用同样模式补充模块级性能基线。
- `performance-smoke-test.yml` 与 `PerformanceBaselineRulesTests.cs` 已形成轻量门禁，避免压测资产遗漏或关键约束回退。

### 未完成但已预留
- PR-T 生产运行 Runbook、应急预案与最终底座验收
- 完整压测结果仍需在受控环境中手动执行后回填

### 下一 PR 入口
- 下一 PR 从 PR-T“生产运行 Runbook、应急预案与最终底座验收”开始
- 后续新增压测脚本时，不应重复实现本地时间格式化与写入载荷构造，应复用 `performance/k6/common.js`
