# PR-长期数据库底座 B 检查台账：查询保护与游标分页

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-B 切片，聚焦 Parcel 查询保护、游标分页与下一阶段断点收口。  
> **检查时间**：2026-05-05  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs` |
| PR-B 查询保护与游标分页 | 本次已完成 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`、`Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs`、`Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelCursorQueryExtensions.cs` |
| PR-C 批量写入缓冲与死信隔离 | 未开始 | 仓库当前无 `WriteBuffer`、`DeadLetter` 相关实现 |
| PR-D 分表巡检、预建与索引检查 | 部分完成 | `DatabaseInitializerHostedService.cs`、`ParcelShardingStrategyEvaluator.cs` |
| PR-F 数据库底座 CI 门禁增强 | 部分完成 | `.github/workflows/stability-gates.yml` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/CursorPageRequest.cs`
- `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/CursorPageResult.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelCursorToken.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelCursorListRequest.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelCursorListResponse.cs`
- `Zeye.Sorting.Hub.Application/Services/Parcels/ParcelQueryRequestMapper.cs`
- `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelCursorQueryExtensions.cs`
- `Zeye.Sorting.Hub.Host/QueryParameters/ParcelCursorListQueryParameters.cs`
- `Zeye.Sorting.Hub.Host.Tests/ParcelCursorQueryTests.cs`

### 修改文件
- `Zeye.Sorting.Hub.Domain/Repositories/IParcelRepository.cs`
- `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelPagedQueryService.cs`
- `Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelRepository.cs`
- `Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs`
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host.Tests/FakeParcelRepository.cs`
- `Zeye.Sorting.Hub.Host.Tests/ParcelReadOnlyApiTests.cs`
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 新增游标分页请求/结果模型、游标令牌合同与 `/api/parcels/cursor` 只读接口，固定使用 `ScannedTime DESC, Id DESC` 稳定排序。
2. 仓储新增 `GetCursorPagedAsync`，复用现有过滤、验证与读模型投影，只在分页方式上切换为游标模式。
3. 普通分页查询新增查询保护：未指定时间范围时默认最近 24 小时，页码超过 10000 直接拒绝。
4. 新增 `ParcelQueryRequestMapper`，统一普通分页与游标分页的过滤映射和默认时间窗口规则，避免影分身代码。
5. 新增 7 个游标分页/查询保护测试，覆盖首页、翻页、非法游标、页大小归一化、默认时间范围、普通分页页码保护与仓储稳定排序。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅（220 通过）
- `rg "DateTime\.UtcNow|DateTimeOffset\.UtcNow|ToUniversalTime\(|DateTimeKind\.Utc" Zeye.Sorting.Hub.Host Zeye.Sorting.Hub.Infrastructure Zeye.Sorting.Hub.Application Zeye.Sorting.Hub.Contracts Zeye.Sorting.Hub.Domain Zeye.Sorting.Hub.Host.Tests -g "*.cs"` ✅ 无命中
- `rg "Z$|\+08:00|-0500" Zeye.Sorting.Hub.Host -g "appsettings*.json"` ✅ 无命中

---

## 五、PR-B 断点摘要

### 已完成
- 游标分页领域模型、合同模型与游标令牌
- Parcel 仓储游标分页实现与游标查询扩展
- Application 游标查询服务与 Host `/api/parcels/cursor` 路由
- 普通分页默认最近 24 小时与最大页码保护
- 对应测试、台账与 README 同步

### 保留能力
- 原有 `/api/parcels`、`/api/parcels/{id}`、`/api/parcels/adjacent` 兼容接口保持不变
- 原有偏移分页响应结构保持兼容，未改动 `ParcelListResponse`
- 现有数据库连接诊断、自动迁移、分表治理与自动调谐链路未被侵入式改动

### 未完成但已预留
- PR-C 批量写入缓冲与死信隔离
- PR-B 如需继续下探，可在后续切片考虑 `includeTotalCount=false` 的额外统计优化，但当前不影响游标分页落地

### 下一 PR 入口
- 下一 PR 从 PR-C“批量写入缓冲与死信隔离”开始
- 禁止重复实现游标令牌编码、默认最近 24 小时窗口与稳定排序条件
- 后续写入治理优先复用现有 `ParcelQueryRequestMapper`、`PageRequest`、`CursorPageRequest` 与 `HealthCheckResponseWriter` 等基础能力
