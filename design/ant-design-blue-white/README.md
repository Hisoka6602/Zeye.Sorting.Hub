# Zeye Sorting Hub · Ant Design 蓝白主题 UI 图集

本图集包含 **22 张桌面端高保真页面图**，按 [前端页面设计方案](../../前端页面设计方案-AntDesign.md) 的 P01–P21 编号组织；P15 拆为登录与账号权限两张。设计使用 Ant Design 5 的蓝白主题：主蓝 `#1677FF`、白色内容面、浅灰页面底色。图中业务数据为演示内容。

布局和控件按 Ant Design 可实现的组件设计：`Layout`、`Menu`、`Breadcrumb`、`Card`、`Table`、`Pagination`、`Form`、`Input`、`Select`、`DatePicker`、`Descriptions`、`Timeline`、`Tabs`、`Drawer`、`Alert`、`Tag`、`Steps`、`Collapse` 等。分析图表可使用 Ant Design Charts。实际开发时仍需按接口合同核对字段与交互状态。

## 现有接口对应页面

P01 为现有接口可拼装的轻量概览；P06 过期清理和 P07–P08 审计展示需要按设计方案中的权限及接口启用条件开放。

### P01 工作台概览

![P01 工作台概览](P01-workbench.png)

### P02 包裹台账

![P02 包裹台账](P02-parcel-list.png)

### P03 包裹详情

![P03 包裹详情](P03-parcel-detail.png)

### P04 新建包裹

![P04 新建包裹](P04-parcel-create.png)

### P05 批量入队

![P05 批量入队](P05-parcel-batch.png)

### P06 过期包裹清理

![P06 过期包裹清理](P06-expired-cleanup.png)

### P07 请求审计

![P07 请求审计](P07-request-audit-list.png)

### P08 审计详情

![P08 审计详情](P08-request-audit-detail.png)

### P09 慢查询画像

![P09 慢查询画像](P09-slow-query-list.png)

### P10 慢查询详情

![P10 慢查询详情](P10-slow-query-detail.png)

### P11 归档任务

![P11 归档任务](P11-archive-tasks.png)

### P13 系统健康

![P13 系统健康](P13-health-check.png)

### P14 操作指南

![P14 操作指南](P14-help.png)

## 后续规划页面

以下 8 张是视觉规划稿，所需认证、实时、规则、分析和治理接口尚未全部落地，图片不代表功能已经可用。

### P15 登录

![P15 登录](P15-login.png)

### P15b 账号与权限

![P15b 账号与权限](P15b-users-roles.png)

### P16 实时运行态势

![P16 实时运行态势](P16-live-operations.png)

### P17 规则管理

![P17 规则管理](P17-rules.png)

### P18 分析报表

![P18 分析报表](P18-analytics.png)

### P19 备份与恢复

![P19 备份与恢复](P19-backup-recovery.png)

### P20 分区管理

![P20 分区管理](P20-partition-management.png)

### P21 系统配置

![P21 系统配置](P21-settings.png)

## 实现参考

Ant Design 官方文档：[布局](https://ant.design/components/layout/)、[表格](https://ant.design/components/table/)、[主题定制](https://ant.design/docs/react/customize-theme/)。本交付为 UI 图片和设计方案，未新增前端代码。
