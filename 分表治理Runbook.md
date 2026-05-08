# 分表治理 Runbook

> 适用范围：分表缺失、索引缺失、预建窗口不足、策略切换门禁触发  
> 关联实现：`ShardingInspectionHostedService`、`ShardingPrebuildHostedService`、`ShardingGovernanceHealthCheck`、`ShardingTableInspectionService`、`ShardingTablePrebuildService`、`ShardingIndexInspectionService`

---

## 一、目标

1. 保证新时间窗口写入前物理表已预建。
2. 保证逻辑表与物理表索引集合一致。
3. 保证策略切换仍受门禁保护，不允许低命中率条件下贸然切换。

---

## 二、日常检查

1. 检查 `/health/ready` 中 `ShardingGovernanceHealthCheck` 输出。
2. 检查 `ShardingInspectionHostedService` 最近巡检日志。
3. 检查 `ShardingPrebuildHostedService` 最近预建计划执行结果。
4. 检查 `PrebuildWindowHours`、`ShardingGovernanceHitRateThreshold` 配置是否仍在安全区间。

---

## 三、故障场景处理

### 1. 分表缺失

1. 先确认缺失的是热表、历史表还是预建目标表。
2. 读取 `ShardingTableInspectionService` 输出的缺表清单。
3. 核对 `DatabaseInitializerHostedService` 与 `ShardingPrebuildHostedService` 是否已运行。
4. 需要补建时，先确认逻辑表名、分片时间窗口、数据库 Provider 与当前 schema。
5. 补建完成后重新执行巡检，确认缺表项归零。

### 2. 索引缺失

1. 读取 `ShardingIndexInspectionService` 的缺索引结果。
2. 区分是主表缺索引还是新分表未继承索引。
3. 先在低风险窗口补齐索引，再确认查询计划恢复。
4. 处理后检查慢查询画像是否下降。

### 3. 预建窗口不足

1. 检查 `.github/workflows/stability-gates.yml` 的 `sharding-prebuild-gate` 是否仍通过。
2. 检查 `PrebuildWindowHours` 是否小于最小门槛。
3. 若预建窗口被误改，优先恢复配置并重新执行预建任务。

### 4. 策略切换门禁触发

1. 检查 `ValidateShardingStrategyGate` 相关日志与告警。
2. 若命中率低于 `ShardingGovernanceHitRateThreshold`，禁止强行切换。
3. 先修复热点倾斜、跨表比例或索引缺口，再重新评估策略。

---

## 四、变更后复核

1. `ShardingGovernanceHealthCheck` 恢复 Healthy 或至少无缺表、无缺索引。
2. 新时间窗口写入验证通过。
3. 慢查询画像中与分表相关的热点查询下降。
4. 处理结果同步到 `生产运行Runbook.md` 关联记录或当次台账。
