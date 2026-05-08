# PR-长期数据库底座 Q 检查台账：租户 / 站点 / 设备维度数据边界预留

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-Q 切片，在 PR-P 报表查询隔离、只读副本预留与预算守卫完成后继续补齐站点 / 产线 / 设备 / 工作站维度的数据边界模型。  
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
| PR-P 报表查询隔离与只读副本预留 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/`、`Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs` |
| PR-Q 租户 / 站点 / 设备维度数据边界预留 | 本次已完成 | `Zeye.Sorting.Hub.Domain/ValueObjects/OperationalScope.cs`、`Zeye.Sorting.Hub.Application/Utilities/OperationalScopeNormalizer.cs`、`Zeye.Sorting.Hub.Host.Tests/OperationalScopeTests.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Domain/ValueObjects/SiteIdentity.cs`
- `Zeye.Sorting.Hub.Domain/ValueObjects/LineIdentity.cs`
- `Zeye.Sorting.Hub.Domain/ValueObjects/DeviceIdentity.cs`
- `Zeye.Sorting.Hub.Domain/ValueObjects/OperationalScope.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Common/OperationalScopeRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Common/OperationalScopeResponse.cs`
- `Zeye.Sorting.Hub.Application/Utilities/OperationalScopeNormalizer.cs`
- `Zeye.Sorting.Hub.Host.Tests/OperationalScopeTests.cs`
- `检查台账/PR-长期数据库底座Q-检查台账.md`

### 修改文件
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `SiteIdentity`、`LineIdentity`、`DeviceIdentity` 与 `OperationalScope`，统一建立站点、产线、设备与工作站四维运营边界值对象，避免后续业务模块继续写死单站点结构。
2. 新增 `OperationalScopeRequest` 与 `OperationalScopeResponse`，统一沉淀通用合同模型，明确后续业务模块接入时必须复用的边界字段命名。
3. 新增 `OperationalScopeNormalizer`，集中处理边界字符串标准化、必填校验、长度约束、异常日志输出与响应映射，消除后续模块的影分身校验代码。
4. 新增 `OperationalScopeTests.cs`，覆盖维度标准化、必填校验、可选维度归一化与合同映射，并同步修正 README、更新记录、文件清单基线与 PR-Q 台账。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter "FullyQualifiedName~OperationalScopeTests" -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `./.github/scripts/validate-database-foundation-rules.sh` ✅

---

## 五、PR-Q 断点摘要

### 已完成
- OperationalScope 值对象
- 站点/产线/设备维度建模规范
- 后续业务接入边界

### 保留能力
- 当前已统一提供 `SiteCode`、`LineCode`、`DeviceCode`、`WorkstationName` 四维运营边界模型，后续业务模块可直接复用。
- `OperationalScopeNormalizer` 已集中处理标准化、必填校验、长度约束与响应映射，避免继续分散实现同义逻辑。

### 未完成但已预留
- PR-R 业务模块接入模板与代码生成规范
- 后续可在现有边界模型上逐步接入真实业务上下文，但当前仍保持“不做租户鉴权、不改动现有表结构”的边界

### 下一 PR 入口
- 下一 PR 从 PR-R“业务模块接入模板与代码生成规范”开始
- 后续不要重复实现站点/产线/设备/工作站维度的字符串标准化与合同映射，应复用 `OperationalScopeNormalizer`、`OperationalScopeRequest` 与 `OperationalScopeResponse`
