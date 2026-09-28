# 历史参考图资产来源

界面图标必须使用矢量图：通用图标由 `src/components/VectorIcon.tsx` 使用 Ant Design SVG 图标实现，品牌标识由 `src/components/BrandMark.tsx` 使用 SVG 路径和文字实现。禁止以 PNG、JPEG、WebP 等位图或在 SVG 中嵌入位图实现图标。

下列 PNG 是以前从参考图裁取的历史资产，保留用于设计对照，当前页面不再引用它们作为图标或品牌字标。以下内容记录其历史来源。

| 文件 | 来源 | 原图裁剪区域（左、上、右、下） | 像素尺寸 |
| --- | --- | --- | --- |
| hub-brand-mark.png | design/ant-design-blue-white/P01-workbench.png | 26, 15, 49, 41 | 23×26 |
| hub-brand-mark-login.png | design/ant-design-blue-white/P15-login.png | 552, 302, 595, 351 | 43×49 |
| hub-brand-wordmark-login.png | design/ant-design-blue-white/P15-login.png | 552, 302, 856, 351 | 304×49 |

可通过 `qa/extract_brand_assets.py` 重现提取过程。原图中的白色内孔和边缘像素保留在 PNG 中，适用于应用页头和登录卡片的白色背景。

登录页使用包含标志和英文名称的完整品牌字标，以保持其字形及间距；页面文字、表单和按钮继续由 HTML 与 Ant Design 渲染。

导航图标、账号头像和帮助页图标同样从选定的参考图中直接导出，使用 Ant Design Menu、Avatar 及原图 PNG 显示。导出过程见 `qa/extract_navigation_assets.py`，其中记录了每个裁剪区域：

- `nav-parcels.png`、`nav-governance.png`、`nav-observability.png`、`nav-guide.png`：P12 的实际导航图标，20×20 或 21×21。
- `nav-home.png`、`nav-home-active.png`：P12 的中性工作台图标和 P01 的选中图标，20×20。
- `nav-operations.png`、`nav-analytics.png`：P16 的运行态势图标和 P18 的分析报表图标，20×20。
- `nav-parcels-neutral.png`、`nav-rules.png`、`nav-system.png`：P17 的中性包裹图标、蓝色规则图标和系统图标，20×20 或 21×21。
- `nav-system-access.png`：P15b 的蓝色系统管理齿轮，20×20。系统配置页使用文档图标，与 P21 参考一致。
- `nav-backup.png`：P19 的备份恢复图标，22×22。
- `partition-calendar-*.png`：P20 的规划标题及月份横幅中的实际日历图标，24×24、30×30。
- `account-avatar.png`：P12 右上角的实际头像，32×32。
- `help-*.png`：P14 功能概览的九个实际图标及圆形背景，56×56。
- `analytics-parcels.png`、`analytics-errors.png`、`analytics-time.png`：P18 三个指标卡片的实际图标及背景，50×50。

这些文件保留来源颜色和抗锯齿边缘，不重新绘制图形。

应用页头的完整品牌字标也直接取自各张参考图：`header-brand-P01.png` 至 `header-brand-P21.png`（含 P15b，P15 登录页使用独立字标）。只裁取品牌图形及英文名称，不裁取页面或控件。裁剪区域、原始位置和自然尺寸记录在 `header-brand-assets.json`，可用 `qa/extract_header_brand_assets.py` 重现；展开导航时按原始尺寸显示，收起导航时保留品牌图标。

页头头像使用各页原图的实际变体：`header-account-P01.png` 至 `header-account-P21.png`（含 P15b）。P18 的蓝色头像与其他页的浅蓝色头像分别保留，使用原生 Ant Design Avatar 按自然尺寸显示。来源、裁剪和尺寸记录在 `header-account-assets.json`，提取脚本为 `qa/extract_header_account_assets.py`。

头像清单还记录逐页账号间距和纵向偏移。Avatar 关闭默认边框与二次圆形裁切，保留原图完整边缘；账号菜单仍为可点击的原生 Button/Dropdown。P14 的提取范围包含完整左侧边缘。
