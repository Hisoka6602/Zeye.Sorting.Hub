# Zeye Sorting Hub Web

仓库根目录下的独立前端项目，目录名 `Zeye.Sorting.Hub.Web` 与现有 .NET 项目保持一致，npm 包名为 `zeye-sorting-hub-web`。按 `design/ant-design-blue-white` 中的 22 张蓝白主题设计图实现。技术栈为 React 19、TypeScript、Vite 和 Ant Design 5。包裹台账、详情、新建检测、批量入队、过期清理和分析报表使用真实后端接口。其他业务页面仍使用演示数据。

## 启动

```powershell
cd D:\WorkSpace\Zeye\Zeye.Sorting.Hub\Zeye.Sorting.Hub.Web
npm ci
npm run dev
```

默认打开 `http://127.0.0.1:4173`。开发代理默认连接Host的 `http://127.0.0.1:5078`；可在启动前设置 `$env:ZEYE_API_PROXY='http://127.0.0.1:5098'` 覆盖。生产环境使用同源反向代理或设置 `VITE_API_BASE_URL`。构建使用 `npm run build`，产物位于 `dist/`；客户端测试使用 `npm test`。

需要对照静态参考图时，可单独启动只读设计预览：

```powershell
npm run dev -- --mode design-preview --port 4186
```

打开 `http://127.0.0.1:4186`。P02、P03、P05、P18在此模式显示参考图的2026-09-25样本，P03路径为 `/parcels/2509250005`。台账筛选、详情链接、面包屑、报表筛选仍可操作；提交动作不会写入后端。普通 `npm run dev` 和 `npm run build` 继续读取真实API，不会用样本替换请求失败状态。设计预览仅供视觉验收，不能作为运营数据。

## 页面

| 设计图 | 页面 | 路径 |
| --- | --- | --- |
| P01 | 工作台 | `/overview` |
| P02 | 包裹台账 | `/parcels` |
| P03 | 包裹详情 | `/parcels/{真实包裹编号}` |
| P04 | 新建包裹 | `/parcels/new` |
| P05 | 批量入队 | `/parcels/batch` |
| P06 | 过期包裹清理 | `/governance/parcel-cleanup` |
| P07 | 请求审计 | `/audit/requests` |
| P08 | 审计详情 | `/audit/requests/1001` |
| P09 | 慢查询画像 | `/diagnostics/slow-queries` |
| P10 | 慢查询详情 | `/diagnostics/slow-queries/8a3f2c1d` |
| P11 | 归档任务 | `/governance/archive-tasks` |
| P12 | Outbox 消息 | `/governance/outbox` |
| P13 | 健康检查 | `/diagnostics/health` |
| P14 | 操作指南 | `/help` |
| P15 | 登录 | `/access/login` |
| P15b | 账号与权限 | `/access` |
| P16 | 实时运行态势 | `/operations/live` |
| P17 | 规则管理 | `/rules` |
| P18 | 分析报表 | `/analytics` |
| P19 | 备份与恢复 | `/governance/backup` |
| P20 | 分区管理 | `/governance/sharding` |
| P21 | 系统配置 | `/settings` |

全部后台页面从同一侧栏进入；登录页可从右上角账号菜单进入。P03使用真实包裹编号；P08、P10保留设计图的独立详情示例，列表中的其他记录也可以点击查看。

## 项目结构

- `src/app`：路由、统一菜单和面包屑、共用母版、Ant Design 主题。`Shell.tsx` 与 `shell.css` 统一管理母版。
- `src/components`：可在多个业务页面复用的控件。
- `src/features/<业务域>`：业务页面及仅供该业务域使用的组件；每个页面单独按路由加载。
- `src/data/api`：统一请求客户端及包裹接口合同。
- `src/data/mock`：按业务域拆分的演示数据。
- `src/data/stores`：按业务域拆分的本地持久化状态。
- `src/app/shell.css`：固定侧栏、顶部栏及面包屑的共用蓝白样式。
- `src/styles.css`：各业务页面内容样式。
- `qa/`：本地生成的截图、对比结果和候选快照，已加入 Git 忽略规则；需要时按设计验收流程重新生成。
- `design-qa.md`：逐页对照、修改历史、已验证交互与剩余差异。

新增页面时，将页面放入对应的 `src/features/<业务域>`，在 `src/app/App.tsx` 中配置路由；跨页面复用的展示控件放入 `src/components`。新页面共用 AppShell，只添加内容区；菜单与可点击面包屑统一在 navigation.tsx 配置，母版样式集中在 shell.css。接入后端时，可在对应业务域下增加 API 模块，避免把请求逻辑写入通用控件。

包裹列表、详情及写操作通过 `src/data/api` 请求真实后端，服务异常会明确显示错误，不回退演示数据。分析报表请求 `/api/parcels/analytics`，按首次入库日期展示来源包裹当前快照，并另列按事件发生日统计的失败尝试和未绑定DWS事实；最多查询31天。无有效生命周期样本时显示“—”，不伪造零秒平均值。新建包裹使用完整管理端新增合同；来源检测登记另有独立路由，其 `RecordId` 由来源实例、运行批次和来源包裹号确定性生成，重载页面后重试仍使用同一身份。批量导入使用完整 `ParcelCreateRequest` 合同，JSON及简单CSV均保留64位编号；入队响应不代表落库完成。清理结果完全由服务端隔离器决定。其他页面的演示操作在本地运行。演示数据存于浏览器 `localStorage` 的 `zeye-demo-*` 键中；清除这些键即可恢复初始数据。登录页是视觉与表单演示，不执行真实认证。

## 当前还原记录

21个参考后台页共用固定左侧菜单、顶部栏和蓝白Ant Design母版；额外的来源检测登记页也使用同一母版，22个后台路由已核对菜单一致，面包屑祖先项可点击回退。普通模式最近一次全站截图基线为 analytics-layout-01。设计预览模式让P02、P03、P05、P18呈现参考样本。P01状态卡微调后整图误差从7.5666降到7.3790；P02筛选控件位置校正后，预览整图误差从7.3970降到7.0309，真实错误状态从8.4437降到8.3787；P03宽屏内容区对齐后，预览整图误差从8.5350降到7.1324，真实错误状态从7.6625降到6.7829；P07筛选与表格对齐后，整图误差从7.2127降到7.0314；P09提示文字与表格对齐后，整图误差从7.0968降到6.8208；P14提示框对齐后，整图误差从7.3291降到7.0808；P20导航和提示框对齐后，整图误差从7.1076降到7.0212。P02、P03、P18当前真实接口仍返回502，22页尚未达到参考图100%逐细节还原。详见[设计验收](design-qa.md)、本地生成的全站基线记录及内容区排名。
