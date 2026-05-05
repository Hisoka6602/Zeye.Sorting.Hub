# Zeye.Sorting.Hub 长期数据库底座多 PR 实施方案与 Copilot 严格门禁

> 适用仓库：`Hisoka6602/Zeye.Sorting.Hub`  
> 目标阶段：业务开发前的长期底座建设。  
> 项目定位：无人值守、高并发查询、高并发写入、长期存储、可观测、可治理、可恢复的数据底座 / 分拣数据中枢。  
> 当前明确不做：鉴权接入、硬件控制、PLC/IO/摆轮控制、实时分拣执行状态机。  
> 当前核心目标：在正式业务逻辑大量进入前，把数据库、查询、写入、分表、归档、观测、恢复、门禁、扩展边界全部打稳。

---

## 0. 项目长期定位

`Zeye.Sorting.Hub` 的长期定位不是硬件控制服务，也不是现场实时执行服务，而是：

```text
分拣数据中枢
高并发存储底座
高并发查询底座
数据审计底座
数据库治理底座
长期无人值守运行底座
后续业务模块的统一数据承载层
```

建议长期架构边界：

```text
外部系统 / 后续业务模块
        ↓
Host API / Background Workers
        ↓
Application 用例编排
        ↓
Domain 领域模型与规则
        ↓
Infrastructure 数据库、分表、归档、审计、观测、恢复
```

当前底座必须先满足：

1. 高并发查询不会拖垮数据库。
2. 高并发写入不会阻塞请求线程。
3. 长期运行不会因为日志、审计、历史数据无限膨胀而崩溃。
4. 分表策略可验证、可预建、可巡检。
5. 慢查询可捕获、可聚合、可定位。
6. 数据库连接异常可感知、可降级、可恢复。
7. 关键数据可备份、可恢复、可校验。
8. 后续业务进入时有明确的接入边界，不污染底座。
9. Copilot 后续开发不会跑偏、不会重复造轮子、不会破坏基线。

---

## 1. 全局强制规则

### 1.1 当前阶段禁止实现内容

所有 PR 均不得实现以下能力：

1. 不接入硬件控制。
2. 不接入 PLC、IO、摆轮、输送线、扫码枪、DWS、相机、变频器。
3. 不实现实时分拣任务执行状态机。
4. 不新增 WCS/ERP 外部业务对接。
5. 不新增 JWT、RBAC、API-Key 等鉴权。
6. 不实现 UI。
7. 不引入消息中间件作为必须依赖。
8. 不引入复杂分布式事务。
9. 不把当前底座改成微服务集合。
10. 不使用 UTC 时间语义。

### 1.2 时间语义强制规则

全项目统一本地时间语义。

禁止：

```csharp
DateTime.UtcNow
DateTimeOffset.UtcNow
DateTime.ToUniversalTime()
DateTimeOffset.ToUniversalTime()
```

允许：

```csharp
var now = DateTime.Now;
```

配置禁止出现：

```text
Z
+08:00
-0500
```

所有新增时间字段若表示本地时间，命名必须体现 Local 语义，例如：

```csharp
CreatedAtLocal
CheckedAtLocal
StartedAtLocal
CompletedAtLocal
```

已有字段保持兼容，不强制大规模重命名，避免破坏现有模型。

### 1.3 代码风格强制规则

1. 新增 C# 代码优先使用 `var`。
2. 能使用 `record class` / `record struct` 满足需求时，优先使用 `record`。
3. `record class` 中不可空属性必须使用 `required`。
4. 事件载荷必须使用 `record class` 或 `record struct`，命名必须以 `EventArgs` 结尾。
5. 布尔属性必须使用 `Is` / `Has` / `Can` / `Should` 前缀。
6. `enum` 必须带 XML 注释。
7. `enum` 每个枚举值必须带 `[Description("中文说明")]`。
8. 注释必须使用中文。
9. 注释中禁止出现第二人称。
10. 异常、日志、ProblemDetails 提示必须使用中文。
11. 业务精度数值优先使用 `decimal`。
12. 热路径性能计时使用 `Stopwatch.GetTimestamp()`。
13. 热路径禁止反射。
14. 热路径禁止多次枚举。
15. 不允许创建影分身代码。

### 1.4 架构边界强制规则

#### Domain 层

只允许放：

```text
聚合
实体
值对象
领域事件
领域枚举
领域仓储契约
领域规则
领域只读模型契约
```

禁止：

```text
EF Core
HTTP
JSON 序列化细节
数据库 Provider
HostedService
配置绑定
```

#### Application 层

只允许放：

```text
应用服务
用例编排
应用层 DTO 映射
跨仓储流程编排
参数守卫
```

禁止：

```text
DbContext
EF Core 查询
SQL
HTTP Endpoint
后台线程
硬件协议
```

#### Contracts 层

只允许放：

```text
API 请求合同
API 响应合同
对外枚举
分页合同
导入导出合同
```

禁止：

```text
业务实现
EF Core
Infrastructure 引用
Host 引用
```

#### Infrastructure 层

只允许放：

```text
数据库
仓储实现
EF Core
分表
归档
备份
恢复
写入缓冲
慢查询
数据库方言
诊断
观测落地实现
```

#### Host 层

只允许放：

```text
Program
Minimal API 路由
HealthCheck
HostedService 接线
Options
Middleware
Swagger
组合根注册
```

禁止在 `Program.cs` 中继续堆积大量实现逻辑。新增模块必须通过扩展方法注册。

### 1.5 README / 更新记录 / 台账规则

每个 PR 必须更新：

```text
README.md
更新记录.md
检查台账/文件清单基线.txt
检查台账/PR-*-检查台账.md
```

要求：

1. README 文件树必须与仓库实际文件一致。
2. 新增文件必须写职责。
3. 删除文件必须同步移除。
4. `检查台账/文件清单基线.txt` 必须由 `git ls-files` 更新。
5. `更新记录.md` 必须追加 PR 断点摘要。
6. 下一 PR 必须能通过断点摘要继续。
7. 不允许遗漏文件。
8. 不允许 README 写不存在的文件。

### 1.6 覆盖旧实现时的强制清理规则

如果新增代码覆盖了旧实现，必须同时：

1. 明确被覆盖的旧文件或旧方法。
2. 删除旧代码。
3. 删除旧 DI 注册。
4. 删除旧配置。
5. 删除旧测试桩。
6. 更新 README。
7. 更新文件清单基线。
8. 在 `更新记录.md` 写明删除原因。

如果只是增强，不得删除旧实现。

---

## 2. 多 PR 断点续接规则

### 2.1 每个 PR 开始前必须读取

Copilot 每个 PR 开始前必须读取：

```text
README.md
更新记录.md
检查台账/文件清单基线.txt
上一 PR 检查台账
Zeye.Sorting.Hub.sln
相关 .csproj
Program.cs
appsettings.json
```

### 2.2 每个 PR 结束必须写断点摘要

格式：

```markdown
## PR-X 断点摘要

### 已完成
- ...

### 新增文件
- ...

### 修改文件
- ...

### 删除文件
- 无 / ...

### 保留能力
- ...

### 未完成但已预留
- ...

### 下一 PR 入口
- 下一 PR 从 ... 开始
- 禁止重复实现 ...
- 禁止删除 ...
```

### 2.3 禁止重复实现清单

新增任何服务前必须全仓搜索：

```text
DatabaseConnection
HealthCheck
Cursor
WriteBuffer
DeadLetter
ShardingInspection
Archive
Backup
Restore
Outbox
Inbox
Telemetry
Metric
SlowQuery
Migration
Seed
DataRetention
DangerousAction
SafeExecutor
```

若已有同类能力，必须扩展原实现。

---

## 3. 总体 PR 路线图

```text
PR-A  数据库连接诊断与就绪状态增强
PR-B  查询保护与游标分页
PR-C  批量写入缓冲与死信隔离
PR-D  分表巡检、预建与索引检查
PR-E  数据归档与冷热分层
PR-F  数据库底座 CI 门禁增强
PR-G  数据库迁移治理与回滚资产
PR-H  种子数据、基线数据与配置一致性校验
PR-I  慢查询指纹聚合与查询画像
PR-J  查询模板治理与索引建议闭环
PR-K  写入幂等、去重与重复键治理
PR-L  Outbox 事件底座与业务事件持久化
PR-M  Inbox 幂等消费底座
PR-N  数据保留策略与自动清理治理
PR-O  备份、恢复、校验与演练底座
PR-P  报表查询隔离与只读副本预留
PR-Q  租户 / 站点 / 设备维度数据边界预留
PR-R  业务模块接入模板与代码生成规范
PR-S  压测工程与性能基线报告
PR-T  生产运行 Runbook、应急预案与最终底座验收
```

---

# PR-A：数据库连接诊断与就绪状态增强

## 目标

增强无人值守场景下数据库底座的启动可靠性、连接池自检、数据库恢复感知和 `/health/ready` 诊断能力。

## 不做内容

1. 不改鉴权。
2. 不改硬件控制。
3. 不改 Parcel 业务语义。
4. 不删除现有健康检查。
5. 不删除现有 `/health/live`、`/health`。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionDiagnosticsOptions.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionHealthSnapshot.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/IDatabaseConnectionDiagnostics.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionDiagnosticsService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/DatabaseConnectionWarmupService.cs
Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs
Zeye.Sorting.Hub.Host/HostedServices/DatabaseConnectionWarmupHostedService.cs
Zeye.Sorting.Hub.Host.Tests/DatabaseConnectionDiagnosticsTests.cs
```

## 实现要求

1. 通过 `IDbContextFactory<SortingHubDbContext>` 创建短生命周期上下文。
2. 探测必须捕获异常并转为快照。
3. 最近一次快照必须可被 HealthCheck 读取。
4. 快照必须包含 Provider、Database、CheckedAtLocal、ElapsedMilliseconds、连续失败次数、连续成功次数。
5. 连接预热不允许阻塞主请求。
6. HostedService 捕获所有异常。
7. 日志必须中文。
8. 不引入 UTC。

## 配置新增

```jsonc
"Persistence": {
  "Diagnostics": {
    "IsWarmupEnabled": true,
    "WarmupConnectionCount": 4,
    "ProbeTimeoutMilliseconds": 3000,
    "FailureThreshold": 3,
    "RecoveryThreshold": 2
  }
}
```

## 测试要求

1. 默认配置合法。
2. 非法配置拒绝。
3. 连接失败不向外抛异常。
4. 连续失败达到阈值后 Unhealthy。
5. 连续成功后恢复 Healthy。
6. 快照时间为本地时间语义。
7. HealthCheck Data 包含关键字段。

## 断点摘要

```markdown
## PR-A 断点摘要

### 已完成
- 数据库连接诊断服务
- 数据库连接预热服务
- 就绪探针详细状态输出

### 下一 PR 入口
- PR-B 从游标分页与查询保护开始
- 不要重复实现数据库连接诊断
```

---

# PR-B：查询保护与游标分页

## 目标

增强高并发查询能力，避免深分页、大范围查询和默认 Count 拖垮数据库。

## 不做内容

1. 不删除 `GET /api/parcels`。
2. 不修改详情接口语义。
3. 不把旧分页替换成游标分页。
4. 不接硬件。
5. 不做业务路由。

## 新增文件

```text
Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelCursorListRequest.cs
Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelCursorListResponse.cs
Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelCursorToken.cs
Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs
Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/CursorPageRequest.cs
Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/CursorPageResult.cs
Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelCursorQueryExtensions.cs
Zeye.Sorting.Hub.Host/QueryParameters/ParcelCursorListQueryParameters.cs
Zeye.Sorting.Hub.Host.Tests/ParcelCursorQueryTests.cs
```

## 新增接口

```http
GET /api/parcels/cursor
```

## 游标规则

固定排序：

```text
ScannedTime DESC, Id DESC
```

游标条件：

```text
ScannedTime < LastScannedTimeLocal
OR
ScannedTime = LastScannedTimeLocal AND Id < LastId
```

## 查询保护规则

1. 游标分页默认 `PageSize=50`。
2. 最大 `PageSize=200`。
3. 普通分页最大页码限制 10000。
4. 无时间范围默认最近 24 小时。
5. 时间范围超过 3 个月返回 400。
6. `includeTotalCount=false` 时不得执行 `LongCountAsync`。
7. 游标分页必须多取一条判断 `HasMore`。

## 测试要求

1. 首页查询。
2. 第二页游标查询。
3. 游标非法返回 400。
4. 时间范围保护。
5. 排序稳定性。
6. 相同时间按 Id 稳定翻页。
7. 不影响旧分页接口。

## 断点摘要

```markdown
## PR-B 断点摘要

### 已完成
- Parcel 游标分页
- 查询保护规则
- Cursor Token 编解码

### 保留
- GET /api/parcels 继续作为兼容分页接口

### 下一 PR 入口
- PR-C 从批量写入缓冲开始
- 不要重复实现 Cursor Token
- 不要删除普通分页接口
```

---

# PR-C：批量写入缓冲与死信隔离

## 目标

为高并发写入建立低阻塞、有界、可观测、可失败隔离的写入底座。

## 不做内容

1. 不替换现有同步新增接口。
2. 不删除 `CreateParcelCommandService`。
3. 不让同步新增变成最终一致。
4. 不绕过仓储。

## 新增文件

```text
Zeye.Sorting.Hub.Application/Services/WriteBuffers/BufferedWriteOptions.cs
Zeye.Sorting.Hub.Application/Services/WriteBuffers/BufferedWriteResult.cs
Zeye.Sorting.Hub.Application/Services/WriteBuffers/IBufferedWriteService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/BoundedWriteChannel.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/BufferedParcelWriteItem.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/ParcelBufferedWriteService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/ParcelBatchWriteFlushService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/DeadLetterWriteEntry.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/DeadLetterWriteStore.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/BatchWriteMetricsSnapshot.cs
Zeye.Sorting.Hub.Host/HostedServices/ParcelBatchWriteFlushHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs
Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelBatchBufferedCreateRequest.cs
Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelBatchBufferedCreateResponse.cs
Zeye.Sorting.Hub.Host.Tests/ParcelBufferedWriteTests.cs
```

## 新增接口

```http
POST /api/admin/parcels/batch-buffer
```

返回必须包含：

```text
acceptedCount
rejectedCount
queueDepth
isBackpressureTriggered
message
```

## Channel 规则

必须使用：

```csharp
System.Threading.Channels.Channel<T>
```

配置：

```jsonc
"Persistence": {
  "WriteBuffering": {
    "IsEnabled": true,
    "ChannelCapacity": 10000,
    "BatchSize": 500,
    "FlushIntervalMilliseconds": 200,
    "MaxRetryCount": 3,
    "BackpressureRejectThreshold": 9000,
    "DeadLetterCapacity": 10000
  }
}
```

## 性能要求

1. 入队路径不得访问数据库。
2. 入队路径不得使用反射。
3. 入队路径不得做复杂映射。
4. Flush 批次不得每条 `SaveChangesAsync`。
5. 死信必须有容量上限。
6. HostedService 必须支持取消和异常隔离。

## 断点摘要

```markdown
## PR-C 断点摘要

### 已完成
- Parcel 批量缓冲写入
- 有界 Channel
- Flush 后台服务
- 死信隔离
- 写入队列健康检查

### 保留
- POST /api/admin/parcels 仍为同步强一致新增接口

### 下一 PR 入口
- PR-D 从分表巡检与预建开始
- 不要把同步新增接口改成缓冲写入
```

---

# PR-D：分表巡检、预建与索引检查

## 目标

增强无人值守场景下的分表运行时可信度，提前发现缺表、缺索引、热点分表与容量风险。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTableInspectionService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTablePrebuildService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingIndexInspectionService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingCapacitySnapshotService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingInspectionReport.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingPrebuildPlan.cs
Zeye.Sorting.Hub.Host/HostedServices/ShardingInspectionHostedService.cs
Zeye.Sorting.Hub.Host/HostedServices/ShardingPrebuildHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/ShardingGovernanceHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/ShardingInspectionTests.cs
```

## 巡检内容

1. 当前周期物理表是否存在。
2. 下一个周期物理表是否存在。
3. 未来预建窗口是否满足。
4. 必要索引是否存在。
5. 单表行数是否过高。
6. 热点比例是否过高。
7. WebRequestAuditLog 热表与详情表是否同步存在。

## 配置新增

```jsonc
"Persistence": {
  "Sharding": {
    "RuntimeInspection": {
      "IsEnabled": true,
      "InspectionIntervalMinutes": 30,
      "ShouldCheckIndexes": true,
      "ShouldCheckNextPeriodTables": true,
      "ShouldCheckCapacity": true
    },
    "Prebuild": {
      "IsEnabled": true,
      "DryRun": true,
      "PrebuildAheadHours": 72
    }
  }
}
```

## 强制规则

1. 默认只输出计划。
2. 真实预建必须走危险动作隔离器。
3. 必须支持 dry-run。
4. 必须记录中文审计日志。
5. 必须支持 MySQL / SQL Server Provider 差异。

## 断点摘要

```markdown
## PR-D 断点摘要

### 已完成
- 分表物理表巡检
- 分表预建计划
- 分表索引检查
- 分表治理健康检查

### 下一 PR 入口
- PR-E 从数据归档与冷热分层开始
- 不要重复实现分表探测
```

---

# PR-E：数据归档与冷热分层

## 目标

为长期无人值守运行提供历史数据治理能力，避免热库无限膨胀。

## 新增文件

```text
Zeye.Sorting.Hub.Domain/Aggregates/DataGovernance/ArchiveTask.cs
Zeye.Sorting.Hub.Domain/Enums/DataGovernance/ArchiveTaskStatus.cs
Zeye.Sorting.Hub.Domain/Enums/DataGovernance/ArchiveTaskType.cs
Zeye.Sorting.Hub.Domain/Repositories/IArchiveTaskRepository.cs
Zeye.Sorting.Hub.Application/Services/DataGovernance/CreateArchiveTaskCommandService.cs
Zeye.Sorting.Hub.Application/Services/DataGovernance/GetArchiveTaskPagedQueryService.cs
Zeye.Sorting.Hub.Application/Services/DataGovernance/RetryArchiveTaskCommandService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchivePlanner.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveExecutor.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveCheckpointStore.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/DataArchiveHostedWorker.cs
Zeye.Sorting.Hub.Infrastructure/Repositories/ArchiveTaskRepository.cs
Zeye.Sorting.Hub.Host/Routing/DataGovernanceApiRouteExtensions.cs
Zeye.Sorting.Hub.Host/HostedServices/DataArchiveHostedService.cs
Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskCreateRequest.cs
Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskListRequest.cs
Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskListResponse.cs
Zeye.Sorting.Hub.Contracts/Models/DataGovernance/ArchiveTaskResponse.cs
```

## 初期策略

只实现：

```text
计划
审计
dry-run
状态记录
重试入口
```

暂不做：

```text
真实删除
真实迁移到冷库
压缩导出
不可逆操作
```

## 新增 API

```http
POST /api/data-governance/archive-tasks
GET /api/data-governance/archive-tasks
POST /api/data-governance/archive-tasks/{id:long}/retry
```

## 断点摘要

```markdown
## PR-E 断点摘要

### 已完成
- 归档任务聚合
- 归档计划 dry-run
- 归档任务 API
- 归档任务查询与重试

### 下一 PR 入口
- PR-F 从数据库底座 CI 门禁增强开始
- 不要执行真实历史数据删除
```

---

# PR-F：数据库底座 CI 门禁增强

## 目标

建立持续门禁，防止后续代码破坏数据库底座。

## 新增文件

```text
.github/workflows/database-foundation-gates.yml
.github/scripts/validate-database-foundation-rules.sh
.github/scripts/validate-no-utc.sh
.github/scripts/validate-readme-file-tree.sh
.github/scripts/validate-sensitive-config.sh
.github/scripts/validate-no-shadow-code.sh
数据库底座门禁说明.md
```

## CI 必须检查

1. `dotnet restore`
2. `dotnet build --configuration Release`
3. `dotnet test --configuration Release`
4. 禁止 UTC API。
5. 禁止配置时区后缀。
6. 禁止敏感连接字符串。
7. README 文件树与 `git ls-files` 对齐。
8. 新增枚举必须带 Description。
9. 新增 HostedService 必须捕获异常。
10. 新增后台循环必须支持 CancellationToken。
11. 新增批量写入必须有有界容量。
12. 禁止影分身代码。

## 敏感配置拦截

必须拦截：

```text
pwd=
Password=
User Id=sa
uid=root
AccessKey
SecretKey
Token=
```

允许：

```text
Password=<请通过环境变量注入>
```

## 断点摘要

```markdown
## PR-F 断点摘要

### 已完成
- 数据库底座 CI 门禁
- UTC 禁止检查
- README 文件树检查
- 敏感配置检查
- 影分身代码检查

### 下一 PR 入口
- PR-G 从数据库迁移治理与回滚资产开始
```

---

# PR-G：数据库迁移治理与回滚资产

## 目标

让数据库结构变更可审计、可预演、可回滚、可归档，避免业务开发后迁移混乱。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationPlan.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationExecutionRecord.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationScriptArchiveService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationRollbackScriptProvider.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/MigrationGovernance/MigrationSafetyEvaluator.cs
Zeye.Sorting.Hub.Host/HostedServices/MigrationGovernanceHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/MigrationGovernanceHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/MigrationGovernanceTests.cs
```

## 实现要求

1. 启动时读取当前 EF Core 迁移列表。
2. 生成待执行迁移计划。
3. 对迁移脚本进行归档。
4. 输出潜在危险操作提示。
5. 支持 dry-run。
6. 生产环境默认禁止自动执行危险迁移。
7. 迁移失败必须写入结构化记录。
8. `/health/ready` 可暴露迁移未完成或迁移失败状态。
9. 不直接执行不可逆回滚。
10. 回滚脚本只作为资产归档与人工执行参考。

## 危险操作识别

至少识别：

```text
DROP TABLE
DROP COLUMN
TRUNCATE
ALTER COLUMN
RENAME COLUMN
RENAME TABLE
DELETE FROM
UPDATE without WHERE
```

## 配置新增

```jsonc
"Persistence": {
  "MigrationGovernance": {
    "IsEnabled": true,
    "DryRun": true,
    "ArchiveDirectory": "migration-scripts",
    "BlockDangerousMigrationInProduction": true
  }
}
```

## 测试要求

1. 无迁移时 Healthy。
2. 有待执行迁移时 Degraded。
3. 检测危险 SQL。
4. dry-run 不执行迁移。
5. 脚本归档路径正确。
6. 启动异常不导致服务静默退出。

## 断点摘要

```markdown
## PR-G 断点摘要

### 已完成
- 迁移计划生成
- 迁移脚本归档
- 危险迁移识别
- 迁移治理健康检查

### 下一 PR 入口
- PR-H 从种子数据、基线数据与配置一致性校验开始
- 不要重复实现迁移检测
```

---

# PR-H：种子数据、基线数据与配置一致性校验

## 目标

确保业务开发前基础配置、枚举映射、默认数据、数据库配置一致，避免运行期才发现缺失。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataOptions.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataValidationResult.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataValidator.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Baseline/BaselineDataSeeder.cs
Zeye.Sorting.Hub.Host/HostedServices/BaselineDataValidationHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/BaselineDataHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/BaselineDataTests.cs
```

## 校验内容

1. 必要配置节点是否存在。
2. 分表起始时间是否合法。
3. Provider 与连接字符串是否匹配。
4. 关键枚举 Description 是否完整。
5. 默认数据是否存在。
6. 默认数据是否重复。
7. 配置中的表名、字段名是否和代码约定一致。
8. 所有本地时间配置不包含时区后缀。

## 配置新增

```jsonc
"Persistence": {
  "BaselineData": {
    "IsValidationEnabled": true,
    "IsSeedEnabled": false,
    "FailureMode": "Degraded"
  }
}
```

## 强制规则

1. 默认只校验，不自动写入。
2. 自动写入必须明确启用。
3. 写入必须幂等。
4. 异常必须隔离。
5. 校验失败必须有中文说明。

## 断点摘要

```markdown
## PR-H 断点摘要

### 已完成
- 基线数据校验
- 配置一致性校验
- 可选幂等种子数据入口

### 下一 PR 入口
- PR-I 从慢查询指纹聚合与查询画像开始
```

---

# PR-I：慢查询指纹聚合与查询画像

## 目标

把已有慢查询采集能力升级为可分析、可聚合、可排名、可定位的查询画像底座。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryFingerprint.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryFingerprintAggregator.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileSnapshot.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryProfileStore.cs
Zeye.Sorting.Hub.Contracts/Models/Diagnostics/SlowQueryProfileResponse.cs
Zeye.Sorting.Hub.Contracts/Models/Diagnostics/SlowQueryProfileListResponse.cs
Zeye.Sorting.Hub.Application/Services/Diagnostics/GetSlowQueryProfileQueryService.cs
Zeye.Sorting.Hub.Host/Routing/DiagnosticsApiRouteExtensions.cs
Zeye.Sorting.Hub.Host.Tests/SlowQueryFingerprintTests.cs
```

## 实现要求

1. SQL 指纹去参数化。
2. 聚合维度包含调用次数、平均耗时、P95、P99、最大耗时、超时次数、异常次数。
3. TopN 可配置。
4. 只保留最近窗口，避免内存无限增长。
5. 可通过 API 查询诊断快照。
6. 查询画像 API 不允许触发数据库重查询，必须读取内存快照或轻量存储。
7. 日志中文。

## 新增 API

```http
GET /api/diagnostics/slow-queries
GET /api/diagnostics/slow-queries/{fingerprint}
```

## 配置新增

```jsonc
"Persistence": {
  "AutoTuning": {
    "SlowQueryProfile": {
      "IsEnabled": true,
      "WindowMinutes": 30,
      "TopN": 50,
      "MaxFingerprintCount": 1000
    }
  }
}
```

## 断点摘要

```markdown
## PR-I 断点摘要

### 已完成
- 慢查询指纹聚合
- 查询画像快照
- 诊断 API

### 下一 PR 入口
- PR-J 从查询模板治理与索引建议闭环开始
```

---

# PR-J：查询模板治理与索引建议闭环

## 目标

在业务开发前建立查询模板与索引建议闭环，防止后续业务随意写低效查询。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryTemplateDescriptor.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryTemplateRegistry.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryIndexRecommendation.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryIndexRecommendationService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/QueryGovernance/QueryGovernanceReport.cs
Zeye.Sorting.Hub.Host/HostedServices/QueryGovernanceReportHostedService.cs
Zeye.Sorting.Hub.Host.Tests/QueryGovernanceTests.cs
```

## 查询模板要求

每个高频查询必须登记：

```text
模板名称
业务用途
涉及表
过滤字段
排序字段
建议索引
最大时间范围
是否允许 Count
是否允许深分页
```

## 初期必须登记

```text
ParcelRecentCursorQuery
ParcelByBarcodeQuery
ParcelByChuteQuery
ParcelByWorkstationQuery
WebRequestAuditLogCursorQuery
ArchiveTaskListQuery
```

## 强制规则

1. 新增高频查询必须登记模板。
2. 未登记模板的查询不得进入高并发路径。
3. 索引建议只输出，不自动执行。
4. 自动建索引仍需走危险动作隔离器，不在本 PR 实现。

## 断点摘要

```markdown
## PR-J 断点摘要

### 已完成
- 查询模板登记
- 索引建议模型
- 查询治理报告

### 下一 PR 入口
- PR-K 从写入幂等、去重与重复键治理开始
```

---

# PR-K：写入幂等、去重与重复键治理

## 目标

为高并发写入和后续业务接入提供幂等保障，避免重复写入、重复事件、重复请求导致数据污染。

## 新增文件

```text
Zeye.Sorting.Hub.Domain/Aggregates/Idempotency/IdempotencyRecord.cs
Zeye.Sorting.Hub.Domain/Enums/Idempotency/IdempotencyRecordStatus.cs
Zeye.Sorting.Hub.Domain/Repositories/IIdempotencyRepository.cs
Zeye.Sorting.Hub.Application/Services/Idempotency/IdempotencyGuardService.cs
Zeye.Sorting.Hub.Infrastructure/Repositories/IdempotencyRepository.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Idempotency/IdempotencyKeyHasher.cs
Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/IdempotencyRecordEntityTypeConfiguration.cs
Zeye.Sorting.Hub.Host.Tests/IdempotencyTests.cs
```

## 幂等键规则

幂等键由以下组合生成：

```text
SourceSystem
OperationName
BusinessKey
PayloadHash
```

## 实现要求

1. 幂等记录必须有唯一索引。
2. PayloadHash 使用 SHA256。
3. 幂等记录状态必须有 Description。
4. 同一个幂等键重复写入必须返回已有结果或明确拒绝。
5. 幂等记录过期清理必须走危险动作治理。
6. 不允许使用内存字典作为唯一幂等机制。
7. 需要支持后续业务模块复用。

## 断点摘要

```markdown
## PR-K 断点摘要

### 已完成
- 幂等记录聚合
- 幂等仓储
- 幂等 Guard 服务
- 写入去重基础能力

### 下一 PR 入口
- PR-L 从 Outbox 事件底座开始
```

---

# PR-L：Outbox 事件底座与业务事件持久化

## 目标

为后续业务模块提供可靠事件落库能力，避免业务处理成功但事件丢失。

## 新增文件

```text
Zeye.Sorting.Hub.Domain/Aggregates/Events/OutboxMessage.cs
Zeye.Sorting.Hub.Domain/Enums/Events/OutboxMessageStatus.cs
Zeye.Sorting.Hub.Domain/Repositories/IOutboxMessageRepository.cs
Zeye.Sorting.Hub.Application/Services/Events/AppendOutboxMessageCommandService.cs
Zeye.Sorting.Hub.Application/Services/Events/GetOutboxMessagePagedQueryService.cs
Zeye.Sorting.Hub.Infrastructure/Repositories/OutboxMessageRepository.cs
Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/OutboxMessageEntityTypeConfiguration.cs
Zeye.Sorting.Hub.Host/HostedServices/OutboxDispatchHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/OutboxHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/OutboxMessageTests.cs
```

## 初期策略

当前阶段只做：

```text
事件持久化
状态流转
重试计数
失败隔离
健康检查
```

不做：

```text
Kafka
RabbitMQ
外部消息推送
跨服务分布式事务
```

## 状态

```text
Pending
Processing
Succeeded
Failed
DeadLettered
```

每个枚举值必须带 `[Description]`。

## 要求

1. Outbox 写入必须与业务数据写入在同一个 DbContext 事务中预留支持。
2. 当前若没有业务事务入口，先提供独立写入能力。
3. 后台派发服务初期只做状态推进模拟或日志派发，不接外部 MQ。
4. 失败重试必须有最大次数。
5. 死信必须可查询。
6. 不允许无限重试。

## 断点摘要

```markdown
## PR-L 断点摘要

### 已完成
- OutboxMessage 聚合
- Outbox 仓储
- Outbox 状态流转
- Outbox 健康检查

### 下一 PR 入口
- PR-M 从 Inbox 幂等消费底座开始
```

---

# PR-M：Inbox 幂等消费底座

## 目标

为后续接收外部业务事件或内部异步事件提供幂等消费记录，避免重复消费。

## 新增文件

```text
Zeye.Sorting.Hub.Domain/Aggregates/Events/InboxMessage.cs
Zeye.Sorting.Hub.Domain/Enums/Events/InboxMessageStatus.cs
Zeye.Sorting.Hub.Domain/Repositories/IInboxMessageRepository.cs
Zeye.Sorting.Hub.Application/Services/Events/InboxMessageGuardService.cs
Zeye.Sorting.Hub.Infrastructure/Repositories/InboxMessageRepository.cs
Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/InboxMessageEntityTypeConfiguration.cs
Zeye.Sorting.Hub.Host.Tests/InboxMessageTests.cs
```

## 实现要求

1. 每条外部事件必须有唯一 MessageId。
2. 同一 SourceSystem + MessageId 不得重复消费。
3. 记录消费状态。
4. 记录错误消息。
5. 支持重试。
6. 支持过期清理治理。
7. 不接外部 MQ，仅建立底座。

## 断点摘要

```markdown
## PR-M 断点摘要

### 已完成
- InboxMessage 聚合
- Inbox 幂等消费记录
- 消费状态治理

### 下一 PR 入口
- PR-N 从数据保留策略与自动清理治理开始
```

---

# PR-N：数据保留策略与自动清理治理

## 目标

统一治理日志、审计、Outbox、Inbox、幂等记录、归档任务等长期数据的保留策略。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionOptions.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionPolicy.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionPlanner.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionExecutor.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Retention/DataRetentionAuditRecord.cs
Zeye.Sorting.Hub.Host/HostedServices/DataRetentionHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/DataRetentionHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/DataRetentionTests.cs
```

## 保留对象

至少支持：

```text
WebRequestAuditLog
OutboxMessage
InboxMessage
IdempotencyRecord
ArchiveTask
DeadLetterWriteEntry
SlowQueryProfile
```

## 策略

1. 默认 dry-run。
2. 清理必须走危险动作隔离器。
3. 每次清理必须分批。
4. 每批必须有上限。
5. 必须记录审计日志。
6. 不允许一次性大范围删除。
7. 清理失败不得影响主服务运行。

## 配置示例

```jsonc
"Persistence": {
  "Retention": {
    "IsEnabled": true,
    "DryRun": true,
    "BatchSize": 1000,
    "Policies": [
      {
        "Name": "WebRequestAuditLog",
        "RetentionDays": 30
      },
      {
        "Name": "OutboxMessage",
        "RetentionDays": 14
      }
    ]
  }
}
```

## 断点摘要

```markdown
## PR-N 断点摘要

### 已完成
- 数据保留策略
- 清理计划
- dry-run 执行器
- 保留治理健康检查

### 下一 PR 入口
- PR-O 从备份、恢复、校验与演练底座开始
```

---

# PR-O：备份、恢复、校验与演练底座

## 目标

为无人值守运行建立备份恢复与演练机制，避免数据库损坏或误删后无恢复路径。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupOptions.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupPlan.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupExecutionRecord.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/IBackupProvider.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/MySqlBackupProvider.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/SqlServerBackupProvider.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/BackupVerificationService.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/Backup/RestoreDrillPlanner.cs
Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs
Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/BackupGovernanceTests.cs
```

## 当前阶段策略

只做：

```text
备份计划
备份命令生成
备份结果记录
备份文件存在性校验
恢复演练记录
```

不做：

```text
自动覆盖生产库恢复
自动删除生产数据
自动执行危险恢复
```

## 要求

1. MySQL / SQL Server Provider 分开实现。
2. 默认 dry-run。
3. 备份文件路径必须配置。
4. 备份失败必须 Degraded。
5. 超过预期未备份必须告警。
6. 恢复演练必须记录到 `drill-records`。
7. 生产恢复只能生成 Runbook，不自动执行。

## 断点摘要

```markdown
## PR-O 断点摘要

### 已完成
- 备份计划
- Provider 备份命令生成
- 备份校验
- 恢复演练记录

### 下一 PR 入口
- PR-P 从报表查询隔离与只读副本预留开始
```

---

# PR-P：报表查询隔离与只读副本预留

## 目标

避免后续报表、大屏、统计查询拖垮在线查询链路。

## 新增文件

```text
Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReadOnlyDatabaseOptions.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReadOnlyDbContextFactorySelector.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReportingQueryGuard.cs
Zeye.Sorting.Hub.Infrastructure/Persistence/ReadModels/ReportingQueryBudget.cs
Zeye.Sorting.Hub.Host/HealthChecks/ReadOnlyDatabaseHealthCheck.cs
Zeye.Sorting.Hub.Host.Tests/ReportingQueryIsolationTests.cs
```

## 实现要求

1. 支持配置只读连接字符串。
2. 读副本不可用时可配置回退主库或直接拒绝。
3. 报表查询必须限制时间范围。
4. 报表查询必须限制最大返回行数。
5. 报表查询必须默认不返回总数。
6. 报表查询必须与在线 Parcel 查询服务分开。
7. 当前只建立底座，不实现具体报表业务。

## 配置示例

```jsonc
"Persistence": {
  "ReadOnlyDatabase": {
    "IsEnabled": false,
    "FallbackToPrimaryWhenUnavailable": false,
    "MaxReportTimeRangeDays": 31,
    "MaxReportRows": 10000
  }
}
```

## 断点摘要

```markdown
## PR-P 断点摘要

### 已完成
- 只读数据库配置底座
- 报表查询预算
- 报表查询隔离守卫

### 下一 PR 入口
- PR-Q 从租户 / 站点 / 设备维度数据边界预留开始
```

---

# PR-Q：租户 / 站点 / 设备维度数据边界预留

## 目标

为后续业务扩展预留站点、产线、设备、客户等维度，不在业务写死单站点结构。

## 新增文件

```text
Zeye.Sorting.Hub.Domain/ValueObjects/OperationalScope.cs
Zeye.Sorting.Hub.Domain/ValueObjects/SiteIdentity.cs
Zeye.Sorting.Hub.Domain/ValueObjects/LineIdentity.cs
Zeye.Sorting.Hub.Domain/ValueObjects/DeviceIdentity.cs
Zeye.Sorting.Hub.Contracts/Models/Common/OperationalScopeRequest.cs
Zeye.Sorting.Hub.Contracts/Models/Common/OperationalScopeResponse.cs
Zeye.Sorting.Hub.Application/Utilities/OperationalScopeGuard.cs
Zeye.Sorting.Hub.Host.Tests/OperationalScopeTests.cs
```

## 要求

1. 只建立数据边界模型，不做租户鉴权。
2. 不修改所有现有表结构，除非必要。
3. 新增业务模块必须优先接入 OperationalScope。
4. Scope 字段命名必须清晰：
   - SiteCode
   - LineCode
   - DeviceCode
   - WorkstationName
5. 不允许用模糊字段名，如 `Code1`、`Tag`、`Group`。

## 断点摘要

```markdown
## PR-Q 断点摘要

### 已完成
- OperationalScope 值对象
- 站点/产线/设备维度建模规范
- 后续业务接入边界

### 下一 PR 入口
- PR-R 从业务模块接入模板与代码生成规范开始
```

---

# PR-R：业务模块接入模板与代码生成规范

## 目标

在正式开始处理业务前，建立统一的业务模块接入模板，避免后续每个业务都随意放文件、随意写接口、随意查库。

## 新增文件

```text
业务模块接入规范.md
Copilot-业务模块新增模板.md
Zeye.Sorting.Hub.Application/Utilities/ApplicationResult.cs
Zeye.Sorting.Hub.Application/Utilities/ApplicationErrorCodes.cs
Zeye.Sorting.Hub.Host/Routing/EndpointRouteBuilderConventionExtensions.cs
Zeye.Sorting.Hub.Host.Tests/BusinessModuleTemplateRulesTests.cs
```

## 业务模块标准结构

每个新业务模块必须遵循：

```text
Domain
├── Aggregates/{ModuleName}
├── Enums/{ModuleName}
├── Events/{ModuleName}
└── Repositories/I{ModuleName}Repository.cs

Application
└── Services/{ModuleName}

Contracts
└── Models/{ModuleName}

Infrastructure
├── Repositories/{ModuleName}Repository.cs
└── EntityConfigurations/{ModuleName}EntityTypeConfiguration.cs

Host
└── Routing/{ModuleName}ApiRouteExtensions.cs

Host.Tests
└── {ModuleName}ApiTests.cs
```

## 强制规则

1. 业务接口不得直接返回领域实体。
2. 业务查询必须有时间范围保护。
3. 高频列表必须优先游标分页。
4. 写入必须考虑幂等。
5. 需要事件时必须优先使用 Outbox。
6. 需要外部事件消费时必须使用 Inbox。
7. 需要批量写入时必须优先复用 WriteBuffer。
8. 不允许新增业务私有的影分身分页模型。
9. 不允许新增业务私有的影分身结果模型。
10. 不允许绕过统一异常与 ProblemDetails 策略。

## 断点摘要

```markdown
## PR-R 断点摘要

### 已完成
- 业务模块标准结构
- 应用服务结果模型
- 业务接入 Copilot 模板
- Endpoint 路由约定

### 下一 PR 入口
- PR-S 从压测工程与性能基线报告开始
```

---

# PR-S：压测工程与性能基线报告

## 目标

在业务进入前建立性能基线，避免后续无法判断性能是否退化。

## 新增文件

```text
performance/README.md
performance/k6/parcel-cursor-query.js
performance/k6/parcel-batch-buffer-write.js
performance/k6/audit-query.js
performance/results/.gitkeep
性能基线报告.md
.github/workflows/performance-smoke-test.yml
```

## 压测范围

1. Parcel 游标分页。
2. Parcel 普通分页。
3. Parcel 批量缓冲写入。
4. 审计日志查询。
5. HealthCheck。
6. 慢查询画像 API。

## 指标要求

至少记录：

```text
RPS
P50
P95
P99
错误率
超时率
数据库连接池占用
写入队列深度
CPU
内存
GC 次数
```

## 门禁策略

CI 中只跑轻量 smoke test，不跑完整压测。

完整压测通过文档手动执行：

```bash
k6 run performance/k6/parcel-cursor-query.js
k6 run performance/k6/parcel-batch-buffer-write.js
```

## 断点摘要

```markdown
## PR-S 断点摘要

### 已完成
- 压测脚本
- 性能基线报告模板
- 轻量性能 smoke test

### 下一 PR 入口
- PR-T 从生产运行 Runbook、应急预案与最终底座验收开始
```

---

# PR-T：生产运行 Runbook、应急预案与最终底座验收

## 目标

把数据库底座能力沉淀为可交付、可运维、可验收的生产运行手册。

## 新增文件

```text
生产运行Runbook.md
数据库故障应急预案.md
分表治理Runbook.md
备份恢复演练Runbook.md
业务接入前底座验收清单.md
无人值守运行检查清单.md
```

## Runbook 必须覆盖

1. 服务启动失败。
2. 数据库连接失败。
3. 数据库连接池耗尽。
4. 慢查询暴增。
5. 写入队列积压。
6. 死信堆积。
7. 分表缺失。
8. 索引缺失。
9. 磁盘空间不足。
10. 备份失败。
11. 迁移失败。
12. 归档任务失败。
13. 审计日志过大。
14. 查询 P99 升高。
15. 内存持续增长。
16. CPU 持续过高。
17. 数据重复写入。
18. 幂等冲突。
19. Outbox 堆积。
20. Inbox 重复消费。

## 最终验收清单

完成 PR-A 到 PR-T 后，项目必须具备：

1. 数据库连接诊断。
2. 数据库连接预热。
3. 详细就绪探针。
4. 查询保护。
5. 游标分页。
6. 批量写入缓冲。
7. 背压拒绝。
8. 死信隔离。
9. 分表巡检。
10. 分表预建计划。
11. 索引检查。
12. 归档 dry-run。
13. CI 数据库底座门禁。
14. 迁移治理。
15. 回滚资产归档。
16. 基线数据校验。
17. 慢查询画像。
18. 查询模板登记。
19. 索引建议。
20. 写入幂等。
21. Outbox 底座。
22. Inbox 底座。
23. 数据保留策略。
24. 备份计划。
25. 恢复演练记录。
26. 报表查询隔离。
27. 只读副本预留。
28. OperationalScope 数据边界。
29. 业务模块接入模板。
30. 性能压测脚本。
31. 性能基线报告。
32. 生产 Runbook。
33. 应急预案。
34. README 与台账完整。
35. 无 UTC 语义。
36. 无敏感配置。
37. 无影分身代码。
38. 无硬件控制入侵。
39. 无鉴权跑偏实现。
40. 可开始进入业务模块开发。

## 断点摘要

```markdown
## PR-T 断点摘要

### 已完成
- 生产运行 Runbook
- 故障应急预案
- 分表治理手册
- 备份恢复演练手册
- 业务接入前底座验收清单

### 下一阶段
- 开始正式业务模块开发
- 所有业务模块必须遵守 业务模块接入规范.md
```

---

## 4. 每个 PR 固定门禁命令

每个 PR 必须执行：

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

必须执行：

```bash
grep -R "DateTime.UtcNow\|DateTimeOffset.UtcNow\|ToUniversalTime" -n .
grep -R "Z$\\|+08:00\\|-0500" -n Zeye.Sorting.Hub.Host/appsettings*.json
```

必须确认：

```text
README.md 已更新
更新记录.md 已更新
检查台账/文件清单基线.txt 已更新
检查台账/PR-*-检查台账.md 已更新
无敏感配置
无影分身代码
```

---

## 5. Copilot 每次执行固定提示词

```markdown
请先读取 README.md、更新记录.md、检查台账/文件清单基线.txt、上一 PR 检查台账、Zeye.Sorting.Hub.sln、相关 .csproj、Program.cs、appsettings.json，然后再开始修改。

当前项目定位是无人值守高并发数据库底座，不接硬件控制，不接鉴权，不做实时分拣执行链路。

本 PR 必须严格遵守：
1. 禁止 UTC 时间语义。
2. 禁止影分身代码。
3. 新增文件必须更新 README.md 文件树。
4. 新增/删除文件必须更新 检查台账/文件清单基线.txt。
5. 必须补充测试。
6. 必须在 更新记录.md 写入本 PR 断点摘要。
7. 不得删除现有接口，除非本 PR 明确要求覆盖并清理旧实现。
8. 不得绕过危险动作隔离器。
9. 所有异常与提示使用中文。
10. 所有 enum 值必须带 Description。
11. 所有后台服务必须捕获异常并支持 CancellationToken。
12. 所有高并发查询必须有边界保护。
13. 所有高并发写入必须有有界容量和背压策略。
14. 所有业务扩展必须遵守业务模块接入规范。
```

---

## 6. 业务开发前最终结论

当 PR-A 到 PR-T 全部完成后，`Zeye.Sorting.Hub` 才进入适合承载复杂业务的状态。

业务开发开始后，任何新增业务都必须优先复用：

```text
游标分页
查询保护
写入缓冲
幂等 Guard
Outbox
Inbox
分表治理
数据保留
审计日志
健康检查
慢查询画像
查询模板登记
OperationalScope
统一 ApplicationResult
```

不得为单个业务模块重复创建私有底座能力。

