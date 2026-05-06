# PR-长期数据库底座 K 检查台账：写入幂等、去重与重复键治理

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-K 切片，在 PR-J 查询治理完成后继续补齐写入幂等、去重与重复键治理基础能力。  
> **检查时间**：2026-05-06  
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
| PR-K 写入幂等、去重与重复键治理 | 本次已完成 | `Zeye.Sorting.Hub.Application/Services/Idempotency/IdempotencyGuardService.cs`、`Zeye.Sorting.Hub.Infrastructure/Repositories/IdempotencyRepository.cs` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Domain/Aggregates/Idempotency/IdempotencyRecord.cs`
- `Zeye.Sorting.Hub.Domain/Enums/Idempotency/IdempotencyRecordStatus.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/IIdempotencyRepository.cs`
- `Zeye.Sorting.Hub.Application/Services/Idempotency/IdempotencyGuardException.cs`
- `Zeye.Sorting.Hub.Application/Services/Idempotency/IdempotencyGuardService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Idempotency/IdempotencyKeyHasher.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/DuplicateKeyExceptionDetector.cs`
- `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/IdempotencyRecordEntityTypeConfiguration.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/IdempotencyRepository.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260506075656_AddIdempotencyRecordSupport.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260506075656_AddIdempotencyRecordSupport.Designer.cs`
- `Zeye.Sorting.Hub.Host.Tests/IdempotencyTests.cs`
- `检查台账/PR-长期数据库底座K-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Application/Services/Parcels/CreateParcelCommandService.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryErrorCodes.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/Routing/ParcelAdminApiRouteExtensions.cs`
- `Zeye.Sorting.Hub.Host.Tests/ParcelAdminApiTests.cs`
- `Zeye.Sorting.Hub.Host.Tests/ParcelBufferedWriteTests.cs`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/SortingHubDbContextModelSnapshot.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelRepository.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `IdempotencyRecord` 聚合、`IdempotencyRecordStatus` 枚举与 `IIdempotencyRepository` 仓储契约，统一定义来源系统、操作名、业务键、SHA256 载荷哈希、执行状态与失败消息。
2. 新增 `IdempotencyGuardService`、`IdempotencyGuardException`、`IdempotencyRepository` 与 `IdempotencyKeyHasher`，形成“读取现有记录 → 竞争创建 Pending → 执行业务 → 更新状态/回放恢复”的可复用幂等守卫链路。
3. 调整 `CreateParcelCommandService` 与 `ParcelAdminApiRouteExtensions`，在管理端新增包裹入口基于规范化本地时间载荷计算哈希；相同请求重复提交返回已有结果，处理中请求返回明确冲突，不再直接落入重复主键异常。
4. 根据审查意见补齐稳定错误码异常类型、取消请求可重试语义与 Pending 自恢复回放：取消会持久化为 `Rejected` 以允许后续重试；若真实结果已存在但幂等记录仍为 Pending，则重复请求会自动按重放语义恢复，避免长期卡死在 409。
5. 提取 `DuplicateKeyExceptionDetector` 统一复用 MySQL/SQL Server 唯一键冲突识别逻辑，避免 `ParcelRepository` 与 `IdempotencyRepository` 出现重复实现。
6. 新增 `IdempotencyRecordEntityTypeConfiguration` 与 EF 迁移 `20260506075656_AddIdempotencyRecordSupport.*`，补齐 `IdempotencyRecords` 表及唯一幂等键索引。
7. 新增 `IdempotencyTests.cs`，覆盖 SHA256 哈希稳定性、重复请求回放、取消后重试与 Pending 自恢复回放，并同步修正管理端/缓冲写入测试应用的依赖注册。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（267 通过）
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.IdempotencyTests' -v minimal` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.ParcelAdminApiTests' -v minimal` ✅
- `dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj --no-build --filter 'FullyQualifiedName~Zeye.Sorting.Hub.Host.Tests.ParcelBufferedWriteTests' -v minimal` ✅
- `bash .github/scripts/validate-database-foundation-rules.sh` ✅
- `bash .github/scripts/validate-copilot-rules.sh` ✅
- `dotnet ef migrations list --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Infrastructure --context SortingHubDbContext -- --provider MySql` ✅
- `dotnet ef migrations list --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Infrastructure --context SortingHubDbContext --no-connect -- --provider SqlServer` ✅

---

## 五、PR-K 断点摘要

### 已完成
- 幂等记录聚合
- 幂等仓储
- 幂等 Guard 服务
- 写入去重基础能力

### 保留能力
- 幂等键固定由 `SourceSystem + OperationName + BusinessKey + PayloadHash(SHA256)` 组成
- 当前已在管理端同步新增包裹入口落地重复请求回放、处理中拒绝与取消后重试
- 唯一键冲突识别逻辑已抽取为共享工具，后续仓储可继续复用

### 未完成但已预留
- PR-L Outbox 事件底座与业务事件持久化
- 后续可将同一幂等 Guard 接入更多写命令与后台写入链路

### 下一 PR 入口
- 下一 PR 从 PR-L“Outbox 事件底座与业务事件持久化”开始
- 后续不要重复实现载荷 SHA256 计算与幂等状态流转，应复用 `IdempotencyGuardService` 与 `IdempotencyKeyHasher`
