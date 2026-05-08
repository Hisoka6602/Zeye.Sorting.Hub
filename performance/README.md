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

## 七、CI 轻量 smoke test 说明

PR-S 引入的 `.github/workflows/performance-smoke-test.yml` 只执行轻量规则验证，不运行真实压测：

1. 仅在压测资产、测试文件或相关文档变更时触发。
2. 仅执行 `PerformanceBaselineRulesTests`，校验脚本、文档与 workflow 的关键约束。
3. 完整压测仍由人工在受控环境执行，结果回填到 `性能基线报告.md` 或 `performance/results/` 中的非版本化产物。

---

## 八、结果沉淀约定

1. 基线摘要写入仓库根目录 `性能基线报告.md`。
2. 原始控制台输出、截图、CSV 或 JSON 结果统一存放在 `performance/results/` 的本地产物中，不提交真实压测数据。
3. 每次刷新基线时，需说明环境、数据规模、配置快照与结论，避免不同环境结果横向误比。
