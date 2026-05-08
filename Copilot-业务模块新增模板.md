# Copilot-业务模块新增模板

## 使用方式

当需要新增业务模块时，应先核对当前长期数据库底座台账，再基于以下模板向 Copilot 发起任务，避免遗漏结构边界、治理底座与文档同步要求。

## 任务模板

```text
开始实施 {ModuleName} 模块。

执行前先检查《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》与最新 PR 台账，确认当前底座完成度，并说明本次接入依赖哪些既有底座能力。

必须遵循：
1. 严格按 Domain / Application / Contracts / Infrastructure / Host / Host.Tests 六层落文件。
2. Domain 仅放聚合、值对象、事件、仓储契约；Infrastructure 才能放 EF Core 与仓储实现。
3. 查询必须带本地时间范围保护；高频列表优先游标分页。
4. 写入必须优先评估幂等；批量写入优先复用 WriteBuffer。
5. 需要事件持久化时优先使用 Outbox；消费外部事件时优先使用 Inbox。
6. 需要站点 / 产线 / 设备 / 工作站边界时，必须复用 OperationalScopeNormalizer、OperationalScopeRequest、OperationalScopeResponse。
7. Host 路由必须复用 EndpointRouteBuilderConventionExtensions，不得把实现直接堆进 Program.cs。
8. 应用层失败结果必须复用 ApplicationResult 与 ApplicationErrorCodes，并映射为统一 ProblemDetails。
9. 新增或删除文件后，必须同步更新 README.md、更新记录.md、检查台账/文件清单基线.txt 与对应检查台账。
10. 修改后必须执行现有 build、test、数据库底座门禁与最终并行校验。

输出要求：
- 先输出实施计划（Plan）。
- 实施前先说明当前已完成到哪个长期数据库底座 PR。
- 最终给出验收清单（Checklist），逐项标记 [x]/[ ]。
```

## 模块接入补充提示

- 如果模块存在高频明细查询，优先沿用游标分页合同结构，不要重新设计一套同义分页协议。
- 如果模块会出现“重复提交同一业务键”的风险，应先设计幂等键，再接入 `IdempotencyGuardService`。
- 如果模块写入后需要异步投递下游，应先定义事件载荷，再接入 Outbox，而不是直接写后台线程。
- 如果模块消费外部事件，需要先落 Inbox 状态，再做业务处理，避免重复消费。
- 如果模块需要跨站点 / 产线 / 设备查询，必须先定义运营边界输入，禁止散落多个字符串参数并各自做 trim / 空值判断。
