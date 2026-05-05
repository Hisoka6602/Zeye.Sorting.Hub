# PR-长期数据库底座 E 检查台账：数据归档与冷热分层

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-E 切片，先核对长期数据库底座当前进度，再补齐归档任务 dry-run、后台执行、查询/重试 API 与下一阶段断点。  
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
| PR-E 数据归档与冷热分层 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/`、`Zeye.Sorting.Hub.Host/Routing/DataGovernanceApiRouteExtensions.cs`、`Zeye.Sorting.Hub.Domain/Aggregates/DataGovernance/ArchiveTask.cs` |
| PR-F 数据库底座 CI 门禁增强 | 部分完成 | `.github/workflows/stability-gates.yml`、`.github/workflows/copilot-instructions-validation.yml` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Domain/Aggregates/DataGovernance/ArchiveTask.cs`
- `Zeye.Sorting.Hub.Domain/Enums/DataGovernance/ArchiveTaskStatus.cs`
- `Zeye.Sorting.Hub.Domain/Enums/DataGovernance/ArchiveTaskType.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/IArchiveTaskRepository.cs`
- `Zeye.Sorting.Hub.Application/Services/DataGovernance/ArchiveTaskContractMapper.cs`
- `Zeye.Sorting.Hub.Application/Services/DataGovernance/CreateArchiveTaskCommandService.cs`
- `Zeye.Sorting.Hub.Application/Services/DataGovernance/GetArchiveTaskPagedQueryService.cs`
- `Zeye.Sorting.Hub.Application/Services/DataGovernance/RetryArchiveTaskCommandService.cs`
- `Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskCreateRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskListRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskListResponse.cs`
- `Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskResponse.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchivePlanner.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveCheckpointStore.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveExecutor.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveHostedWorker.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/ArchiveTaskRepository.cs`
- `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/ArchiveTaskEntityTypeConfiguration.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/AddArchiveTaskDryRunSupport.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/AddArchiveTaskDryRunSupportDesigner.cs`
- `Zeye.Sorting.Hub.Host/Routing/DataGovernanceApiRouteExtensions.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/DataArchiveHostedService.cs`
- `Zeye.Sorting.Hub.Host.Tests/DataArchiveTaskTests.cs`
- `检查台账/PR-长期数据库底座E-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/SortingHubDbContextModelSnapshot.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增归档任务聚合、状态/类型枚举与仓储契约，统一记录 dry-run 状态、计划摘要、检查点 JSON、重试次数与失败信息。
2. 新增 `DataArchivePlanner`、`DataArchiveExecutor`、`DataArchiveCheckpointStore`、`DataArchiveHostedWorker` 与 `DataArchiveHostedService`，实现待执行任务轮询、状态流转与 `WebRequestAuditLogHistory` 历史数据 dry-run 计划生成。
3. 新增 `POST /api/data-governance/archive-tasks`、`GET /api/data-governance/archive-tasks`、`POST /api/data-governance/archive-tasks/{id:long}/retry` 三个接口，补齐任务创建、分页查询与终态重试入口。
4. 新增 `Persistence:Archiving` 配置节，约束 Worker 轮询间隔与 dry-run 样本条数，保持全部本地时间语义。
5. 新增 `ArchiveTasks` EF 迁移与 4 个归档任务测试，覆盖创建、分页、后台执行完成、终态重试与非法类型校验。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（238 通过）
- `dotnet ef migrations add AddArchiveTaskDryRunSupport --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Infrastructure --context SortingHubDbContext -- --provider MySql` ✅
- `rg "DateTime\.UtcNow|DateTimeOffset\.UtcNow|ToUniversalTime\(|DateTimeKind\.Utc|UtcDateTime|AssumeUniversal|AdjustToUniversal" Zeye.Sorting.Hub.Host Zeye.Sorting.Hub.Infrastructure Zeye.Sorting.Hub.Application Zeye.Sorting.Hub.Contracts Zeye.Sorting.Hub.Domain Zeye.Sorting.Hub.Host.Tests -g "*.cs"` ✅ 无命中
- `rg "Z$|\+08:00|-0500" Zeye.Sorting.Hub.Host -g "appsettings*.json"` ✅ 无命中

---

## 五、PR-E 断点摘要

### 已完成
- 归档任务聚合与仓储
- 归档 dry-run 计划器与后台执行骨架
- 归档任务创建 / 分页 / 重试 API
- 归档任务迁移、测试、README 与台账同步

### 保留能力
- 当前仅执行 dry-run 计划与审计记录，不执行真实删除、迁移或压缩导出
- 既有连接诊断、查询保护、批量缓冲写入与分表治理链路保持兼容
- 所有新增时间字段继续保持本地时间语义

### 未完成但已预留
- PR-F 数据库底座 CI 门禁增强
- PR-E 后续真实冷库迁移执行器、危险动作隔离器、审计与回滚资产

### 下一 PR 入口
- 下一 PR 从 PR-F“数据库底座 CI 门禁增强”开始
- 不要绕过当前归档 dry-run 状态流转与检查点模型
- 后续真实冷热分层必须复用 `ArchiveTask`、`DataArchivePlanner`、`DataArchiveCheckpointStore` 与 `DataArchiveHostedWorker`
