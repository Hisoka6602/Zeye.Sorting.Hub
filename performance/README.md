# 压测工程说明

> 本目录对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》的 PR-S。  
> 目标是在业务模块大规模接入前，为现有数据库底座建立可复用、可追溯、可手动执行的 API 级性能基线资产。

来源：
- 《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-S 交付边界
- `检查台账/PR-长期数据库底座S-检查台账.md`
- 仓库内现有接口、测试与 README 结构约束

---

## 一、目录说明

```text
performance/
├── README.md
├── k6/
│   ├── common.js
│   ├── parcel-cursor-query.js
│   ├── parcel-batch-buffer-write.js
│   └── audit-query.js
└── results/
    └── .gitkeep
```

- `k6/common.js`：压测脚本共用的本地时间格式化、环境变量解析、请求头与批量写入载荷构造逻辑。
- `k6/common.js` 中的 `parcelTimestamp` 按 `.NET DateTime.Ticks` 语义生成，并通过十进制字面量字符串规避 JavaScript 大整数精度丢失。
- `k6/parcel-cursor-query.js`：覆盖 Parcel 游标分页与普通分页两类高频读取链路。
- `k6/parcel-batch-buffer-write.js`：覆盖 Parcel 批量缓冲写入链路。
- `k6/audit-query.js`：覆盖审计日志查询、`/health/ready` 与慢查询画像 API。
- `results/`：保留压测结果落盘目录；真实结果文件不纳入版本控制。

---

## 二、覆盖范围映射

| 路线图要求 | 资产位置 | 说明 |
|---|---|---|
| Parcel 游标分页 | `k6/parcel-cursor-query.js` | 默认执行 `/api/parcels/cursor` |
| Parcel 普通分页 | `k6/parcel-cursor-query.js` | 同脚本内追加 `/api/parcels` 场景 |
| Parcel 批量缓冲写入 | `k6/parcel-batch-buffer-write.js` | 默认执行 `/api/admin/parcels/batch-buffer` |
| 审计日志查询 | `k6/audit-query.js` | 默认执行 `/api/audit/web-requests` |
| HealthCheck | `k6/audit-query.js` | 默认执行 `/health/ready` |
| 慢查询画像 API | `k6/audit-query.js` | 默认执行 `/api/diagnostics/slow-queries` |

---

## 三、执行前置条件

1. 先执行 `dotnet build Zeye.Sorting.Hub.sln -v quiet` 与 `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet`，确认当前仓库处于可运行状态。
2. 目标环境必须已准备好本地时间语义数据，时间查询参数只能使用无 `Z`、无 offset 的本地时间字符串，例如 `2026-05-08 08:00:00`。
3. 若执行写入压测，建议使用隔离环境，并提前确认有界缓冲队列容量、数据库连接池上限与日志磁盘容量。
4. 若执行审计日志查询与慢查询画像压测，需先准备足量测试数据，避免压测结果被空数据短路。

---

## 四、环境变量

| 变量名 | 默认值 | 说明 |
|---|---|---|
| `BASE_URL` | `http://127.0.0.1:5000` | 压测目标服务地址 |
| `PERF_DURATION` | `30s` | 单脚本持续时间 |
| `PERF_VUS` | `4` | 默认并发虚拟用户数 |
| `PARCEL_BATCH_SIZE` | `10` | 批量缓冲写入脚本每次提交的包裹数 |
| `PARCEL_PAGE_SIZE` | `50` | Parcel 查询脚本页大小 |
| `AUDIT_PAGE_SIZE` | `50` | 审计日志查询页大小 |
| `PERF_REQUEST_TIMEOUT` | `15s` | 单请求超时保护 |
| `PERF_MAX_ERROR_RATE` | `0.01` | 最大错误率门禁 |
| `PERF_P95_MS` | 场景默认值 | 覆盖 P95 毫秒预算 |
| `PERF_P99_MS` | 场景默认值 | 覆盖 P99 毫秒预算 |
| `PERF_SLEEP_SECONDS` | `1` | 每轮场景间隔秒数；容量测试可设为 `0` |

---

## 五、手动执行命令

```bash
k6 run performance/k6/parcel-cursor-query.js
k6 run performance/k6/parcel-batch-buffer-write.js
k6 run performance/k6/audit-query.js
```

可按环境覆盖变量：

```bash
BASE_URL=http://127.0.0.1:5000 PERF_DURATION=2m PERF_VUS=8 k6 run performance/k6/parcel-cursor-query.js
BASE_URL=http://127.0.0.1:5000 PERF_DURATION=2m PERF_VUS=6 PARCEL_BATCH_SIZE=20 k6 run performance/k6/parcel-batch-buffer-write.js
BASE_URL=http://127.0.0.1:5000 PERF_DURATION=90s PERF_VUS=4 AUDIT_PAGE_SIZE=100 k6 run performance/k6/audit-query.js
```

---

## 六、指标采集要求

执行完整压测时，至少同步记录以下指标到 `性能基线报告.md`：

- RPS
- P50
- P95
- P99
- 错误率
- 超时率
- 数据库连接池占用
- 写入队列深度
- CPU
- 内存
- GC 次数

---

## 七、CI 门禁说明

PR-S 引入的 `.github/workflows/performance-smoke-test.yml` 在普通 PR 中只执行轻量规则验证：

1. 仅在压测资产、测试文件或相关文档变更时触发。
2. 仅执行 `PerformanceBaselineRulesTests`，校验脚本、文档与 workflow 的关键约束。
3. 不访问外部测试环境，不会误写业务数据。

`.github/workflows/performance-regression-gate.yml` 负责真实回归压测：

1. 可手动输入目标环境、持续时间和并发数，也可由工作日定时任务读取仓库变量 `PERFORMANCE_BASE_URL`。
2. 读取场景默认开启，写入场景必须显式选择或设置 `PERFORMANCE_ENABLE_WRITE=true`，避免误写。
3. k6 阈值失败会直接阻断任务；JSON 原始摘要与 Markdown 汇总作为 30 天构建产物留存。
4. `performance/scripts/summarize-k6.ps1` 可在本地或 CI 将多个 k6 JSON 摘要合并为统一表格。

---

## 八、结果沉淀约定

1. 基线摘要写入仓库根目录 `性能基线报告.md`。
2. 原始控制台输出、截图、CSV 或 JSON 结果统一存放在 `performance/results/` 的本地产物中，不提交真实压测数据。
3. 每次刷新基线时，需说明环境、数据规模、配置快照与结论，避免不同环境结果横向误比。

---

## 九、运行时性能指标

应用通过 `Zeye.Sorting.Hub.Performance` Meter 发布以下低开销指标，可由 OpenTelemetry 或 `dotnet-counters` 订阅：

- `sorting.audit.enqueued`、`sorting.audit.dropped`、`sorting.audit.queue.depth`
- `sorting.buffered_write.enqueued`、`sorting.buffered_write.dropped`、`sorting.buffered_write.queue.depth`
- `sorting.slow_query.collected`、`sorting.slow_query.dropped`

本地采集示例：

```bash
dotnet-counters monitor --name Zeye.Sorting.Hub.Host --counters Zeye.Sorting.Hub.Performance,System.Runtime,Microsoft.AspNetCore.Hosting
```

---

## 十、Fusion来源事实与跨分表报表基准

`ParcelAnalyticsBenchmark`直接调用生产使用的`ParcelProcessingRepository`、`ParcelAnalyticsReadService`和`ParcelRepository`，建立跨周期、失败尝试、未绑定DWS及迟到事实的确定性样本。它测量稳态写入、1/7/31天报表、包裹列表/游标/详情、并发游标读取及可选的混合读写P50/P95/P99，逐次核对报表与查询结果，并采集实际参数化报表SQL的MySQL `EXPLAIN ANALYZE`或SQL Server `STATISTICS XML`执行计划。建表耗时单独记录，不计入稳态写入。样本规模可调，未提供设备峰值和保留时长时，不将样本结果称为目标规模达标。

`ZEYE_BENCH_PAYLOAD_BYTES`允许填`0`至`262144`，默认`0`；设置为`32768`可复现大报文数据页的统计成本，结果中的`Input.PayloadBytes`记录该参数。比较优化前后结果时必须使用同一数据、日期窗口和报文大小。

工具强制`ZEYE_BENCH_ISOLATED=1`和`zeye_bench_`库名前缀。MySQL只允许本机非3306端口；SQL Server只允许`(localdb)\ZeyeQueryBench...`专用实例与集成认证。工具拒绝未迁移或已有事实的数据库，建表调用现有分表DDL隔离器。先创建专用隔离库；用同一连接串生成并审核迁移脚本，再执行迁移与基准。MySQL示例：

```powershell
$env:ZEYE_BENCH_ISOLATED = '1'
$env:ZEYE_BENCH_PROVIDER = 'MySql'
$env:ZEYE_BENCH_MYSQL = 'Server=127.0.0.1;Port=34068;Database=zeye_bench_example;User=root;SslMode=None;'
$env:ZEYE_BENCH_START_DATE = '2026-08-15'
$env:ZEYE_BENCH_DAYS = '31'
$env:ZEYE_BENCH_PARCELS_PER_DAY = '200'
$env:ZEYE_BENCH_CONCURRENCY = '8'
$env:ZEYE_BENCH_ITERATIONS = '15'
$env:ZEYE_BENCH_GRANULARITY = 'PerWeek'
$env:ZEYE_BENCH_READ_CONCURRENCY = '8'
$env:ZEYE_BENCH_READ_REQUESTS = '80'
$env:ZEYE_BENCH_READ_FANOUT = 'true'
$env:ZEYE_BENCH_READ_FANOUT_CONCURRENCY = '4'
$env:ZEYE_BENCH_READ_FANOUT_MAX_PARTITIONS = '12'
$env:ConnectionStrings__MySql = $env:ZEYE_BENCH_MYSQL
New-Item -ItemType Directory -Force .codex-artifacts/benchmark-results | Out-Null
dotnet ef migrations script --idempotent --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Host --output .codex-artifacts/benchmark-results/isolated-migration-preview.sql -- --provider MySql
# 审核脚本且确认数据库名称、端口后：
dotnet ef database update --project Zeye.Sorting.Hub.Infrastructure --startup-project Zeye.Sorting.Hub.Host -- --provider MySql
dotnet run -c Release --project performance/ParcelAnalyticsBenchmark/ParcelAnalyticsBenchmark.csproj
```

SQL Server LocalDB示例；实例和库名均为专用隔离名称，`dotnet ef`始终显式传入同一连接串：

```powershell
sqllocaldb create ZeyeQueryBenchExample
sqllocaldb start ZeyeQueryBenchExample
sqlcmd -S '(localdb)\ZeyeQueryBenchExample' -E -C -Q 'CREATE DATABASE [zeye_bench_example]'
$env:ZEYE_BENCH_ISOLATED = '1'
$env:ZEYE_BENCH_PROVIDER = 'SqlServer'
$env:ZEYE_BENCH_SQLSERVER = 'Server=(localdb)\ZeyeQueryBenchExample;Database=zeye_bench_example;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;'
$env:ZEYE_BENCH_DAYS = '31'
$env:ZEYE_BENCH_PARCELS_PER_DAY = '200'
$env:ZEYE_BENCH_CONCURRENCY = '8'
$env:ZEYE_BENCH_ITERATIONS = '21'
$env:ZEYE_BENCH_GRANULARITY = 'PerWeek'
dotnet ef database update --project Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations --startup-project Zeye.Sorting.Hub.Host --context SortingHubDbContext --connection $env:ZEYE_BENCH_SQLSERVER -- --provider SqlServer
dotnet run -c Release --project performance/ParcelAnalyticsBenchmark/ParcelAnalyticsBenchmark.csproj

# 同一批样本只读复测，false/true分别保存输出并核对Signature：
$env:ZEYE_BENCH_QUERY_ONLY = '1'
$env:ZEYE_BENCH_READ_FANOUT = 'false'
$env:ZEYE_BENCH_OUTPUT = '.codex-artifacts/benchmark-results/sqlserver-single-query.json'
dotnet run -c Release --project performance/ParcelAnalyticsBenchmark/ParcelAnalyticsBenchmark.csproj
$env:ZEYE_BENCH_READ_FANOUT = 'true'
$env:ZEYE_BENCH_OUTPUT = '.codex-artifacts/benchmark-results/sqlserver-fanout-query.json'
dotnet run -c Release --project performance/ParcelAnalyticsBenchmark/ParcelAnalyticsBenchmark.csproj

# 可选：向下一时间段写入新样本时并发查询固定历史窗口；只在造数完成后使用。
$env:ZEYE_BENCH_MIXED_WRITES = '500'
$env:ZEYE_BENCH_OUTPUT = '.codex-artifacts/benchmark-results/sqlserver-mixed.json'
dotnet run -c Release --project performance/ParcelAnalyticsBenchmark/ParcelAnalyticsBenchmark.csproj
```

`ZEYE_BENCH_DAYS`范围31～366，`ZEYE_BENCH_PARCELS_PER_DAY`范围1～10000，`ZEYE_BENCH_CONCURRENCY`和`ZEYE_BENCH_READ_CONCURRENCY`范围1～32，`ZEYE_BENCH_READ_REQUESTS`范围2～1000，`ZEYE_BENCH_ITERATIONS`范围2～100，`ZEYE_BENCH_MIXED_WRITES`范围0～10000；粒度允许`PerDay`、`PerWeek`、`PerMonth`。`ZEYE_BENCH_READ_FANOUT`可切换新旧读取路径，分表并行度范围1～8，最大分表数范围1～32。默认输出到`.codex-artifacts/benchmark-results/parcel-analytics-sample.json`，可由`ZEYE_BENCH_OUTPUT`覆盖。生成的JSON包含环境和样本参数、写入吞吐、各窗口延迟、包裹查询结果签名、并发读吞吐、可选混合读写结果、SQL及执行计划，不纳入仓库。复测同一库时设置`ZEYE_BENCH_QUERY_ONLY=1`并保持样本参数不变；需要核对同一批事实重放时可设置`ZEYE_BENCH_ALLOW_REPLAY=1`，重复写入耗时不得与首次写入基线混用。排障时设置`ZEYE_BENCH_VERBOSE_ERRORS=1`，错误堆栈写到标准错误。

包裹列表/游标读取的生产配置为`Persistence:Sharding:ReadFanout:Enabled`、`MaxConcurrency`和`MaxPartitions`。默认对超过2天且无条码子串条件的浅页查询启用有界分表读取；分表数超过12（含历史基础表）、单分表需读取超过2000条候选行的深页、短窗口或条码子串查询继续使用单SQL路径。压测应同时比较两种模式的P95、吞吐和结果签名，按目标环境的连接池与分表数量调优，不直接套用本机示例值。

MySQL迁移`20260928110851_AddParcelProcessingOccurredAtIndex`为基础事实表建索引；SQL Server空库基线`20260928143409_InitialSqlServerSchema`直接包含该索引。新周期物理表由当前EF模型自动带上索引。已有事实物理表先预览补建计划与回滚清单，确认维护窗口后再显式双开关执行：

```powershell
$indexBackfillParameters = @{
    MySqlExe = 'D:\WorkSpace\Zeye\Zeye.Sorting.Hub\.codex-artifacts\mysql-portable\mysql-8.0.46-winx64\bin\mysql.exe'
    Server = '127.0.0.1'; Port = 34068; Database = 'zeye_bench_example'; ExpectedDatabase = 'zeye_bench_example'; User = 'root'
    PlanPath = '.codex-artifacts/benchmark-results/occurred-index-plan.sql'
    RollbackPath = '.codex-artifacts/benchmark-results/occurred-index-rollback.sql'
}
& performance/scripts/backfill-processing-occurred-index.ps1 @indexBackfillParameters
# 审核计划后，仅对隔离库执行：
& performance/scripts/backfill-processing-occurred-index.ps1 @indexBackfillParameters -Apply -AllowDangerousActionExecution
```

SQL Server使用同一脚本的Provider分支；以下示例仅针对独立LocalDB实例，默认仍只生成计划：

```powershell
$sqlServerBackfill = @{
    Provider = 'SqlServer'
    SqlCmdExe = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE'
    Server = '(localdb)\ZeyeHubMigrationIsolated20260928'
    Database = 'zeye_hub_migration_isolated_20260928'
    ExpectedDatabase = 'zeye_hub_migration_isolated_20260928'
    TrustServerCertificate = $true
    PlanPath = '.codex-artifacts/sqlserver-validation/occurred-index-plan.sql'
    RollbackPath = '.codex-artifacts/sqlserver-validation/occurred-index-rollback.sql'
}
& performance/scripts/backfill-processing-occurred-index.ps1 @sqlServerBackfill
# 审核计划后，仅对隔离库执行：
& performance/scripts/backfill-processing-occurred-index.ps1 @sqlServerBackfill -Apply -AllowDangerousActionExecution
```

脚本逐张验证物理表后缀与现有索引，默认只写计划和反向`DROP INDEX`清单；默认审计文件为计划路径加`.audit.log`，可用`-AuditPath`覆盖。实际执行前必须核对目标数据库名称。SQL Server默认使用集成认证；若提供`-User`，密码只从`SQLCMDPASSWORD`环境变量读取。建索引可能占用I/O和锁，应在生产维护窗口按审查结果分批操作。
