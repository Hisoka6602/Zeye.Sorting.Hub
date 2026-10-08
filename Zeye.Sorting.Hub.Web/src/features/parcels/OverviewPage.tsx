import { formatNumber } from '../../data/formatNumber';
import { Button, Empty, Input, Pagination, Segmented, Skeleton, Switch } from 'antd';
import { ApiOutlined, AppstoreOutlined, ArrowRightOutlined, CheckCircleOutlined, CloudServerOutlined, DatabaseOutlined, DesktopOutlined, QuestionCircleOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { useNavigate } from 'react-router';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { readHealthReport } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import { useAccessSession } from '../../data/api/useAccessSession';
import { canAccessRestrictedSections } from '../../app/sectionAccess';
import { getRealtimeState, subscribeRealtimeState } from '../../data/api/realtimeTransport';
import { archiveStatusLabels, localTime, type ArchiveTask, type HealthReport, type PagedResult } from '../../data/api/operationalTypes';
import type { ParcelList, ParcelSummary } from '../../data/api/parcelTypes';
import { mergeWorkstationSources, observeWorkstationSummaries, recentWorkstationPath, type ParcelWorkbenchSummary, type WorkstationObservation, type FusionSourcePresence } from './workbenchModel';
import './workbench.css';

const healthProbes = [
  { path: '/health/live', name: '应用存活', caption: '检查管理平台进程是否运行', icon: <CloudServerOutlined /> },
  { path: '/health/ready', name: '服务就绪', caption: '检查数据库等关键依赖', icon: <DatabaseOutlined /> },
  { path: '/health/deep', name: '深度诊断', caption: '检查后台治理与扩展依赖', icon: <ApiOutlined /> },
] as const;
const healthStates: Record<string, { label: string; tone: string }> = {
  Healthy: { label: '正常', tone: 'healthy' }, Degraded: { label: '降级', tone: 'degraded' }, Unhealthy: { label: '异常', tone: 'unhealthy' },
};
const parcelStatus = (status: number) => ['待分拣', '已完成', '分拣异常'][status] ?? '未知状态';
const emptyParcels: ParcelSummary[] = [];

function PlatformProbe({ probe, resource }: { probe: typeof healthProbes[number]; resource: ReturnType<typeof useApiResource<HealthReport>> }) {
  const state = healthStates[resource.data?.status ?? ''] ?? { label: '未知', tone: 'unknown' };
  const checks = Object.values(resource.data?.entries ?? {});
  const issues = checks.filter(check => check.status !== 'Healthy').length;
  return <article className={`workbench-probe tone-${state.tone}`} aria-label={`管理平台${probe.name}`}>
    <div className="workbench-probe-top"><span className="workbench-probe-icon" aria-hidden="true">{probe.icon}</span><span>{probe.name}</span>
      <span className={`workbench-probe-state tone-${state.tone}`}><i aria-hidden="true" />{resource.loading ? '检查中' : state.label}</span></div>
    <p>{probe.caption}</p>
    {resource.loading ? <Skeleton active title={false} paragraph={{ rows: 1 }} /> : resource.error
      ? <div className="workbench-probe-error" role="alert">{resource.error.message}<Button type="link" onClick={resource.refresh}>重试检查</Button></div>
      : <div className="workbench-probe-result">{resource.data ? checks.length ? `${checks.length} 项检查 · ${issues} 项异常 / 降级` : '进程存活探针不检查外部依赖' : '尚未读取检查结果'}</div>}
    <div className="workbench-probe-time">检查时间 <span>{resource.data ? localTime(resource.data.generatedAt) : '—'}</span></div>
  </article>;
}

function WorkstationCard({ station, onSelect }: { station: WorkstationObservation; onSelect: () => void }) {
  return <article className="workbench-station" aria-label={`${station.name}，${station.sourceInstanceId ?? '未提供实例编码'}`}>
    <div className="workbench-station-head"><span className="workbench-station-icon" aria-hidden="true"><DesktopOutlined /></span><div>
      <h3>{station.name}</h3><p title={station.sourceInstanceId ?? undefined}>{station.sourceInstanceId ? `实例 ${station.sourceInstanceId}` : '来源实例编码未提供'}</p>
    </div></div>
    <div className="workbench-station-presence">
      {station.presence ? <><StatusTag value={station.presence.isOnline ? '在线' : '离线'} tone={station.presence.isOnline ? 'green' : 'orange'} /><span>{station.presence.lineId}{station.presence.deviceCode ? ` · ${station.presence.deviceCode}` : ''}</span></>
        : <><QuestionCircleOutlined /><span>来源未登记，在线状态未知</span></>}
    </div>
    {station.presence && <div className="workbench-station-heartbeat">最近心跳 {station.presence.lastSeenAt ? localTime(station.presence.lastSeenAt) : '尚未连接'}
      <span>上次心跳：待确认事实 {formatNumber(station.presence.pendingFacts, { grouping: true })} · 待传图片 {formatNumber(station.presence.pendingImages, { grouping: true })}</span></div>}
    {station.presence && (station.presence.droppedUnacknowledgedFacts > 0 || station.presence.droppedUnacknowledgedImages > 0) &&
      <div className="workbench-station-loss" role="status">发送端累计舍弃 {formatNumber(station.presence.droppedUnacknowledgedFacts, { grouping: true })} 条未确认事实、
        {formatNumber(station.presence.droppedUnacknowledgedImages, { grouping: true })} 张未确认图片</div>}
    <dl className="workbench-station-counts"><div><dt>待分拣（票）</dt><dd>{formatNumber(station.pendingCount, { grouping: true })}</dd></div>
      <div><dt>已完成（票）</dt><dd>{formatNumber(station.completedCount, { grouping: true })}</dd></div><div className={station.exceptionCount > 0 ? 'has-exceptions' : ''}><dt>分拣异常（票）</dt><dd>{formatNumber(station.exceptionCount, { grouping: true })}</dd></div></dl>
    {station.otherCount > 0 && <div className="workbench-station-other">其他状态 {formatNumber(station.otherCount, { grouping: true })} 票</div>}
    <div className="workbench-station-foot"><div><span>窗口内最近入库</span><time dateTime={station.lastParcelAt?.replace(' ', 'T')}>{station.lastParcelAt ? localTime(station.lastParcelAt) : station.parcelCount === 0 ? '当前窗口暂无包裹' : '入库时间未提供'}</time></div>
      <Button type="link" icon={<ArrowRightOutlined />} iconPosition="end" onClick={onSelect} aria-label={`查看 ${station.name} ${station.sourceInstanceId ?? ''} 的包裹`}>查看包裹</Button></div>
  </article>;
}

export function OverviewPage() {
  const navigate = useNavigate();
  const session = useAccessSession();
  const [automaticRefresh, setAutomaticRefresh] = useState(true);
  const connection = useSyncExternalStore(subscribeRealtimeState, getRealtimeState, getRealtimeState);
  const canReadGovernance = canAccessRestrictedSections(session.data);
  const statistics = useApiResource<ParcelWorkbenchSummary>('/api/parcels/workbench', undefined, automaticRefresh);
  const sources = useApiResource<FusionSourcePresence[]>('/api/parcels/fusion/sources', undefined, automaticRefresh);
  const live = useApiResource<HealthReport>('/health/live', readHealthReport, automaticRefresh);
  const ready = useApiResource<HealthReport>('/health/ready', readHealthReport, automaticRefresh);
  const deep = useApiResource<HealthReport>(canReadGovernance ? '/health/deep' : null, readHealthReport, automaticRefresh);
  const archives = useApiResource<PagedResult<ArchiveTask>>(canReadGovernance ? '/api/data-governance/archive-tasks?pageNumber=1&pageSize=5' : null, undefined, automaticRefresh);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [selectedKey, setSelectedKey] = useState<string>();
  const recentRef = useRef<HTMLDivElement>(null);
  const observations = useMemo(() => mergeWorkstationSources(observeWorkstationSummaries(statistics.data), sources.data ?? []), [statistics.data, sources.data]);
  const keyword = search.trim().toLocaleLowerCase();
  const filteredStations = observations.workstations.filter(item => (!keyword || `${item.name} ${item.sourceInstanceId ?? ''}`.toLocaleLowerCase().includes(keyword)) && (filter !== 'exceptions' || item.exceptionCount > 0));
  const currentPage = Math.min(page, Math.max(1, Math.ceil(filteredStations.length / 6)));
  const selectedStation = observations.workstations.find(item => item.key === selectedKey);
  const parcels = useApiResource<ParcelList>(recentWorkstationPath(selectedStation), undefined, automaticRefresh);
  const recent = parcels.data?.items ?? emptyParcels;
  const resources = [statistics, parcels, sources, live, ready, deep, archives];
  const loading = resources.some(item => item.loading);
  const refresh = () => resources.forEach(item => item.refresh());

  return <div className="workbench-page">
    <PageIntro title="工作台" description="集中查看平台健康与多个分拣工作台的处理情况。" action={<div className="workbench-header-actions">
      <label className="workbench-auto-refresh"><Switch size="small" checked={automaticRefresh} onChange={setAutomaticRefresh} aria-label="实时更新" /><span>实时更新</span></label>
      <Button icon={<ReloadOutlined />} onClick={refresh} loading={loading}>刷新数据</Button>
    </div>} />
    <ApiFeedback error={session.error} retry={session.refresh} />
    <section aria-labelledby="platform-status-title" className="workbench-platform">
      <div className="workbench-section-heading"><div><h2 id="platform-status-title">管理平台状态</h2><p>Zeye.Sorting.Hub 服务及依赖健康</p></div>
        {canReadGovernance && <Button type="link" icon={<ArrowRightOutlined />} iconPosition="end" onClick={() => navigate('/diagnostics/health')}>查看诊断</Button>}</div>
      <div className={`workbench-probes ${canReadGovernance ? '' : 'is-basic'}`}>{healthProbes.map((probe, index) => (index < 2 || canReadGovernance) && <PlatformProbe key={probe.path} probe={probe} resource={[live, ready, deep][index]} />)}</div>
    </section>
    <SectionCard className="workbench-stations-card" title={<div className="workbench-section-title"><AppstoreOutlined /><div><h2>分拣工作台</h2><p>按来源实例查看处理情况，支持多个工作台同时工作</p></div></div>} extra={<span className="workbench-connection-state"><ApiOutlined />{connection === 'connected' ? '实时通道已连接' : connection === 'reconnecting' ? '实时通道重连中' : connection === 'connecting' ? '正在连接实时通道' : '实时通道未连接'}</span>}>
      <div className="workbench-source-note"><span><CheckCircleOutlined />处理情况来自已入库包裹，在线状态来自 Fusion 心跳</span><p>显示已登记工作台及最近出现的来源。包裹统计按扫码时间汇总滚动最近 24 小时内的全部记录；下方明细展示最新 200 票包裹。Fusion 在线表示服务心跳有效，设备连接状态需另行上报。</p></div>
      <ApiFeedback error={sources.error} retry={sources.refresh} />
      {statistics.data && <p className="workbench-unassigned-note">统计窗口 {localTime(statistics.data.windowStartLocal)} — {localTime(statistics.data.windowEndLocal)}</p>}
      <div className="workbench-summary"><div><span>观察到的工作台</span><strong>{statistics.data ? observations.workstations.length : '—'}</strong></div>
        <div><span>窗口内包裹</span><strong>{statistics.data ? formatNumber(statistics.data.parcelCount, { grouping: true }) : '—'}<small>票</small></strong></div>
        <div><span>有包裹异常的工作台</span><strong>{statistics.data ? observations.workstations.filter(item => item.exceptionCount > 0).length : '—'}</strong></div></div>
      <div className="workbench-station-filters"><Input allowClear prefix={<SearchOutlined />} value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} aria-label="搜索工作台名称或实例编码" placeholder="工作台名称 / 来源实例编码" />
        <Segmented aria-label="工作台处理筛选" value={filter} onChange={value => { setFilter(String(value)); setPage(1); }} options={[{ value: 'all', label: '全部工作台' }, { value: 'exceptions', label: '有包裹异常' }]} /></div>
      {statistics.loading && !statistics.data ? <Skeleton active paragraph={{ rows: 5 }} /> : statistics.error ? <ApiFeedback error={statistics.error} retry={statistics.refresh} />
        : filteredStations.length ? <><div className="workbench-station-grid">{filteredStations.slice((currentPage - 1) * 6, currentPage * 6).map(station => <WorkstationCard key={station.key} station={station} onSelect={() => { setSelectedKey(station.key); recentRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }); }} />)}</div>
          {filteredStations.length > 6 && <Pagination className="workbench-station-pagination" current={currentPage} total={filteredStations.length} pageSize={6} showSizeChanger={false} onChange={setPage} showTotal={total => `共 ${total} 个工作台`} />}</>
        : <div className="workbench-stations-empty"><Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={keyword || filter !== 'all' ? '没有匹配的工作台' : '暂无工作台处理记录'} />
          {keyword || filter !== 'all' ? <Button onClick={() => { setSearch(''); setFilter('all'); setPage(1); }}>清除筛选</Button> : <p>登记来源并接入 Fusion 后将显示工作台心跳及处理情况。</p>}</div>}
      {statistics.data && observations.unassignedCount > 0 && <p className="workbench-unassigned-note">另有 {observations.unassignedCount} 票包裹未提供工作台名称或来源实例编码，未计入工作台数量。</p>}
    </SectionCard>
    <div ref={recentRef} className="workbench-recent-anchor">
      <SectionCard title={<div className="workbench-section-title"><DesktopOutlined /><div><h2>最近包裹记录</h2><p>{selectedKey ? `${selectedStation?.name ?? '所选工作台'} · 最近 24 小时内最新 200 票包裹` : '最近 24 小时 · 最新 200 票包裹'}</p></div></div>} extra={<div className="workbench-table-actions">
        {selectedKey && <Button onClick={() => setSelectedKey(undefined)}>显示全部工作台</Button>}<Button type="link" icon={<ArrowRightOutlined />} iconPosition="end" onClick={() => navigate('/parcels')}>查看台账</Button></div>}>
        {parcels.error ? <ApiFeedback error={parcels.error} retry={parcels.refresh} /> : <DataTable<ParcelSummary> countUnit="票" loading={parcels.loading} rowKey="id" dataSource={recent} pagination={{ defaultPageSize: 8, showSizeChanger: false }} scroll={{ x: 940 }} columns={[
          { title: '入库时间', dataIndex: 'createdTime', width: 165, render: localTime },
          { title: '主条码 / 包裹 ID', dataIndex: 'barCodes', width: 180, render: (value: string, item) => <Button type="link" className="table-link" onClick={() => navigate(`/parcels/${item.id}`)}>{value || item.id}</Button> },
          { title: '工作台', dataIndex: 'workstationName', width: 130, render: (value: string) => value || '未提供' },
          { title: '来源实例', dataIndex: 'sourceInstanceId', width: 150, render: (value: string | null) => value || '未提供' },
          { title: '状态', dataIndex: 'status', width: 100, render: (value: number) => <StatusTag value={parcelStatus(value)} /> },
          { title: '目标 / 实际格口', width: 145, render: (_, item) => `${item.targetChuteCode ?? item.targetChuteId ?? '—'} / ${item.actualChuteCode ?? item.actualChuteId ?? '—'}` },
          { title: '重量', dataIndex: 'weight', width: 100, render: (value: number | null) => value == null ? '未提供' : `${formatNumber(value)} kg` },
        ]} locale={{ emptyText: selectedKey ? '该工作台在当前窗口暂无包裹' : '暂无包裹记录' }} />}
      </SectionCard>
    </div>
    {canReadGovernance && <div className="workbench-bottom">
      <SectionCard title={<div className="workbench-section-title"><h2>最近归档任务</h2></div>} extra={<Button type="link" onClick={() => navigate('/governance/archive-tasks')}>查看全部 <ArrowRightOutlined /></Button>}>
        {archives.error ? <ApiFeedback error={archives.error} retry={archives.refresh} /> : <DataTable<ArchiveTask> loading={archives.loading} dataSource={archives.data?.items ?? []} pagination={false} scroll={{ x: 510 }} columns={[
          { title: '创建时间', dataIndex: 'createdAt', width: 160, render: localTime }, { title: '任务 ID', dataIndex: 'id', width: 150 },
          { title: '状态', dataIndex: 'status', width: 90, render: (value: string) => <StatusTag value={archiveStatusLabels[value] ?? value} /> }, { title: '发起人', dataIndex: 'requestedBy', width: 110 },
        ]} locale={{ emptyText: '暂无归档任务' }} />}
      </SectionCard>
    </div>}
  </div>;
}
