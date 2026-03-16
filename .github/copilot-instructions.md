# Copilot Repository Instructions

## 时间处理硬性规则

- 全项目禁止使用 UTC 时间语义和 UTC 相关 API，包括但不限于：
  - `DateTime.UtcNow`
  - `DateTimeOffset.UtcNow`
  - `DateTimeKind.Utc`
  - `ToUniversalTime()`
  - `UtcDateTime`
  - `DateTimeStyles.AssumeUniversal`
  - `DateTimeStyles.AdjustToUniversal`
- 统一使用本地时间语义，包括但不限于：
  - `DateTime.Now`
  - `DateTimeKind.Local`
  - `DateTimeStyles.AssumeLocal`

## 代码修改要求

- 任何新增或修改涉及时间的代码，必须保持本地时间语义一致，不得引入 UTC 转换链路。
- 如果读取配置中的时间字符串，默认按本地时间解析；示例配置不得使用 `Z` 或 offset（如 `+08:00`）。
- 每次新增文件或删除文件后，必须同步更新仓库根目录 `README.md` 中用于逐项说明目录/文件职责的章节（当前标题为“各层级与各文件作用说明（逐项）”），保证职责说明与仓库实际内容一致。

## PR 交付门禁（必须全部满足）

- 先输出“实施计划（Plan）”，再改代码；每完成一步更新进度。
- 所有改动文件必须通过编译；若无法编译，必须说明阻塞原因与替代验证。
- 每个 PR 必须附“验收清单（Checklist）”，逐条标注 [x]/[ ]。
- 涉及数据库 DDL、批量删除、批量更新、外部调用重试策略等危险动作，必须走隔离器（开关 + dry-run + 审计 + 回滚脚本）。
- 可使用 `var` 的地方优先使用 `var`（不降低可读性前提下）。
- 新增/删除文件后，必须更新 README 的文件树与逐文件职责，并新增“本次更新内容 / 后续可完善点”。
- 若需求不明确，先提出“待确认项”；未确认项不得默认实现。
