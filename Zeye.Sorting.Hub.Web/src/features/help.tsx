import { Anchor, Avatar, Collapse, Steps } from 'antd';
import { DownOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { Link } from 'react-router';
import { InfoAlert } from '../components/InfoAlert';
import { PageIntro } from '../components/PageIntro';
import { VectorIcon } from '../components/VectorIcon';
import { contentTypographyForPath } from '../app/contentTypography';

const topics = [
  { key: 'parcels', title: '包裹台账', subtitle: '查询包裹列表与详情', detail: '在「包裹中心 - 包裹台账」中查看系统内的包裹列表。支持按条码、扫码时间、状态等条件筛选。', steps: ['设置筛选条件', '点击「查询」', '在列表中查看结果'], fields: '条码可输入主条码或包裹 ID；扫码时间使用本地时间。重置会恢复默认查询条件。' },
  { key: 'detail', title: '包裹详情', subtitle: '查看处理轨迹', detail: '从包裹台账点击「查看」进入详情，查看基本信息、处理轨迹和相关记录。', steps: ['核对包裹 ID 与主条码', '阅读目标与实际格口、重量和尺寸', '按需查看分拣链路及邻近包裹'], fields: '尺寸单位为 mm，重量单位为 kg；详情显示当前服务端已保存的来源记录。' },
  { key: 'create', title: '新建包裹', subtitle: '创建单个包裹', detail: '在「包裹中心 - 新建包裹」中创建单个包裹。', steps: ['填写身份与条码', '填写分拣信息与本地时间', '填写重量并提交'], fields: '包裹 ID 由调用方提供且必须为正整数；重复 ID 会被拒绝。创建成功后进入详情页。' },
  { key: 'batch', title: '批量入队', subtitle: '批量创建包裹', detail: '通过粘贴 JSON 数组或导入本地 CSV 批量校验包裹数据。', steps: ['粘贴 JSON 或选择 CSV', '点击「校验数据」并检查错误行', '点击「提交入队」查看结果'], fields: '每批最多 1000 条。已入队不等于数据库持久化完成；批次内重复 ID 会在本地校验时提示，最终结果以服务端为准。' },
  { key: 'cleanup', title: '包裹清理', subtitle: '提供试运行预览（仅预览）', detail: '清理决策由服务端隔离器决定，返回 blocked、dry-run 或 execute 结果。', steps: ['选择创建时间边界', '阅读影响范围并确认', '提交清理请求并查看决策结果'], fields: '计划处理数和实际执行数必须分别阅读；提交前请确认当前连接环境。' },
  { key: 'audit', title: '审计日志', subtitle: '查询操作审计日志', detail: '在「可观测性 - 请求审计」中按时间、路径、状态码或 TraceId 查询。', steps: ['输入时间与追踪线索', '点击「查询」定位请求', '进入详情查看链路与异常'], fields: '默认查询最近 24 小时。敏感 Header、Body 和 cURL 只应向获得服务端授权的人员展示。' },
  { key: 'slow', title: '慢查询', subtitle: '查看系统慢查询记录', detail: '慢查询页面只展示当前进程内观测窗口的快照。', steps: ['按 SQL 指纹、耗时或错误筛选', '查看 P95/P99 与超时次数', '打开详情比较标准 SQL 与样例 SQL'], fields: '当前窗口无数据不代表数据库没有历史慢查询；刷新会更新快照时间。' },
  { key: 'archive', title: '归档任务', subtitle: '提供试运行预览（仅预览）', detail: '归档任务仅生成 dry-run 计划，不执行真实迁移或删除。', steps: ['点击「新建 dry-run 任务」', '选择任务类型并填写保留天数', '在列表或详情抽屉查看计划'], fields: '当前仅支持 WebRequestAuditLogHistory，保留天数范围 1～3650。失败任务可在详情中重试。' },
  { key: 'outbox', title: 'Outbox', subtitle: '查看 Outbox 消息', detail: '在「数据治理 - Outbox 消息」中查看状态、重试次数和失败摘要。', steps: ['选择状态并查询', '点击「查看」打开消息详情', '切换消息内容和失败信息'], fields: '追加消息会验证 JSON 格式；当前演示不会触发真实事件派发。' },
  { key: 'health', title: '系统健康', subtitle: '查看系统健康状态', detail: '健康检查区分存活、就绪和深度诊断。', steps: ['点击「刷新检查」', '查看三个探针的整体状态', '在就绪和深度检查中阅读依赖项'], fields: '/health/live 仅判断应用是否存活，不展示子检查项；探针结果不代表完整告警历史。' },
].map(topic => ({ ...topic, icon: topic.key === 'detail'
  ? <Avatar size={56} style={{ color: '#1677ff', backgroundColor: '#eaf3ff' }} icon={<VectorIcon name="parcels" surface="help" size={28} />} />
  : <img src={`/assets/help-${topic.key}.png`} width={56} height={56} alt="" /> }));

export function HelpPage() {
  const [open, setOpen] = useState<string[]>(['parcels']);
  const jump = (key: string) => { setOpen(keys => [...new Set([...keys, key])]); document.getElementById(`topic-${key}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' }); };
  return <div className="help-grid">
    <div>
      <PageIntro title="使用帮助" description="了解系统的主要功能和操作方法，快速上手 Zeye Sorting Hub。" />
      <InfoAlert message="本页介绍当前已实现的最小可用 API 与操作方式。所有示例基于系统当前功能，界面可能随版本迭代而更新。" />
      <h3 className="card-heading" id="overview" style={contentTypographyForPath('/help', 'section', '功能概览')}>功能概览</h3>
      <div className="help-tiles">{topics.filter(topic => ['parcels', 'create', 'batch', 'cleanup', 'audit', 'slow', 'archive', 'outbox', 'health'].includes(topic.key)).map(topic => <button className="help-tile" key={topic.key} onClick={() => jump(topic.key)}><span className="help-tile-icon">{topic.icon}</span><span><span className="help-tile-title" style={contentTypographyForPath('/help', 'section', topic.title)}>{topic.title}</span><span className="help-tile-caption" style={{ display: 'block' }}>{topic.subtitle}</span>{topic.key === 'audit' && <span className="help-tile-caption" style={{ display: 'block' }}>默认查询最近 24 小时</span>}</span></button>)}</div>
      <h3 className="card-heading" style={{ marginTop: 21, ...contentTypographyForPath('/help', 'section', '详细说明') }}>详细说明</h3>
      <Collapse className="help-collapse" expandIconPosition="end" expandIcon={() => <DownOutlined />} activeKey={open} onChange={keys => setOpen(keys.map(String))} items={topics.map((topic, index) => ({ key: topic.key, label: <span className="help-collapse-label" id={`topic-${topic.key}`}><b>{index + 1}. {topic.title}{topic.key === 'parcels' ? '（包裹列表）' : ''}</b><span className="help-collapse-summary">{topic.key === 'detail' ? '在包裹列表中，点击「查看」进入包裹详情页面，查看包裹的完整信息，包括主条码、状态、目标/实际格口、重量、关联任务等。' : topic.key === 'create' ? <>在「包裹中心 - 新建包裹」中创建单个包裹，填写 <span className="help-keyword">主条码、目标格口、工作台、重量</span> 等信息后提交。</> : topic.key === 'batch' ? '在「包裹中心 - 批量入队」中通过文件或批量输入的方式创建多个包裹，系统将依次校验并入队。' : topic.detail}</span></span>, children: <>
        <p>{topic.key === 'parcels' ? <>在「包裹中心 - 包裹台账」中查看系统内的包裹列表。支持按 <span className="help-keyword">条码</span>、<span className="help-keyword">扫码时间</span>、<span className="help-keyword">状态</span> 等条件筛选。</> : topic.detail}</p>{topic.key === 'create' && <p>若只有首次检测信息，可使用 <Link to="/parcels/detection/new">来源检测登记</Link>，待后续量测和落格结果到达后再补充。</p>}<div className="help-step-box"><b>操作步骤</b><Steps style={{ marginTop: 10 }} size="small" current={0} items={topic.steps.map((step, stepIndex) => ({ title: step, description: topic.key === 'parcels' ? ['输入条码、选择时间范围和状态', '查看匹配的包裹列表', '可进一步查看包裹详情'][stepIndex] : undefined }))} /></div>
      </> }))} />
      <h3 className="card-heading" id="faq" style={{ marginTop: 24 }}>常见问题</h3>
      <Collapse className="help-collapse" items={[{ key: 'demo', label: '页面中的操作会修改线上数据吗？', children: '包裹创建、来源检测登记和批量入队会请求所配置的服务端。提交前请确认当前连接环境与操作权限；清理结果由服务端隔离器决定。' }, { key: 'data', label: '刷新后如何保留演示数据？', children: '服务端记录可通过台账重新查询；其他演示页面的数据保留方式取决于具体功能和浏览器存储。' }]} />
    </div>
    <div className="help-toc"><b>本页目录</b><Anchor affix={false} getCurrentAnchor={current => current || '#overview'} items={[{ key: 'overview', href: '#overview', title: '功能概览' }, ...topics.map(topic => ({ key: topic.key, href: `#topic-${topic.key}`, title: topic.key === 'cleanup' ? '包裹清理（试运行）' : topic.key === 'archive' ? '归档任务（试运行）' : topic.key === 'outbox' ? 'Outbox 消息' : topic.title })), { key: 'faq', href: '#faq', title: '常见问题' }]} onClick={(event, link) => { event.preventDefault(); if (link.href.startsWith('#topic-')) jump(link.href.replace('#topic-', '')); else document.getElementById(link.href.slice(1))?.scrollIntoView({ behavior: 'smooth' }); }} /></div>
  </div>;
}
