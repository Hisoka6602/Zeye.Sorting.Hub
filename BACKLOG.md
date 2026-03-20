# 待完善事项（BACKLOG）

> 本文件记录代码中**尚未实现**的可完善点，供后续迭代参考。  
> **已实现项不在此记录**（例如物理分表存在性探测、ParcelDetailResponse 值对象明细合同等均已落地）。  
> 更新规则：每次 PR 合入后请同步移除已实现项，或将新的待完善点追加到对应章节。

---

## 分表治理

1. 在 CI 中增加"下周期分表预建校验"门禁（按 `PrebuildWindowHours` 自动检查），将当前运行期守卫升级为发布前自动门禁。
2. 为 `ExpansionPlan` 增加结构化字段（阶段、窗口、回滚脚本路径），替代纯文本描述。
3. 将三项分表指标（`autotuning.sharding.hit_rate`、`autotuning.sharding.cross_table_query_ratio`、`autotuning.sharding.hot_table_skew`）接入 Prometheus/Grafana 告警面板，补齐阈值化运营闭环。
4. 将 `CurrentEstimatedRowsPerShard` / `CurrentObservedHotRatio` 从手工配置升级为真实观测源（数据库统计表或可观测指标），减少人工维护成本。
5. 在隔离器框架下补充"阈值命中后的自动切换编排（开关 + dry-run + 审计 + 回滚脚本）"，逐步从决策骨架演进到安全可控的自动化治理。
6. 为 Time/Volume/Hybrid 策略增加分环境差异化模板（生产更保守、压测环境更激进）与 CI 配置校验门禁。
7. 评估引入 `PerHour` 或日内 hash bucket 作为 "PerDay 仍过大" 的下一层细粒度策略。
8. 将 `Volume:Observation:Source` 与可观测平台打通，沉淀来源标签与采样时间戳，提高阈值决策可审计性。
9. Parcel 关联值对象分表规则已收敛为单清单，但新增类型仍需补一条声明；后续可评估通过属性标记/约定推断进一步自动化，减少人工维护成本。
10. `FutureExecutable` 生命周期需在隔离器（开关 + dry-run + 审计 + 回滚）边界内逐步接入可控执行编排。
11. 为 `BucketedPerDay` 增加更细的策略参数（例如路由字段组合、桶热点重平衡提示），并补充运维演练 Runbook 模板。
12. finer-granularity 真实自动执行仍需严格在隔离器边界内演进（开关 + dry-run + 审计 + 回滚边界全部就绪后再放开）。
13. 哈希扩容/重分片自动执行属于下一阶段能力，当前未实现。

---

## 迁移治理

1. 若后续 SQL Server 与 MySQL 的迁移演进差异持续扩大，可升级为"独立迁移程序集"策略，进一步降低跨提供器误用风险。
2. 在流水线上增加迁移脚本归档（artifact）与 DBA 审批节点，形成可追溯发布审计链路。
3. 将"月度回滚演练 / 季度灾备升降级演练"接入流水线门禁，自动校验演练记录完备性。

---

## 自动调谐治理

1. 接入真实数据库执行计划视图（MySQL `EXPLAIN ANALYZE` / SQL Server Query Store）替代默认日志探针，减少 unavailable 占比。
2. 将自动验证 snapshot diff 输出落地到结构化审计表（而非仅日志），支持长周期追踪与可视化报表。
3. 引入按表/按业务域的动态阈值学习（结合历史分位数），降低统一阈值在不同负载模型下的误报率。
4. 为闭环动作增加端到端压测回放（离线流量）验证门禁，进一步提升生产变更安全性。
5. 在不引入并行执行器的前提下，继续引入"数据库真实索引元数据（运行时）+ 模型索引（静态）"双源比对，进一步降低跨环境误判率。
6. 对"低价值索引"规则引入按表历史基线学习（例如分位数阈值），减少统一阈值在不同业务负载下的偏差。

---

## 规则治理

1. 增加 CI 静态检查（如 Roslyn Analyzer 或自定义脚本）自动门禁"字段/方法注释、事件载荷目录与类型、枚举 Description 完整性"，避免回归。
2. 为事件载荷结构体（`ParcelScannedEventArgs`、`ParcelChuteAssignedEventArgs`）补充业务字段与对应单元测试，替换当前最小占位定义（当前仅为空结构体）。

---

## Parcel 仓储

1. 若后续仓储增多，可将 `MaxTimeRangeAttribute` 提升为更通用的查询验证组件，并在应用层统一入参校验链路中复用。
2. 对于包含全部扁平字段的 `ParcelSummaryReadModel`，后续可按"接口场景"拆分成轻量/完整两个读模型版本，以平衡带宽与可用性。
3. 在不破坏当前契约的前提下，为分页列表逐步补充更细粒度排序/筛选选项（保持 summary 模型边界不变）。
4. 若后续出现更多聚合仓储，可逐步统一读路径是否也采用 `RepositoryResult<T>` 语义，以便跨仓储错误处理策略一致。
5. 可按业务错误类型对 `RepositoryResult.ErrorMessage` 做结构化编码，减少上层字符串分支判断。

---

## Parcel 异常治理

1. 在 Application/Contracts 层补充 `ParcelExceptionType` 的对外 DTO/查询筛选条件，减少字符串化状态判断。
2. 按异常类型建立告警分级策略（例如机械故障/包裹丢失优先级高于超时类），提升运维响应效率。

---

## 压测治理

1. 在 CI 中增加"压测数据自动清理"步骤，防止种子数据影响正式环境。
2. 将 sysbench 压测脚本封装为可复用的 Shell/Makefile 目标，纳入仓库 `scripts/` 目录管理。
3. 接入 Prometheus + Grafana，将 AutoTuning 的分表观测指标（命中率、倾斜度、跨表查询占比）可视化，实现压测期间实时大盘监控。

---

## 危险删除治理

1. 在具备成熟数据备份/归档体系后，可评估将"删除前归档 + 可执行补偿脚本路径"纳入 `DangerousBatchActionResult`，将当前文本边界升级为可执行治理资产。
2. 可在告警平台增加"危险删除被阻断次数 / dry-run 次数 / 真实执行次数"指标看板，提升治理策略可观测性。
3. cleanup-expired 端点可结合定时任务（如 `IHostedService` + Cron）实现自动触发，目前为纯手动调用。

---

## API 与接口

1. 后续可按场景拆分"轻量列表 DTO / 完整列表 DTO"以降低网络负载（当前列表接口已为扁平字段摘要）。
2. 可在后续迭代补充统一参数模型验证器（如分页上限、字符串长度）并输出字段级错误明细，进一步增强 API 可观测性与前端联调体验。
3. 可补充 `/swagger/v1/swagger.json` 结构断言测试，防止后续重构时端点元数据（tags/summary/response）回退。
4. 引入鉴权体系（JWT/API-Key/RBAC）后，在 `MapParcelAdminApis` 的 `MapGroup` 上追加 `.RequireAuthorization("AdminPolicy")` 统一保护普通写接口，治理接口额外追加 `"DangerousActionPolicy"`。
5. 可在 Application 层为新增/更新接口引入 FluentValidation 或自定义 Validator，实现字段级错误聚合输出。
6. `AddRangeAsync` 目前未暴露为 API，如后续有批量导入业务需求，可在充分评估风险后，通过治理型端点（带 dry-run + 上限保护）暴露。
7. 可为 `GET /api/parcels` 补充"带多重过滤条件的成功路径"测试，覆盖 bagCode、workstationName、actualChuteId 等过滤参数的联合使用。
8. 引入 FluentValidation 或统一参数模型验证器后，可追加字段级错误明细的结构化断言。
9. 引入真实鉴权框架后，补充鉴权测试：未携带 Token 时管理端接口返回 401，权限不足时返回 403。
10. 建立面向 Swagger 规范的契约断言测试，防止重构时端点 tags/summary/response 回退。

---

## 可观测性与运营

1. 可在测试层补充统一的"本地时间语义输入构造约束"测试工具或约定，进一步降低后续引入 UTC 相关 API 的回归风险。
2. 可进一步抽取方言层"表名转义/限定名拼装"公共骨架，在不改变方言 SQL 细节的前提下继续降低重复率。
3. 可在 AutoTuning 相关测试中增加配置键拼装的参数化覆盖，减少未来配置项扩展时的回归风险。
