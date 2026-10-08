import { useState } from 'react';
import { Badge, Button, Empty, Spin, Switch, Tabs, Tag } from 'antd';
import { CheckCircleOutlined, CloudServerOutlined, DatabaseOutlined, ReloadOutlined, SafetyCertificateOutlined } from '@ant-design/icons';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { formatNumber } from '../../data/formatNumber';
import { useApiResource } from '../../data/api/useApiResource';
import { readHealthReport } from '../../data/api/client';
import { localTime, type HealthReport } from '../../data/api/operationalTypes';
import './health.css';

/** 三类探针共用状态卡片与明细入口，检查范围保持后端定义。 */
const probes = [
  { path: '/health/live', title: '应用存活', description: '检查应用进程是否正常运行', icon: CloudServerOutlined },
  { path: '/health/ready', title: '服务就绪', description: '检查数据库和业务关键依赖', icon: DatabaseOutlined },
  { path: '/health/deep', title: '深度检查', description: '检查扩展依赖及后台治理状态', icon: SafetyCertificateOutlined },
] as const;

/** 中文名称辅助阅读，原始检查项编码仍完整展示。 */
const checkNames: Record<string, string> = {
  database: '数据库连接', 'parcel-buffered-write': '包裹缓冲写入',
  'baseline-data': '基线数据', outbox: 'Outbox 消息队列', backup: '备份治理',
  'read-only-database': '只读数据库', 'data-retention': '数据保留治理',
  'migration-governance': '数据库迁移', 'sharding-governance': '分表治理',
  'runtime-resources': '运行资源',
};

/** 状态外观与关注优先级共用同一映射，未知状态不视为正常。 */
function healthState(status?: string) {
  if (status === 'Healthy') return { label: '正常', badge: 'success', priority: 3 } as const;
  if (status === 'Degraded') return { label: '降级', badge: 'warning', priority: 1 } as const;
  if (status === 'Unhealthy') return { label: '异常', badge: 'error', priority: 0 } as const;
  return { label: '未知', badge: 'default', priority: 2 } as const;
}

/** 状态同时提供颜色和文字提示，避免仅依赖颜色辨识。 */
function HealthStatus({ status, label }: { status?: string; label?: string }) {
  const state = healthState(status);
  return <Tag className={`health-state health-state-${state.badge}`}><Badge status={state.badge} />{label ?? state.label}</Tag>;
}

/** 卡片可通过鼠标或键盘切换对应检查明细。 */
function HealthProbe({ probe, resource, selected, onSelect }: {
  probe: typeof probes[number]; resource: ReturnType<typeof useApiResource<HealthReport>>;
  selected: boolean; onSelect: () => void;
}) {
  const report = resource.data;
  const entries = Object.values(report?.entries ?? {});
  const healthy = entries.filter(entry => entry.status === 'Healthy').length;
  const status = resource.error ? undefined : report?.status;
  const state = healthState(status);
  const statusLabel = resource.error ? '检查失败' : resource.loading && !report ? '检查中' : state.label;
  const Icon = probe.icon;
  return <button type="button" className={`health-probe-card${selected ? ' is-selected' : ''}`} aria-pressed={selected} aria-label={`${probe.title}，${statusLabel}，查看检查明细`} onClick={onSelect}>
    <div className="health-probe-heading">
      <span className="health-probe-icon"><Icon /></span>
      <div><h2>{probe.title}</h2><code>{probe.path}</code></div>
      {resource.loading && !report ? <Spin size="small" /> : <HealthStatus status={status} label={statusLabel} />}
    </div>
    <p className={`health-probe-description${resource.error ? ' has-error' : ''}`}>{resource.error ? '检查请求失败，点击卡片查看原因并重试。' : probe.description}</p>
    <div className="health-probe-metrics">
      {probe.path === '/health/live' ? <>
        <div><span>进程状态</span><strong className={`health-process-${state.badge}`}>{resource.loading && !report ? '检查中' : resource.error ? '检查失败' : report ? state.label === '正常' ? '运行正常' : state.label : '等待检查'}</strong></div>
        <div><span>检查范围</span><strong className="health-probe-scope">应用进程</strong></div>
      </> : <>
        <div><span>正常检查项</span><strong>{report ? formatNumber(healthy) : '—'}<small> / {report ? formatNumber(entries.length) : '—'} 项</small></strong></div>
        <div><span>需关注</span><strong className={report && entries.length > healthy ? `health-attention-number health-attention-${state.badge}` : ''}>{report ? formatNumber(entries.length - healthy) : '—'}<small> 项</small></strong></div>
      </>}
    </div>
    <div className="health-probe-footer"><span>{resource.error && report ? '上次成功检查' : resource.loading && report ? '更新中 · 上次检查' : '检查时间'}</span><time>{report?.generatedAt ? localTime(report.generatedAt) : resource.error ? '未获取检查结果' : '尚未完成'}</time></div>
  </button>;
}

/** 并行读取三类真实探针；明细切换和筛选只在当前快照内进行。 */
export function HealthPage() {
  const live = useApiResource<HealthReport>('/health/live', readHealthReport);
  const ready = useApiResource<HealthReport>('/health/ready', readHealthReport);
  const deep = useApiResource<HealthReport>('/health/deep', readHealthReport);
  const resources = [live, ready, deep];
  const [selectedPath, setSelectedPath] = useState<string>('/health/deep');
  const [onlyAttention, setOnlyAttention] = useState(false);
  const selectedIndex = probes.findIndex(probe => probe.path === selectedPath);
  const selected = resources[selectedIndex];
  const report = selected.data;
  const entries = Object.entries(report?.entries ?? {}).map(([key, value]) => ({ key, ...value }));
  const attentionCount = entries.filter(entry => entry.status !== 'Healthy').length;
  const visibleEntries = entries.filter(entry => !onlyAttention || entry.status !== 'Healthy')
    .sort((left, right) => healthState(left.status).priority - healthState(right.status).priority);
  const loading = resources.some(resource => resource.loading);

  /** 手动刷新复用现有请求和实时订阅，不增加轮询。 */
  const refresh = () => resources.forEach(resource => resource.refresh());

  return <div className="health-diagnostics">
    <PageIntro title="健康检查" description="查看应用与依赖服务的运行状态，及时定位需要关注的检查项。" action={<Button type="primary" icon={<ReloadOutlined spin={loading} />} loading={loading} onClick={refresh}>刷新检查</Button>} />
    <div className="health-probe-grid">{probes.map((probe, index) => <HealthProbe key={probe.path} probe={probe} resource={resources[index]} selected={selectedPath === probe.path} onSelect={() => setSelectedPath(probe.path)} />)}</div>
    <div className="health-legend"><span>状态说明</span>{['Healthy', 'Degraded', 'Unhealthy', 'Unknown'].map(status => <span key={status}><Badge status={healthState(status).badge} />{healthState(status).label}<small>{status}</small></span>)}</div>
    <SectionCard className="health-details-card" title="检查明细" extra={<span className="health-details-count">{report ? `共 ${formatNumber(entries.length)} 项检查` : selected.error ? '检查请求失败' : '等待检查结果'}</span>}>
      <div className="health-details-toolbar">
        <Tabs activeKey={selectedPath} onChange={setSelectedPath} items={probes.map(probe => ({ key: probe.path, label: probe.title }))} />
        <label className="health-attention-filter"><Switch size="small" checked={onlyAttention} disabled={!entries.length} onChange={setOnlyAttention} aria-label="仅看需关注检查项" /><span>仅看需关注</span></label>
      </div>
      <ApiFeedback error={selected.error} retry={selected.refresh} />
      {selected.loading && !report ? <div className="health-detail-placeholder" role="status"><Spin /><span>正在读取检查结果</span></div> : report && <>
        <div className={`health-details-note${attentionCount ? ' has-attention' : ''} health-details-note-${healthState(selected.error ? undefined : report.status).badge}`}>
          <Badge status={selected.error ? 'default' : healthState(report.status).badge} />
          <span>{selected.error ? '当前请求失败，下方保留上次成功检查的结果。' : attentionCount ? `${formatNumber(attentionCount)} 项检查需要关注，已优先展示。` : selectedPath === '/health/live' ? '应用存活检查仅判断进程状态，不检查外部依赖。' : report.status !== 'Healthy' ? `检查结果为${healthState(report.status).label}，请结合服务诊断确认运行状态。` : entries.length ? '当前检查项均正常。' : '此探针未返回检查项。'}</span>
          <span className="health-details-time">{selected.loading ? '更新中 · ' : ''}{localTime(report.generatedAt)}</span>
        </div>
        {selectedPath === '/health/live' && !entries.length ? <div className={`health-process-result health-process-${healthState(report.status).badge}`}><CloudServerOutlined /><div><strong>{healthState(report.status).label === '正常' ? '应用进程运行正常' : `应用进程状态：${healthState(report.status).label}`}</strong><p>存活探针独立于数据库和其他外部依赖。</p></div></div> : <DataTable className="health-items-table" rowKey="key" tableLayout="fixed" scroll={{ x: '100%' }} pagination={false} loading={selected.loading} dataSource={visibleEntries} rowClassName={entry => entry.status === 'Healthy' ? '' : `health-item-attention health-item-${healthState(entry.status).badge}`} locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={onlyAttention && entries.length ? '当前没有需要关注的检查项' : '暂无检查项'} /> }} columns={[
          { title: '检查项', dataIndex: 'key', width: '26%', render: (key: string) => <div className="health-item-name"><strong>{checkNames[key] ?? key}</strong>{checkNames[key] && <code>{key}</code>}</div> },
          { title: '状态', dataIndex: 'status', width: '13%', render: (status: string) => <HealthStatus status={status} />, onCell: () => ({ className: 'health-item-status', 'data-label': '状态' }) },
          { title: '响应时间', dataIndex: 'durationMs', width: '13%', render: (value?: number) => <span className="health-item-duration">{value == null ? '—' : <>{formatNumber(value)}<small> ms</small></>}</span>, onCell: () => ({ className: 'health-item-response-time', 'data-label': '响应时间' }) },
          { title: '检查说明', dataIndex: 'description', render: (value?: string) => value || '未提供检查说明', onCell: () => ({ className: 'health-item-description', 'data-label': '检查说明' }) },
        ]} />}
      </>}
      {!report && !selected.loading && !selected.error && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚未获取检查结果，请刷新检查" />}
    </SectionCard>
    <div className="health-page-note"><CheckCircleOutlined />检查结果来自管理平台及其依赖服务，各探针按自身检查范围独立判断。</div>
  </div>;
}
