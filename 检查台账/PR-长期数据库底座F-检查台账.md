# PR-长期数据库底座 F 检查台账：数据库底座 CI 门禁增强

> **批次说明**：本台账对应《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》中的 PR-F 切片，先核对长期数据库底座当前完成度，再补齐数据库底座 CI 门禁工作流、规则脚本与交付断点。  
> **检查时间**：2026-05-05  
> **检查人**：Copilot

---

## 一、当前完成度核对

| 路线图项 | 当前状态 | 证据 |
|---|---|---|
| PR-A 数据库连接诊断与就绪状态增强 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`、`Zeye.Sorting.Hub.Host/HealthChecks/DatabaseConnectionDetailedHealthCheck.cs` |
| PR-B 查询保护与游标分页 | 已完成 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelCursorPagedQueryService.cs`、`Zeye.Sorting.Hub.Host/Routing/ParcelReadOnlyApiRouteExtensions.cs` |
| PR-C 批量写入缓冲与死信隔离 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/`、`Zeye.Sorting.Hub.Host/HealthChecks/BufferedWriteQueueHealthCheck.cs` |
| PR-D 分表巡检、预建与索引检查 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ShardingTableInspectionService.cs`、`Zeye.Sorting.Hub.Host/HealthChecks/ShardingGovernanceHealthCheck.cs` |
| PR-E 数据归档与冷热分层 | 已完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Archiving/`、`Zeye.Sorting.Hub.Host/Routing/DataGovernanceApiRouteExtensions.cs` |
| PR-F 数据库底座 CI 门禁增强 | 本次已完成 | `.github/workflows/database-foundation-gates.yml`、`.github/scripts/validate-database-foundation-rules.sh`、`数据库底座门禁说明.md` |
| PR-I 慢查询指纹聚合与查询画像 | 部分完成 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/` |

---

## 二、本次新增与修改文件

### 新增文件
- `.github/workflows/database-foundation-gates.yml`
- `.github/scripts/validate-database-foundation-rules.sh`
- `.github/scripts/validate-no-utc.sh`
- `.github/scripts/validate-readme-file-tree.sh`
- `.github/scripts/validate-sensitive-config.sh`
- `.github/scripts/validate-no-shadow-code.sh`
- `数据库底座门禁说明.md`
- `检查台账/PR-长期数据库底座F-检查台账.md`

### 修改文件
- `README.md`
- `更新记录.md`
- `检查台账/文件清单基线.txt`

### 删除文件
- 无

---

## 三、本次实现结果

1. 先核对长期数据库底座路线图，确认仓库已完成 PR-A ~ PR-E，PR-F 仅由 `stability-gates.yml` / `copilot-instructions-validation.yml` 局部覆盖，尚缺独立数据库底座门禁工作流与脚本。
2. 新增 `database-foundation-gates.yml`，固定执行 `restore + Release build + Release test`，并将 UTC、README 对账、敏感配置、影分身代码与结构性底座规则拆分为独立门禁步骤。
3. 新增 `validate-database-foundation-rules.sh` 统一承载数据库底座规则校验，并通过四个薄包装脚本复用同一套实现，避免脚本层影分身代码。
4. 新增 `数据库底座门禁说明.md`，明确本地执行命令、增量扫描边界与下一阶段演进入口。
5. 同步更新 README、更新记录与文件清单基线，补齐 PR-F 台账断点，保证后续 PR 可直接从 PR-G 接续。

---

## 四、验证记录

- `dotnet build Zeye.Sorting.Hub.sln -v quiet` ✅
- `dotnet test Zeye.Sorting.Hub.sln --no-build -v quiet` ✅
- `bash .github/scripts/validate-no-utc.sh` ✅
- `bash .github/scripts/validate-readme-file-tree.sh` ✅
- `bash .github/scripts/validate-sensitive-config.sh` ✅
- `bash .github/scripts/validate-no-shadow-code.sh` ✅
- `bash .github/scripts/validate-database-foundation-rules.sh --check advanced` ✅

---

## 五、PR-F 断点摘要

### 已完成
- 数据库底座 CI 门禁工作流
- UTC / 配置时区后缀增量拦截
- README 文件树与职责说明增量对账
- 敏感配置与影分身代码增量拦截
- 枚举 Description、HostedService 异常、后台循环取消、有界容量结构性检查

### 保留能力
- 既有 `stability-gates.yml` 与 `copilot-instructions-validation.yml` 保持兼容
- 当前数据库底座门禁以增量扫描为主，不强制一次性清理历史遗留问题
- 所有新增门禁继续遵守本地时间语义，不引入 UTC 转换链路

### 未完成但已预留
- PR-G 数据库迁移治理与回滚资产
- 后续可将敏感配置与 README 对账演进为更强语义级校验

### 下一 PR 入口
- 下一 PR 从 PR-G“数据库迁移治理与回滚资产”开始
- 后续迁移治理应复用当前数据库底座门禁工作流，避免新增并行重复校验链路
