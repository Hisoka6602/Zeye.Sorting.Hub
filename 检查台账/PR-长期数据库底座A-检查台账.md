# PR-长期数据库底座 A 检查台账：数据库连接诊断与就绪状态增强

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-A 切片，先核对仓库现状，再补齐数据库连接诊断、连接预热与详细就绪探针能力。  
> **检查时间**：2026-05-05  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 本次已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs`、`Zeye.Sorting.Hub.Host/HostedServices/DatabaseConnectionWarmupHostedService.cs` |
| PR-B 查询保护与游标分页 | 未开始 | 仓库当前仍仅有 `PageRequest/PageResult` 偏移分页模型，无 `Cursor` 相关实现 |
| PR-C 批量写入缓冲与死信隔离 | 未开始 | 仓库当前无 `WriteBuffer` / `DeadLetter` 相关实现 |
| PR-D 分表巡检、预建与索引检查 | 部分完成 | `DatabaseInitializerHostedService.cs`、`ParcelShardingStrategyEvaluator.cs` |
| PR-F 数据库底座 CI 门禁增强 | 部分完成 | `.github/workflows/stability-gates.yml` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionDiagnosticsOptions.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionHealthSnapshot.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/IDatabaseConnectionDiagnostics.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionDiagnosticsService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionWarmupService.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs`
- `Zeye.Sorting.Hub.Host/HostedServices/DatabaseConnectionWarmupHostedService.cs`
- `Zeye.Sorting.Hub.Host.Tests/DatabaseConnectionDiagnosticsTests.cs`

### 修改文件
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`
- `Zeye.Sorting.Hub.Host/HealthChecks/HealthCheckResponseWriter.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings.json`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增数据库连接诊断配置、快照与诊断服务，统一用 `IDbContextFactory<SortingHubDbContext>` 执行短生命周期探测，并缓存最近一次快照。
2. 新增数据库连接预热服务与托管服务，启动阶段按配置执行非阻塞预热，所有异常均记录 NLog。
3. `/health/ready` 改为使用详细数据库健康检查，返回 provider、database、checkedAtLocal、elapsedMilliseconds、连续失败/成功次数等结构化数据。
4. `HealthCheckResponseWriter` 新增 Data 输出，保证诊断快照可直接从健康检查 JSON 读取。
5. 新增 7 个数据库连接诊断测试，覆盖默认配置、非法配置、失败快照、失败阈值、成功快照本地时间语义与健康检查数据输出。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（213 通过）
- `rg "DateTime\.UtcNow|DateTimeOffset\.UtcNow|ToUniversalTime\(|DateTimeKind\.Utc" Zeye.Sorting.Hub.Host Zeye.Sorting.Hub.Infrastructure Zeye.Sorting.Hub.Host.Tests -g "*.cs"` ✅ 无命中
- `rg "Z$|\+08:00|-0500" Zeye.Sorting.Hub.Host -g "appsettings*.json"` ✅ 无命中

---

## 五、PR-A 断点摘要

### 已完成
- 数据库连接诊断配置与快照模型
- 数据库连接探测服务与启动预热服务
- 详细数据库健康检查与健康检查 Data 输出
- 对应单元测试与配置接线

### 保留能力
- 原有 `DatabaseReadinessHealthCheck.cs` 文件仍保留，未直接删除
- `/health/live`、`/health` 兼容端点保持不变
- 现有自动迁移、分表治理、自动调谐链路未被侵入式改动

### 未完成但已预留
- PR-B 游标分页与查询保护
- PR-C 批量写入缓冲与死信隔离
- PR-A 后续若需要可补充独立 `/health/details` 兼容端点，但当前 `/health/ready` 已具备详细数据输出

### 下一 PR 入口
- 下一 PR 从 PR-B“查询保护与游标分页”开始
- 禁止重复实现数据库连接诊断、预热 HostedService 与详细健康检查
- 后续查询保护优先复用现有 `HealthCheckResponseWriter`、`LocalDateTimeParsing` 与 `PageRequest/PageResult` 目录结构
