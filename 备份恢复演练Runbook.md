# 备份恢复演练 Runbook

> 适用范围：备份失败、恢复演练、备份目录校验、季度演练归档  
> 关联实现：`BackupHostedService`、`BackupVerificationService`、`BackupHealthCheck`、`RestoreDrillPlanner`、`drill-records/2026-Q1-稳定性演练记录.md`

---

## 一、目标

1. 确认备份文件按计划生成且在允许年龄内。
2. 确认恢复演练有可追溯记录，不以“文件存在”代替“可恢复”。
3. 确认 Runbook、演练记录、备份健康检查三者一致。

---

## 二、日常检查

1. 检查 `BackupHealthCheck` 是否 Healthy。
2. 检查最新备份文件时间、本地时间窗口与文件后缀是否符合 Provider 约定。
3. 检查恢复 Runbook 路径与 `drill-records/` 演练记录路径是否存在。
4. 检查 NLog 中是否出现连续备份失败或校验失败日志。

---

## 三、备份失败处理

### 1. 检查项

1. 备份目录是否存在且可写。
2. 数据库命令生成是否正常。
3. 连接串读取是否完整。
4. 目标磁盘空间是否充足。
5. 最新备份文件是否超过 `MaxAllowedBackupAgeHours`。

### 2. 处置顺序

1. 先恢复备份计划生成能力。
2. 再恢复备份文件写入能力。
3. 最后执行恢复演练，确认备份可用。

---

## 四、恢复演练步骤

1. 使用 `RestoreDrillPlanner` 生成本次演练计划。
2. 记录演练开始时间、目标备份文件、演练环境、负责人。
3. 执行恢复流程并验证核心表、关键接口、健康探针是否恢复。
4. 将结果写入 `drill-records/`，并在需要时更新季度演练记录。

---

## 五、通过标准

1. `BackupHealthCheck` 恢复 Healthy。
2. 最新备份文件新鲜且命令生成正确。
3. 恢复演练记录已写入 `drill-records/`。
4. `业务接入前底座验收清单.md` 中“备份计划”“恢复演练记录”条目保持完成。
