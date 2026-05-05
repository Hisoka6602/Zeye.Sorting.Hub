# PR-长期数据库底座 I 检查台账：慢查询指纹聚合与查询画像

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-I 切片，在 PR-H 基线数据校验完成后继续补齐慢查询指纹聚合、查询画像快照与诊断 API。  
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
| PR-I 慢查询指纹聚合与查询画像 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileStore.cs`、`Zeye.Sorting.Hub.Host/Routing/DiagnosticsApiRouteExtensions.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Application/Abstractions/Diagnostics/ISlowQueryProfileReader.cs`
- `Zeye.Sorting.Hub.Application/Abstractions/Diagnostics/SlowQueryProfileReadModel.cs`
- `Zeye.Sorting.Hub.Application/Services/Diagnostics/GetSlowQueryProfileQueryService.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Diagnostics/SlowQueryProfileListResponse.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Diagnostics/SlowQueryProfileResponse.cs`
- `Zeye.Sorting.Hub.Host/Routing/DiagnosticsApiRouteExtensions.cs`
- `Zeye.Sorting.Hub.Host.Tests/SlowQueryFingerprintTests.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryFingerprint.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryFingerprintAggregator.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileSnapshot.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileStore.cs`
- `检查台账/PR-长期数据库底座I-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAutoTuningPipeline.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryCommandInterceptor.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `SlowQueryFingerprintAggregator`，统一完成 SQL 去参数化、16 位指纹生成、P95/P99 计算与画像快照构建，并正确处理 SQL 单引号转义与样例 SQL 脱敏，避免自动调优与诊断链路重复实现。
2. 新增 `SlowQueryProfileStore`，按配置维护最近窗口的慢查询样本，提供 TopN 排序、窗口裁剪、最大指纹数量淘汰与单指纹样本上限控制能力。
3. 新增应用层诊断抽象 `ISlowQueryProfileReader` 与 `GetSlowQueryProfileQueryService`，保证 Application 通过只读抽象消费画像快照，不直接依赖 Infrastructure。
4. 新增 `DiagnosticsApiRouteExtensions`，暴露 `/api/diagnostics/slow-queries` 与 `/api/diagnostics/slow-queries/{fingerprint}` 两个只读端点，明确仅返回进程内快照，不触发数据库重查询。
5. 调整 `SlowQueryCommandInterceptor` 与 `SlowQueryAutoTuningPipeline`，复用现有慢查询采集链路同步写入画像快照并统一指纹计算逻辑。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（254 通过）
- `bash .github/scripts/validate-database-foundation-rules.sh` ✅
- `bash .github/scripts/validate-copilot-rules.sh` ✅

---

## 五、PR-I 断点摘要

### 已完成
- 慢查询指纹聚合
- 查询画像快照
- 诊断 API

### 保留能力
- 诊断接口仅读取进程内窗口快照，不访问数据库
- 画像快照按窗口裁剪并限制最大指纹数量，避免内存无限增长
- 现有自动调优闭环分析仍复用同一慢查询采集链路

### 未完成但已预留
- PR-J 查询模板治理与索引建议闭环
- 后续可把画像快照升级为可选轻量持久化缓存

### 下一 PR 入口
- 下一 PR 从 PR-J“查询模板治理与索引建议闭环”开始
- 后续不要重复实现 SQL 去参数化、指纹生成与分位点计算，应复用当前 `SlowQueryFingerprintAggregator`
