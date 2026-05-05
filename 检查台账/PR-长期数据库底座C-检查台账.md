# PR-长期数据库底座 C 检查台账：批量写入缓冲与死信隔离

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-C 切片，聚焦 Parcel 批量写入缓冲、后台 Flush、死信隔离与队列健康检查。  
> **检查时间**：2026-05-05  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs` |
| PR-B 查询保护与游标分页 | 已完成 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`、`Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs` |
| PR-C 批量写入缓冲与死信隔离 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/`、`Zeye.Sorting.Hub.Host/Routing/ParcelAdminApiRouteExtensions.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs` |
| PR-D 分表巡检、预建与索引检查 | 未开始本轮实施 | `ParcelShardingStrategyEvaluator.cs`、`DatabaseInitializerHostedService.cs` |
| PR-F 数据库底座 CI 门禁增强 | 部分完成 | `.github/workflows/stability-gates.yml` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Application/Mappers/Parcels/ParcelCreateRequestMapper.cs`
- `Zeye.Sorting.Hub.Application/Services/WriteBuffers/BufferedWriteOptions.cs`
- `Zeye.Sorting.Hub.Application/Services/WriteBuffers/BufferedWriteResult.cs`
- `Zeye.Sorting.Hub.Application/Services/WriteBuffers/IBufferedWriteService.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelBatchBufferedCreateRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelBatchBufferedCreateResponse.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/ParcelBatchWriteFlushHostedService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/BatchWriteMetricsSnapshot.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/BoundedWriteChannel.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/BufferedParcelWriteItem.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/DeadLetterWriteEntry.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/DeadLetterWriteStore.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/ParcelBatchWriteFlushService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/ParcelBufferedWriteService.cs`
- `Zeye.Sorting.Hub.Host.Tests/ParcelBufferedWriteTests.cs`
- `检查台账/PR-长期数据库底座C-检查台账.md`

### 修改文件
- `Zeye.Sorting.Hub.Application/Services/Parcels/CreateParcelCommandService.cs`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Zeye.Sorting.Hub.Infrastructure.csproj`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/Routing/ParcelAdminApiRouteExtensions.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `Zeye.Sorting.Hub.Host.Tests/FakeParcelRepository.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增 `POST /api/admin/parcels/batch-buffer`，返回 `acceptedCount`、`rejectedCount`、`queueDepth`、`isBackpressureTriggered`、`message`。
2. 新增 `Persistence:WriteBuffering` 配置节，支持开关、通道容量、批次大小、刷新间隔、最大重试次数、背压阈值与死信容量。
3. 落地 `BoundedWriteChannel<T>`、`ParcelBufferedWriteService`、`ParcelBatchWriteFlushService`、`DeadLetterWriteStore` 与 `BatchWriteMetricsSnapshot`，满足有界缓冲、后台批量落库、失败重试与死信隔离。
4. 新增 `BufferedWriteQueueHealthCheck`，将缓冲写入队列状态并入 `/health/ready`。
5. 新增 `ParcelCreateRequestMapper`，统一同步新增与批量缓冲写入的请求到聚合映射，避免影分身代码。
6. 保留 `POST /api/admin/parcels` 同步强一致新增接口，不将既有链路改造成最终一致。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（227 通过）
- `rg "DateTime\.UtcNow|DateTimeOffset\.UtcNow|ToUniversalTime\(|DateTimeKind\.Utc" Zeye.Sorting.Hub.Host Zeye.Sorting.Hub.Infrastructure Zeye.Sorting.Hub.Application Zeye.Sorting.Hub.Contracts Zeye.Sorting.Hub.Domain Zeye.Sorting.Hub.Host.Tests -g "*.cs"` ✅ 无命中
- `rg "Z$|\+08:00|-0500" Zeye.Sorting.Hub.Host -g "appsettings*.json"` ✅ 无命中

---

## 五、PR-C 断点摘要

### 已完成
- Parcel 批量缓冲写入
- 有界 Channel
- Flush 后台服务
- 死信隔离
- 写入队列健康检查

### 保留能力
- `POST /api/admin/parcels` 仍为同步强一致新增接口
- 既有 Parcel 只读查询、数据库连接诊断、自动迁移与分表治理链路保持兼容

### 未完成但已预留
- PR-D 分表巡检、预建与索引检查
- 后续如需补充死信查询接口，可基于 `DeadLetterWriteStore` 与 `BatchWriteMetricsSnapshot` 继续扩展

### 下一 PR 入口
- 下一 PR 从 PR-D“分表巡检、预建与索引检查”开始
- 不要把同步新增接口改成缓冲写入
- 继续复用 `ParcelCreateRequestMapper`、`BoundedWriteChannel<T>` 与 `BufferedWriteQueueHealthCheck` 等基础能力
