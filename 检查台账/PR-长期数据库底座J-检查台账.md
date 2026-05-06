# PR-长期数据库底座 J 检查台账：查询模板治理与索引建议闭环

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-J 切片，在 PR-I 慢查询画像完成后继续补齐查询模板登记、索引建议报告与未登记缺口暴露。  
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
| PR-H 种子数据、基线数据与配置一致性校验 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/`、`Zeye.Sorting.Hub.Host/HostedServices/BaselineDataValidationHostedService.cs` |
| PR-I 慢查询指纹聚合与查询画像 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileStore.cs`、`Zeye.Sorting.Hub.Host/Routing/DiagnosticsApiRouteExtensions.cs` |
| PR-J 查询模板治理与索引建议闭环 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/`、`Zeye.Sorting.Hub.Host/HostedServices/QueryGovernanceReportHostedService.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryTemplateDescriptor.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryTemplateRegistry.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryIndexRecommendation.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryIndexRecommendationService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryGovernanceReport.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/QueryGovernanceReportHostedService.cs`
- `Zeye.Sorting.Hub.Host.Tests/QueryGovernanceTests.cs`
- `检查台账/PR-长期数据库底座J-检查台账.md`

### 修改文件
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

1. 新增 `QueryTemplateRegistry`，集中登记当前阶段必须治理的 6 个高频查询模板，统一声明用途、涉及表、过滤/排序字段、索引建议、时间范围与深分页边界。
2. 新增 `QueryIndexRecommendationService` 与 `QueryGovernanceReport`，复用现有 `ISlowQueryProfileReader` 读取慢查询画像，对慢查询指纹做模板匹配、缺口识别与只读索引建议汇总。
3. 新增 `QueryGovernanceReportHostedService`，并在 `appsettings.json` 增补 `Persistence:AutoTuning:QueryGovernance` 配置节，在运行期周期输出查询模板登记、未命中模板、未登记慢查询指纹与索引建议报告，不自动执行任何 DDL。
4. 新增 `QueryGovernanceTests.cs`，覆盖强制模板登记、已登记模板建议输出与未登记慢查询缺口暴露，保证 PR-J 断点可继续往后推进。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（263 通过）
- `bash .github/scripts/validate-database-foundation-rules.sh` ✅
- `bash .github/scripts/validate-copilot-rules.sh` ✅

---

## 五、PR-J 断点摘要

### 已完成
- 查询模板登记
- 索引建议模型
- 查询治理报告

### 保留能力
- 索引建议只输出，不自动执行
- 查询治理报告仅复用进程内慢查询画像，不直接访问数据库
- 未登记模板覆盖的慢查询指纹会显式暴露，便于后续继续加严门禁

### 未完成但已预留
- PR-K 写入幂等、去重与重复键治理
- 后续可按 Provider 细化索引建议表达式与覆盖列策略

### 下一 PR 入口
- 下一 PR 从 PR-K“写入幂等、去重与重复键治理”开始
- 后续不要重复维护高频查询模板清单，应复用 `QueryTemplateRegistry`
