# 生产运行 Runbook

> 适用阶段：长期数据库底座 PR-T  
> 适用范围：`Zeye.Sorting.Hub` 生产运行、值守交接、故障首响、恢复验证  
> 关联资料：`数据库故障应急预案.md`、`分表治理Runbook.md`、`备份恢复演练Runbook.md`、`业务接入前底座验收清单.md`、`无人值守运行检查清单.md`

---

## 一、执行前提

1. 先确认当前长期数据库底座已完成至 PR-S，PR-T 仅补齐生产运行资料与最终验收口径。
2. 所有排障动作必须优先走只读检查、dry-run、审计留痕，再决定是否执行危险动作。
3. 所有异常处理必须以 NLog 落盘日志、健康探针结果、审计记录、台账记录作为证据。

---

## 二、统一入口

### 1. 健康探针与核心入口

- 存活探针：`/health/live`
- 就绪探针：`/health/ready`
- 诊断 API：`/api/diagnostics/slow-queries`
- 数据治理 API：`/api/data-governance/archive-tasks`、`/api/data-governance/outbox-messages`
- 只读查询入口：`/api/parcels`、`/api/parcels/cursor`、`/api/audit/web-requests`

### 2. 优先检查的实现位置

- 启动与迁移：`Zeye.Sorting.Hub.Host/HostedServices/DatabaseInitializerHostedService.cs`
- 数据库连接预热：`Zeye.Sorting.Hub.Host/HostedServices/DatabaseConnectionWarmupHostedService.cs`
- 数据库详细诊断：`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs`
- 批量缓冲写入：`Zeye.Sorting.Hub.Host/HostedServices/ParcelBatchWriteFlushHostedService.cs`
- 分表巡检与预建：`Zeye.Sorting.Hub.Host/HostedServices/ShardingInspectionHostedService.cs`、`Zeye.Sorting.Hub.Host/HostedServices/ShardingPrebuildHostedService.cs`
- 备份与恢复：`Zeye.Sorting.Hub.Host/HostedServices/BackupHostedService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/BackupHealthCheck.cs`
- 数据保留治理：`Zeye.Sorting.Hub.Host/HostedServices/DataRetentionHostedService.cs`
- Outbox：`Zeye.Sorting.Hub.Host/HostedServices/OutboxDispatchHostedService.cs`
- 长期门禁：`.github/workflows/stability-gates.yml`

---

## 三、20 个必覆盖场景速查表

| 场景 | 主要症状 | 第一检查点 | 首选处理路径 |
|---|---|---|---|
| 服务启动失败 | 进程退出、`/health/live` 不可用 | `DatabaseInitializerHostedService`、启动日志 | 先查迁移/配置/连接串，再按 `数据库故障应急预案.md` 执行 |
| 数据库连接失败 | `/health/ready` 降级或失败 | `DatabaseConnectionDetailedHealthCheck` | 检查 Provider、连接串、网络与数据库服务状态 |
| 数据库连接池耗尽 | 请求超时、数据库等待激增 | `ResourceThresholds`、NLog 慢请求日志 | 限流只读请求、检查慢查询与长事务 |
| 慢查询暴增 | `/api/diagnostics/slow-queries` 指纹暴涨 | `SlowQueryProfileStore`、`QueryGovernanceReportHostedService` | 先看模板命中与索引建议，再评估是否切只读副本 |
| 写入队列积压 | 批量写入响应变慢、Flush 延迟升高 | `BufferedWriteQueueHealthCheck` | 检查后台 Flush、死信与数据库写入吞吐 |
| 死信堆积 | 死信数量持续上升 | `DeadLetterWriteStore`、队列健康检查 | 先导出失败载荷，再修正数据或写入条件 |
| 分表缺失 | 新时间窗口写入失败或查询缺表 | `ShardingGovernanceHealthCheck` | 按 `分表治理Runbook.md` 执行巡检与预建 |
| 索引缺失 | 查询计划回退全表扫描 | `ShardingIndexInspectionService` | 依据索引检查结果补齐索引资产 |
| 磁盘空间不足 | 备份、日志、归档写入失败 | NLog 文件目录、备份目录、归档目录 | 先清点日志/备份/归档占用，再按保留策略清理 |
| 备份失败 | 备份健康检查降级 | `BackupHostedService`、`BackupHealthCheck` | 按 `备份恢复演练Runbook.md` 检查计划、命令与目录 |
| 迁移失败 | 启动阻断、迁移治理失败 | `MigrationGovernanceHostedService`、迁移健康检查 | 先执行 dry-run，再按回滚资产恢复 |
| 归档任务失败 | 归档任务卡住或失败数增加 | `DataArchiveHostedService`、归档 API | 先重试 dry-run，再核查目标表与窗口条件 |
| 审计日志过大 | 审计表膨胀、查询明显变慢 | `DataRetentionHostedService`、审计查询接口 | 评估保留策略、冷热分层与清理计划 |
| 查询 P99 升高 | P99 长时间超阈值 | 性能基线、慢查询画像、只读副本健康检查 | 先比对基线，再决定限流、索引、只读副本切流 |
| 内存持续增长 | 进程 RSS 持续走高 | `ResourceThresholds`、后台队列长度 | 先查缓冲队列、审计采集与缓存快照大小 |
| CPU 持续过高 | CPU 长时间接近阈值 | 慢查询画像、后台任务周期 | 先辨别是查询热点、归档/分表任务还是日志风暴 |
| 数据重复写入 | 幂等键命中率异常、重复业务键 | `IdempotencyGuardService` | 先检查幂等键、载荷哈希与重放结果 |
| 幂等冲突 | 处理中状态长时间不释放 | `IdempotencyGuardException`、幂等仓储 | 检查处理中超时、取消重试与业务补偿 |
| Outbox 堆积 | 待处理/失败/死信数量上升 | `OutboxHealthCheck`、`OutboxDispatchHostedService` | 先查消息状态推进，再决定重派发或死信隔离 |
| Inbox 重复消费 | 同一消息多次进入处理链路 | `InboxMessageGuardService` | 先查消息键、处理中状态与失败重试窗口 |

---

## 四、标准排障步骤

### 1. 首响 10 分钟

1. 记录故障开始时间、本地时间窗口、影响接口、影响租户/站点/设备边界。
2. 检查 `/health/live` 与 `/health/ready`，确认是进程故障、依赖故障还是局部功能降级。
3. 检查对应 HealthCheck 输出的 `Data` 字段，优先抓取数据库、分表、备份、Outbox、保留治理快照。
4. 查询 NLog 最新错误日志，按 `EvidenceId`、`CorrelationId` 汇总同一故障链路。

### 2. 30 分钟内定位

1. 将故障归类到数据库连接、查询、写入、分表、归档、备份、幂等、事件投递中的一种主链路。
2. 对高风险动作先执行 dry-run，不允许直接批量删除、直接跳过迁移或绕过隔离器。
3. 若涉及数据库写入风险，优先暂停高风险后台任务，再保留只读查询能力。
4. 若涉及结构风险，先锁定当前迁移版本、分表窗口与回滚资产位置。

### 3. 恢复后复核

1. 再次检查 `/health/ready`、相关 API、后台任务恢复状态。
2. 对比 `性能基线报告.md` 中基线口径，确认 P95/P99、错误率、队列深度是否回归。
3. 将处理结果沉淀到 `更新记录.md`、`drill-records/` 或当次应急记录中。

---

## 五、关键场景处理指引

### 1. 服务启动失败 / 迁移失败

1. 检查 `DatabaseInitializerHostedService`、`MigrationGovernanceHostedService` 日志。
2. 优先确认配置文件是否缺失 Provider、连接串、迁移治理选项。
3. 若迁移预演已判定危险 SQL，禁止跳过门禁直接启动。
4. 若必须回退，优先使用已归档的迁移脚本与回滚资产。

### 2. 数据库连接失败 / 连接池耗尽

1. 读取 `DatabaseConnectionDetailedHealthCheck` 输出的 provider、database、失败次数、成功次数。
2. 若为连接失败，先排查数据库服务、网络、连接串与凭据占位符配置。
3. 若为连接池耗尽，先检查慢查询、长事务、批量写入 Flush 与报表查询是否压满主库。
4. 必要时启用报表只读副本路线，避免在线写链路继续受压。

### 3. 慢查询暴增 / 查询 P99 升高

1. 调用 `/api/diagnostics/slow-queries` 获取当前指纹画像。
2. 查看 `QueryTemplateRegistry` 与 `QueryIndexRecommendationService` 输出，确认是否存在未登记模板或索引缺口。
3. 对照 `性能基线报告.md` 的最近基线，确认异常是否超出正常波动。
4. 若只读副本可用，优先把报表型读流量切走。

### 4. 写入队列积压 / 死信堆积

1. 检查 `BufferedWriteQueueHealthCheck` 队列深度、死信数量、最后 Flush 状态。
2. 检查 `ParcelBatchWriteFlushHostedService` 是否持续运行、是否存在数据库写失败重试。
3. 导出死信样本后再决定修复策略，禁止直接清空死信。
4. 若主库压力过大，优先降低批量写入入口流量。

### 5. 分表缺失 / 索引缺失

1. 读取 `ShardingGovernanceHealthCheck` 输出的缺表、缺索引、预建窗口状态。
2. 检查 `ShardingInspectionHostedService`、`ShardingPrebuildHostedService` 是否正常执行。
3. 需要补建物理表或索引时，先在 `分表治理Runbook.md` 的检查表完成核对。
4. 完成后重新执行健康检查，确认风险消除。

### 6. 备份失败 / 磁盘空间不足

1. 检查 `BackupHealthCheck` 返回的最新备份文件、Runbook 路径、演练记录路径。
2. 检查备份目录、归档目录、NLog 目录容量是否超过安全阈值。
3. 若空间不足，优先按保留策略清理历史日志、旧备份与归档产物。
4. 恢复后执行一次恢复演练核验，不允许只看备份文件存在就结束。

### 7. 归档任务失败 / 审计日志过大

1. 检查归档任务分页接口与后台 HostedService 日志。
2. 优先执行 dry-run，确认计划量、执行量、失败原因与时间窗口。
3. 审计日志膨胀时，同时核查 `DataRetentionHostedService` 是否按计划运行。
4. 需要清理时，保留审计痕迹与补偿边界。

### 8. 数据重复写入 / 幂等冲突 / Outbox 堆积 / Inbox 重复消费

1. 先定位业务键、幂等键、消息键是否稳定。
2. 检查 `IdempotencyGuardService`、`OutboxDispatchHostedService`、`InboxMessageGuardService` 的状态推进日志。
3. 若存在处理中长时间不释放，优先排查后台任务卡顿与数据库写入冲突。
4. 仅在证据完整且已保留回放上下文后，才允许做人工补偿。

### 9. CPU 持续过高 / 内存持续增长

1. 先区分是查询、后台任务、日志风暴还是缓冲队列导致。
2. 检查慢查询画像、归档/分表/备份/Outbox 周期任务是否在同一时间窗口集中触发。
3. 检查写入队列、审计后台队列、慢查询画像快照是否持续积压。
4. 必要时先降级低优先级任务，再恢复主链路。

---

## 六、升级、回退与交接要求

1. 所有生产变更先通过 `.github/workflows/stability-gates.yml`。
2. 季度演练记录统一沉淀到 `drill-records/`。
3. 每次故障处理后，至少更新一次对应台账或运行记录，保证后续交接可续接。
4. 若开始新业务模块开发，必须先通过 `业务接入前底座验收清单.md`。
