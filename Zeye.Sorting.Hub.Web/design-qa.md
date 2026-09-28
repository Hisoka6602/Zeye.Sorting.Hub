# 页面设计验收

> 本文引用的截图和测量文件位于本地 qa/ 目录；该目录不随 Git 提交，需在本地重新生成后查看。

22页参考图100%逐细节还原尚未通过；目标继续保持 active。最近完成P20分区管理页的导航与提示框对齐，同时保留固定母版。

## 本轮 p20-panel-alignment-01

- P20面包屑改为参考图中的“包裹中心 / 分区管理”，前一级仍是可点击的 `/parcels` 链接。浏览器点击后确实进入包裹台账；两页侧栏菜单内容一致。
- 在1586×992参考视口下，将提示框图标右移3px、下移3px，标题右移3px、下移4px，说明右移3px、上移3px。关闭图标和两张内容卡片保持原位。试用日历位图与移动关闭图标均增加误差，已恢复原实现。
- 原尺寸截图仅经显示器ICC到sRGB转换。整页RGB MAE从7.1076降到7.0212，内容区从6.6272降到6.5648，提示框从9.4748降到8.7169；面包屑局部从19.2061降到14.5961。390、1440、1586、1600px视口没有页面横向溢出；`npm test` 10项和 `npm run build` 均通过。P20仍未逐像素一致。

证据：[局部和整页测量](qa/candidates/p20-panel-alignment/measurement.json)、[调整前并排图](qa/candidates/p20-panel-alignment/P20-before-compare.png)、[调整后并排图](qa/candidates/p20-panel-alignment/P20-final-compare.png)、[更新的内容区排名](qa/candidates/content-triage/ranking.json)。

## 本轮 p09-table-alignment-01

- 在1586×992参考视口下，将快照提示文字右移9px、下移1px，表格表体上移1px，分页控件下移2px；筛选卡、表头、外框及共用母版位置保持。
- 原尺寸截图经过显示器ICC到sRGB转换，不缩放或配准。整页RGB MAE从7.0968降到6.8208，内容区从6.6277降到6.2565；提示条从8.8196降到7.8910，表格从8.6350降到8.0121。
- 1440、1586、1600px视口没有页面横向溢出；点击首行“查看”可进入对应详情页，并可返回列表。`npm test` 10项通过，`npm run build` 通过。P09仍未逐像素一致。

证据：[误差与源码散列](qa/candidates/p09-table-alignment/measurement.json)、[调整前并排图](qa/candidates/p09-table-alignment/P09-before-compare.png)、[调整后并排图](qa/candidates/p09-table-alignment/P09-final-compare.png)、[更新的内容区排名](qa/candidates/content-triage/ranking.json)。

## 本轮 p07-panel-alignment-01

- 在1487×1058参考视口下，将筛选卡内容起点右移1px、日期范围控件下移2px、提示条到表格卡片的间距缩小1px；筛选卡、提示条与共用母版的外框原位保留。
- 原尺寸截图经过显示器ICC到sRGB转换，只裁掉右侧1px宽度差，不缩放或配准。整页RGB MAE从7.2127降到7.0314，内容区从6.8347降到6.5895；筛选卡从6.3591降到5.8218，表格从8.6688降到8.4158。
- 1440、1487、1600px视口没有页面横向溢出，日期和状态控件仍有间距；`npm test` 10项通过，`npm run build` 通过。P07仍有字体和色彩等差异，尚未逐像素一致。

证据：[误差与源码散列](qa/candidates/p07-panel-alignment/measurement.json)、[调整前并排图](qa/candidates/p07-panel-alignment/P07-before-compare.png)、[调整后并排图](qa/candidates/p07-panel-alignment/P07-final-compare.png)、[更新的内容区排名](qa/candidates/content-triage/ranking.json)。

## 本轮 p01-metric-alignment-01

- 按参考图微调第二、第三张状态卡的左内边距，描述文字从16px调至15px，检查时间从16px调至15px并下移2px；只改变P01状态卡，三张卡的外框和共用母版保持原位。
- 原尺寸截图经过显示器ICC到sRGB转换，只裁掉右侧1px宽度差，不缩放或配准。状态卡区域RGB MAE从8.9887降到7.5017；整页从7.5666降到7.3790，内容区从7.3915降到7.1307。指示点蓝绿颜色也按原图采样校正。
- 1440px和1600px桌面视口无页面横向溢出，卡片内容未溢出；390px视口仍沿用原16px描述文字且没有页面横向溢出。`npm test` 10项通过，`npm run build` 通过。仍存在文字字形与旧图不同的侧栏状态等差异。

证据：[误差与源码散列](qa/candidates/p01-metric-alignment/measurement.json)、[调整前并排图](qa/candidates/p01-metric-alignment/P01-before-compare.png)、[调整后并排图](qa/candidates/p01-metric-alignment/P01-final-compare.png)、[更新的内容区排名](qa/candidates/content-triage/ranking.json)。

## 本轮 p02-filter-grid-01

- 在1487×1058参考视口下，将台账筛选区设为条码200px、日期325px、状态218px，三个控件间按参考图留30px、30px，状态到操作区留25px。实测控件起点分别为x≈299、529、884，操作区x≈1127。
- 设计预览整页RGB MAE从7.3970降到7.0309，筛选卡片区域从7.3536降到6.4076。真实接口错误状态整页从8.4437降到8.3787，筛选卡片区域从7.3807降到6.6858。真实模式仍显示502错误，不伪造数据。
- 1440、1487、1600px视口实测没有控件重叠或横向溢出。`npm test` 10项通过，`npm run build` 通过。新截图经过显示器ICC到sRGB转换；最多裁掉右侧1px宽度差，不缩放或配准。

证据：[两种状态的误差与源码散列](qa/candidates/p02-filter-grid/measurement.json)、[预览对照](qa/candidates/p02-filter-grid/P02-preview-after-compare.png)、[真实状态对照](qa/candidates/p02-filter-grid/P02-normal-after-compare.png)、[更新的内容区排名](qa/candidates/content-triage/ranking.json)。

## 本轮 p14-alert-alignment-01

- 按原始1586×992参考图对照，P14提示框原先从y≈195开始，参考图从y≈189开始；将提示框上移6px，下面的“功能概览”和卡片保持原位置。
- 提示框局部RGB MAE从14.2586降到9.0184；整页从7.3291降到7.0808，内容区从7.0647降到6.7307。截图只做显示器ICC到sRGB转换，没有缩放或配准。
- `npm test` 10项通过，`npm run build` 通过。全页内容区误差排名现使用P14最新截图，P03使用此前的真实错误状态最新截图；其他页面仍沿用较早的 `qa/current` 基线。

证据：[误差与散列](qa/candidates/p14-alert-alignment/measurement.json)、[修改前对照](qa/candidates/p14-alert-alignment/P14-before-compare.png)、[修改后对照](qa/candidates/p14-alert-alignment/P14-after-compare.png)、[内容区误差排名](qa/candidates/content-triage/ranking.json)。

## 固定母版与导航回退复核

- 所有后台路由继续共用 `AppShell`。侧栏固定在视口左侧，宽246px；菜单展开状态保存在同一个母版组件中，页面切换后保持。
- 修正栏目首页的面包屑自指链接。栏目没有单独首页时，当前栏目名称显示为文本；真正的上级页面保留可点击链接，只有最后一级带 `aria-current="page"`。
- 浏览器实测“新建包裹 → 包裹台账 → 工作台”两次面包屑点击均完成跳转。包裹台账和工作台的侧栏位置均为x=0、宽246px。手动收起“数据治理”后切到“分析报表”，展开状态仍保持。
- `npm test` 10项通过；`npm run build` 通过。这次修正没有改动页面内容区或母版尺寸。

## 本轮 p03-detail-alignment-01

- 仅在视口宽度至少1500px时调整详情页内容区左右边距及上边距。1586px视口下，基本信息卡片现在从x=287、y≈215开始，宽约1274px，与参考图卡片边界对齐；固定246px侧栏、顶部栏和面包屑组件未改。
- 预览模式整图RGB MAE从8.5350降到7.1324；真实接口错误状态从7.6625降到6.7829。两种状态的主体区误差也下降。1487px视口仍使用原边距，页面没有横向溢出。
- 生产构建通过。原尺寸截图、sRGB副本、源码散列与浏览器DOM几何已保存。P03仍非逐像素一致，主要可见差异包括侧栏宽度、祖先面包屑数量及字形色彩。

证据：[两种状态的误差记录](qa/candidates/p03-detail-alignment/measurement.json)、[预览并排图](qa/candidates/p03-detail-alignment/P03-preview_after-compare.png)、[真实状态并排图](qa/candidates/p03-detail-alignment/P03-normal_after-compare.png)、[卡片与母版坐标](qa/candidates/p03-detail-alignment/geometry.json)、[源码散列](qa/candidates/p03-detail-alignment/source-proof.json)。

## 上轮 design-preview-01

- 显式用 `npm run dev -- --mode design-preview --port 4186` 启动。P02、P03、P05、P18显示原图的固定示例数据；普通模式继续读取真实API，生产构建中的请求客户端仍只有真实 `fetch` 分支。
- P02出现7条包裹记录，P03出现已完成详情与5条轨迹，P05出现5条预览记录，P18出现125,680件、0.56%、18.6秒及7天报表。P02条码筛选、台账到详情及详情面包屑回退实测通过；P05设计预览的提交按钮给出只读提示，没有向后端写入。
- 4张预览截图使用原参考图视口并单独等候绘制，原生截图只做显示器ICC至sRGB转换，不缩放、不配准。生产构建通过，9项客户端测试通过；四张真实模式回归截图与上一轮相比，P02/P03/P05逐像素相同，P18平均差0.0118 RGB单位。

| 页面 | 真实状态整图MAE | 样本预览整图MAE | 样本状态结果 |
| --- | ---: | ---: | --- |
| P02 台账 | 8.4410 | 7.3943 | 7条记录，主体区更接近原图 |
| P03 详情 | 7.6625 | 8.5350 | 数据与结构齐全；固定母版和原图在宽屏水平位置约差14px，整图误差上升 |
| P05 批量入队 | 6.6616 | 6.3952 | 5条预览记录，主体区更接近原图 |
| P18 分析报表 | 7.2006 | 6.4369 | KPI、分布和日报呈现，主体区更接近原图 |

证据：[四页原尺寸比较与指标](qa/candidates/design-preview/final/preview-comparison.json)、[P02对照](qa/candidates/design-preview/final/P02-parcel-list-compare.png)、[P03对照（调整前）](qa/candidates/design-preview/final/P03-parcel-detail-compare.png)、[P05对照](qa/candidates/design-preview/final/P05-parcel-batch-compare.png)、[P18对照](qa/candidates/design-preview/final/P18-analytics-compare.png)、[该轮源码散列](qa/candidates/design-preview/normal/source-proof.json)。更早的全22页截图仍在 `qa/current`，其源码快照属于 analytics-layout-01；上轮改变的四页已在真实模式重新截图核对。

## 固定母版和面包屑

21个参考后台页面共用246px固定左侧菜单、56px顶部栏和蓝白Ant Design母版；登录页独立。另含来源检测登记路由，合计22个后台路由在1487×1058视口接受DOM核对：22项菜单文字及顺序完全一致，侧栏均位于x=0、宽246px，顶部栏均从y=0开始。面包屑祖先项使用实际路由链接，当前页保持非链接。

实际点击验证了“新建包裹 → 包裹中心 → 包裹台账”、“审计详情 → 请求审计 → 审计列表”和“来源检测登记 → 工作台”；折叠侧栏后跨页跳转，72px折叠状态保持，说明导航切换没有重建母版。验收记录见[固定母版与面包屑路由核对](qa/candidates/shared-shell-audit/results.json)。

## 上轮 analytics-layout-01

- P18保留真实分析接口和完整处理事实。两张分布卡片移到参考图y≈482，按日汇总移到y≈792；主要文案和五列表头对齐参考图，扩展处理指标置于主报表下方。整图RGB MAE从8.8661降到7.2006。
- 原生浏览器重截22页，保留22张原生JPEG、22张带ICC的同尺寸sRGB图片、67份源码和3份字体快照。只有ICC色彩空间转换，没有配准或重采样。每页DOM稳定后分开取画面，并等待抽屉动画完成。
- 20项截图、源码、公共母版、点击导航及像素检查通过；生产构建通过。除P02加载帧修正外，其他20页无实质视觉回退。上轮218个文件已归档，散列复核通过。

## 尚未通过的差异

- [P1] P02、P03、P18依赖真实API；当前代理返回502。设计预览已有固定样本，真实状态仍无法呈现这些样本。
- [P1] P05真实导入页初始为空，只有显式设计预览显示5条参考样本。
- [P2] P02上轮截图残留表格加载遮罩；本轮捕获完整绘制的错误表格。该页源码未改，整图MAE从7.9830变为8.4410。
- [P2] 全部22页整图误差仍非零；字体、矢量图标轮廓、颜色和边框仍有差异。按最新要求保持统一母版，旧图中逐页不同的菜单展开状态不作为回退目标。
- [P2] P17原图同时显示第1、2页按钮，却写共10条和10条/页；当前功能正确的Ant Design分页只显示第1页。

## 五类检查

| 范围 | 本轮结果 |
| --- | --- |
| 字体与排版 | P18可见文案与表头更接近参考图，字形差异仍存在 |
| 间距与布局 | P18分布卡和日报卡到达参考图位置，21页共用母版保持 |
| 颜色与令牌 | 蓝白Ant Design令牌保留，真实错误状态使用错误色 |
| 图像与资源 | 参考图、图标资源和字体快照未改动 |
| 文案与内容 | P18真实接口与扩展指标保留，不使用伪造数据 |

## 全22页整图诊断

误差为原图坐标、共同像素范围内的RGB MAE，不是保真百分比。

| 页面 | 上轮MAE | 本轮MAE |
| --- | ---: | ---: |
| P01-workbench | 7.5718 | 7.5718 |
| P02-parcel-list | 7.9830 | 8.4410 |
| P03-parcel-detail | 7.6651 | 7.6625 |
| P04-parcel-create | 5.4905 | 5.4905 |
| P05-parcel-batch | 6.6426 | 6.6616 |
| P06-expired-cleanup | 6.4666 | 6.4666 |
| P07-request-audit-list | 7.2094 | 7.2094 |
| P08-request-audit-detail | 6.4498 | 6.4498 |
| P09-slow-query-list | 7.1010 | 7.1010 |
| P10-slow-query-detail | 5.7255 | 5.7255 |
| P11-archive-tasks | 5.9588 | 5.9588 |
| P12-outbox-messages | 6.8408 | 6.8408 |
| P13-health-check | 6.9637 | 6.9637 |
| P14-help | 7.3207 | 7.3207 |
| P15-login | 1.7356 | 1.7356 |
| P15b-users-roles | 7.0376 | 7.0375 |
| P16-live-operations | 7.3352 | 7.3352 |
| P17-rules | 6.9975 | 6.9975 |
| P18-analytics | 8.8661 | 7.2006 |
| P19-backup-recovery | 6.6574 | 6.6574 |
| P20-partition-management | 7.1120 | 7.1120 |
| P21-settings | 5.3915 | 5.3915 |

上轮证据：[记录索引](qa/current/evidence-index.json)、[22页检查](qa/current/analytics-layout-evidence-check.json)、[P18原尺寸并排图](qa/current/P18-analytics-full-compare.png)。该轮源码和截图一致的记录时间为2026-09-28T08:19:51.454Z；最新源码以本轮四页回归及散列记录为准。
