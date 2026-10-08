import { Button, Empty, Segmented, Select, Skeleton, Tooltip } from 'antd';
import { ClockCircleOutlined, CloudServerOutlined, DatabaseOutlined, DesktopOutlined, InfoCircleOutlined, ReloadOutlined, WarningOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { readHealthReport } from '../../data/api/client';
import { localDateTimeFormat, localTime, type HealthReport } from '../../data/api/operationalTypes';
import { useApiResource } from '../../data/api/useApiResource';
import type { ParcelList, ParcelSummary } from '../../data/api/parcelTypes';
import { formatNumber } from '../../data/formatNumber';
import './liveOperations.css';

/** 所有状态统计限定于当前最近入库窗口，不表示设备或全站总量。 */
const emptyParcels: ParcelSummary[] = [];
/** 复用包裹合同中的状态顺序，保留未识别状态的反馈。 */
const statusName = (status: number) => ['待分拣', '已完成', '分拣异常'][status] ?? '未知';
/** 两种探针共用同一布局，描述区分进程存活和依赖就绪。 */
const probes = [
  { title: '应用存活', path: '/health/live', description: '管理平台进程的运行状态', healthyLabel: '运行正常', icon: <CloudServerOutlined /> },
  { title: '服务就绪', path: '/health/ready', description: '数据库等关键依赖的可用状态', healthyLabel: '服务可用', icon: <DatabaseOutlined /> },
] as const;

/** 使用真实检查结果；加载、降级和读取失败均不冒充健康状态。 */
function LiveProbeCard({ probe, resource }: { probe: typeof probes[number]; resource: ReturnType<typeof useApiResource<HealthReport>> }) {
  const initialLoading = resource.loading && !resource.data;
  const status = resource.error ? undefined : resource.data?.status;
  const healthy = status === 'Healthy';
  const label = resource.error ? '检查失败' : initialLoading ? '检查中' : healthy ? '正常' : status === 'Degraded' ? '降级' : status === 'Unhealthy' ? '异常' : '未知';
  const tone = resource.error || status === 'Unhealthy' ? 'red' : status === 'Degraded' ? 'orange' : healthy ? 'green' : 'blue';
  return <article className={`live-status-card live-probe-card live-tone-${tone}`} aria-label={`${probe.title}，${label}`}>
    <div className="live-status-heading"><h2>{probe.title}</h2><span className="live-status-icon" aria-hidden="true">{probe.icon}</span></div>
    <div className="live-probe-value">
      {initialLoading ? <Skeleton.Input active size="small" /> : <strong>{resource.error ? '检查失败' : healthy ? probe.healthyLabel : resource.data ? '需要关注' : '等待检查'}</strong>}
      <StatusTag value={label} tone={tone} />
    </div>
    <p className="live-status-description">{probe.description}</p>
    <div className="live-status-footer"><span>{resource.error && resource.data ? '上次成功检查' : resource.loading && resource.data ? '更新中 · 上次检查' : '最近检查'}</span><time title={resource.data ? localTime(resource.data.generatedAt) : undefined}>{resource.data ? localTime(resource.data.generatedAt) : '—'}</time></div>
  </article>;
}

/** 最近包裹和健康探针来自 Host；页面不会凭本地样本声称设备在线。 */
export function LiveOperationsPage() {
  const parcels = useApiResource<ParcelList>('/api/parcels?pageNumber=1&pageSize=50&includeTotalCount=true');
  const live = useApiResource<HealthReport>('/health/live', readHealthReport);
  const ready = useApiResource<HealthReport>('/health/ready', readHealthReport);
  const [workstation, setWorkstation] = useState<string>();
  const [status, setStatus] = useState<string>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [lastUpdated, setLastUpdated] = useState<string>();

  useEffect(() => {
    if (parcels.data && live.data && ready.data) setLastUpdated(dayjs().format(localDateTimeFormat));
  }, [parcels.data, live.data, ready.data]);

  const items = parcels.data?.items ?? emptyParcels;
  const workstations = useMemo(() => [...new Set(items.map(item => item.workstationName).filter(Boolean))].sort(), [items]);
  const filtered = useMemo(() => items.filter(item => (!workstation || item.workstationName === workstation) && (status === 'all' || String(item.status) === status)), [items, workstation, status]);
  const abnormalCount = items.filter(item => item.status === 2).length;
  const currentPage = Math.min(page, Math.max(1, Math.ceil(filtered.length / pageSize)));
  const loading = parcels.loading || live.loading || ready.loading;
  /** 手动刷新沿用现有实时订阅，不新增轮询或其他接口调用。 */
  const refresh = () => { parcels.refresh(); live.refresh(); ready.refresh(); };

  return <div className="live-operations-page">
    <PageIntro title="实时运行态势" description="实时查看平台健康与最近入库包裹的处理动态。" action={<div className="live-page-actions">
      <span className="live-update-time"><ClockCircleOutlined aria-hidden="true" />最后更新 <time>{lastUpdated ? localTime(lastUpdated) : '等待数据'}</time></span>
      <Button icon={<ReloadOutlined />} onClick={refresh} loading={loading}>立即刷新</Button>
    </div>} />
    {(parcels.error || live.error || ready.error) && <InfoAlert type="error" closable={false} message="运行数据读取失败" description={[parcels.error?.message, live.error?.message, ready.error?.message].filter(Boolean).join('；')} />}
    <div className="live-status-grid">
      {probes.map((probe, index) => <LiveProbeCard key={probe.path} probe={probe} resource={[live, ready][index]} />)}
      <article className="live-status-card live-exception-card" aria-label="最近入库窗口内的异常包裹">
        <div className="live-status-heading"><h2>窗口内异常包裹</h2><span className="live-status-icon" aria-hidden="true"><WarningOutlined /></span></div>
        <div className="live-exception-value">{parcels.loading && !parcels.data ? <Skeleton.Input active size="small" /> : <strong>{parcels.data ? formatNumber(abnormalCount) : '—'}</strong>}<span>票</span></div>
        <p className="live-status-description">最近入库包裹中，当前状态为分拣异常的票数</p>
        <div className="live-status-footer"><span>当前观察窗口</span><span>{parcels.data ? `最近 ${formatNumber(items.length)} 票包裹` : '等待包裹数据'}</span></div>
      </article>
    </div>
    <p className="live-source-note"><InfoCircleOutlined aria-hidden="true" /><span>服务状态来自平台健康探针；包裹动态仅展示最近 50 票入库记录，不作全站统计。</span></p>
    <SectionCard title={<div className="live-activity-heading"><h2>最新包裹动态</h2><span>{parcels.data ? `${formatNumber(items.length)} 票包裹 · ${formatNumber(workstations.length)} 个来源工作台` : '等待入库记录'}</span></div>} className="live-activity-card" extra={<Link className="live-ledger-link" to="/parcels">查看包裹台账 →</Link>}>
      <div className="live-activity-toolbar">
        <Segmented aria-label="包裹状态筛选" value={status} onChange={value => { setStatus(String(value)); setPage(1); }} options={[{ label: '全部状态', value: 'all' }, { label: '待分拣', value: '0' }, { label: '已完成', value: '1' }, { label: '分拣异常', value: '2' }]} />
        <div className="live-workstation-filter"><label htmlFor="live-workstation">来源工作台</label><Select id="live-workstation" aria-label="来源工作台" allowClear showSearch optionFilterProp="label" placeholder="全部工作台" value={workstation} onChange={value => { setWorkstation(value); setPage(1); }} options={workstations.map(value => ({ value, label: value }))} /></div>
      </div>
      <DataTable<ParcelSummary> rowKey="id" countUnit="票" loading={parcels.loading} dataSource={filtered} tableLayout="fixed" scroll={{ x: 1100 }} pagination={{ current: currentPage, pageSize, pageSizeOptions: ['10', '20', '50'], onChange: (nextPage, nextSize) => { setPage(nextPage); setPageSize(nextSize); } }} locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={items.length ? '当前筛选条件下暂无包裹' : '暂无入库包裹，收到包裹后将自动更新'} /> }} columns={[
        { title: '入库时间', dataIndex: 'createdTime', width: 215, render: (value: string) => <time className="live-parcel-time" dateTime={value} title={localTime(value)}>{localTime(value)}</time> },
        { title: '包裹条码', dataIndex: 'barCodes', width: 360, render: (value: string, item) => <Tooltip title={value || item.id}><Link className="live-barcode-link" to={`/parcels/${item.id}`}>{value || item.id}</Link></Tooltip> },
        { title: '来源工作台', dataIndex: 'workstationName', width: 230, render: (value: string) => <span className="live-workstation-cell"><DesktopOutlined aria-hidden="true" /><Tooltip title={value || '未提供'}><span>{value || '未提供'}</span></Tooltip></span> },
        { title: '状态', dataIndex: 'status', width: 120, render: (value: number) => <StatusTag value={statusName(value)} /> },
        { title: '目标格口', dataIndex: 'targetChuteCode', align: 'center', width: 100, render: (value: string | null) => <span className="live-chute-code">{value || '—'}</span> },
        { title: '实际格口', dataIndex: 'actualChuteCode', align: 'center', width: 100, render: (value: string | null) => <span className="live-chute-code">{value || '—'}</span> },
      ]} />
    </SectionCard>
  </div>;
}
