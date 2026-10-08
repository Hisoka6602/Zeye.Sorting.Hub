import { formatNumber } from '../../data/formatNumber';
import { App, Button, Collapse, DatePicker, Select, Skeleton, Space, Tooltip } from 'antd';
import { ClockCircleOutlined, InboxOutlined, InfoCircleOutlined, ReloadOutlined, WarningOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { useApiResource } from '../../data/api/useApiResource';
import { AnalyticsRanking, AnalyticsTrend } from './AnalyticsCharts';
import { analyticsPercent as percent, type Analytics, type AnalyticsDaily as Daily, type AnalyticsDistribution as Distribution } from './analyticsModel';
import { validAnalysisDate } from '../parcels/parcelAnalysisModel';
import './analytics.css';

/** 来自真实持久化快照与处理事实的有界运营报表。 */
export function AnalyticsPage({ parcelCenter = false }: { parcelCenter?: boolean } = {}) {
  const designPreview = import.meta.env.MODE === 'design-preview';
  const { message } = App.useApp();
  const [scopeParams, setScopeParams] = useSearchParams();
  const fromScope = scopeParams.get('fromDate'), toScope = scopeParams.get('toDate');
  const validScope = parcelCenter && fromScope !== null && toScope !== null && validAnalysisDate(fromScope) && validAnalysisDate(toScope)
    && Date.parse(toScope) >= Date.parse(fromScope) && Date.parse(toScope) - Date.parse(fromScope) <= 30 * 86400000;
  const initialRange: [Dayjs, Dayjs] = validScope ? [dayjs(fromScope), dayjs(toScope)] : designPreview
    ? [dayjs('2026-09-19'), dayjs('2026-09-25')]
    : [dayjs().subtract(6, 'day').startOf('day'), dayjs().startOf('day')];
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(initialRange);
  const [site, setSite] = useState('华东分拨中心');
  const [line, setLine] = useState('全部产线');
  const [appliedSite, setAppliedSite] = useState(site);
  const [appliedLine, setAppliedLine] = useState(line);
  const [applied, setApplied] = useState({ from: initialRange[0].format('YYYY-MM-DD'), to: initialRange[1].format('YYYY-MM-DD') });
  useEffect(() => {
    if (validScope && fromScope && toScope) { setRange([dayjs(fromScope), dayjs(toScope)]); setApplied({ from: fromScope, to: toScope }); }
  }, [validScope, fromScope, toScope]);
  const saveScope = (from: string, to: string) => { if (parcelCenter) setScopeParams({ fromDate: from, toDate: to }); };
  const analysisScope = new URLSearchParams({ fromDate: applied.from, toDate: applied.to });
  const path = useMemo(() => `/api/parcels/analytics?fromDate=${encodeURIComponent(applied.from)}&toDate=${encodeURIComponent(applied.to)}${designPreview ? `&site=${encodeURIComponent(appliedSite)}&line=${encodeURIComponent(appliedLine)}` : ''}`, [applied, appliedSite, appliedLine, designPreview]);
  const { data, loading, error, refresh } = useApiResource<Analytics>(path);
  const exceptionRate = data && data.detectedCount > 0 ? formatNumber(data.exceptionCount / data.detectedCount * 100) : null;
  const rangeEnd = designPreview ? dayjs('2026-09-25') : dayjs().startOf('day');

  const onQuickRange = (days: number) => {
    const next: [Dayjs, Dayjs] = [rangeEnd.subtract(days - 1, 'day'), rangeEnd];
    const from = next[0].format('YYYY-MM-DD'), to = next[1].format('YYYY-MM-DD');
    saveScope(from, to);
    setRange(next);
    if (from === applied.from && to === applied.to) refresh();
    else setApplied({ from, to });
  };

  const onSearch = () => {
    if (!range?.[0] || !range[1]) { message.warning('请选择完整日期范围'); return; }
    if (range[1].isBefore(range[0]) || range[1].startOf('day').diff(range[0].startOf('day'), 'day') > 30) {
      message.warning('日期范围最多为 31 天'); return;
    }
    const from = range[0].format('YYYY-MM-DD'), to = range[1].format('YYYY-MM-DD');
    saveScope(from, to);
    if (from === applied.from && to === applied.to && (!designPreview || site === appliedSite && line === appliedLine)) refresh();
    else setApplied({ from, to });
    if (designPreview) { setAppliedSite(site); setAppliedLine(line); }
  };
  const onReset = () => {
    const next: [Dayjs, Dayjs] = designPreview
      ? [dayjs('2026-09-19'), dayjs('2026-09-25')]
      : [dayjs().subtract(6, 'day').startOf('day'), dayjs().startOf('day')];
    setRange(next);
    const from = next[0].format('YYYY-MM-DD'), to = next[1].format('YYYY-MM-DD');
    saveScope(from, to);
    if (from === applied.from && to === applied.to && (!designPreview || appliedSite === '华东分拨中心' && appliedLine === '全部产线')) refresh();
    else setApplied({ from, to });
    if (designPreview) { setSite('华东分拨中心'); setLine('全部产线'); setAppliedSite('华东分拨中心'); setAppliedLine('全部产线'); }
  };

  return <div className="analytics-page analytics-dashboard" aria-busy={loading}>
    <PageIntro title={parcelCenter ? '包裹统计' : '分析报表'} planned={designPreview}
      description={parcelCenter ? '从已持久化的包裹和处理事实查看入库趋势、来源分布与处理质量，并进一步查看异常、耗时和格口分析。' : '基于分拣数据的运营概览，提供关键指标统计与趋势分析。'}
      action={parcelCenter && <Space wrap><Link to={`/parcels/exceptions?${analysisScope}`}><Button>异常分析</Button></Link><Link to={`/parcels/duration?${analysisScope}`}><Button>耗时分析</Button></Link><Link to={`/parcels/chutes?${analysisScope}`}><Button>格口分析</Button></Link></Space>} />
    {designPreview ? <InfoAlert message="统计口径与聚合接口待接入" />
      : error ? <InfoAlert type="error" closable={false} message={`报表读取失败：${error.message}`} />
      : <InfoAlert closable={false} message={loading ? '正在读取真实报表数据…' : data?.detectedCount || data?.processingEventCount ? '统计来自已持久化的包裹和处理事实' : '该时间范围暂无来源包裹或处理事实'}
          description="包裹指标按首次入库日归组，反映当前快照；失败尝试和未绑定 DWS 按事实发生日计数，使用独立总体。" />}
    <SectionCard className="filter-card analytics-filter"><div className="filter-grid analytics-filter-grid">
      {designPreview && <Field label="站点"><Select value={site} onChange={setSite} options={['华东分拨中心', '华南分拨中心', '全部站点'].map(value => ({ value, label: value }))} /></Field>}
      {designPreview && <Field label="产线"><Select value={line} onChange={setLine} options={['全部产线', '产线 1', '产线 2', '产线 3'].map(value => ({ value, label: value }))} /></Field>}
      <Field label="日期范围" wide><DatePicker.RangePicker separator="~" value={range} onChange={setRange} style={{ width: '100%' }} /></Field>
      <div className="analytics-range-shortcuts" aria-label="快捷日期范围">{[7, 14, 30].map(days => <Button key={days} type="text" className={applied.from === rangeEnd.subtract(days - 1, 'day').format('YYYY-MM-DD') && applied.to === rangeEnd.format('YYYY-MM-DD') ? 'selected' : ''} onClick={() => onQuickRange(days)}>近 {days} 天</Button>)}</div>
      <FilterActions onSearch={onSearch} onReset={onReset} extra={designPreview ? undefined : <Button icon={<ReloadOutlined />} onClick={refresh} loading={loading}>刷新</Button>} />
    </div></SectionCard>
    <div className="analytics-kpi-grid">
      <div className="analytics-kpi"><div className="analytics-kpi-heading"><span>入库票数 <Tooltip title="按首次入库日期统计，包裹状态反映当前快照"><InfoCircleOutlined className="analytics-kpi-help" /></Tooltip></span><div className="analytics-kpi-icon"><InboxOutlined /></div></div><div className="analytics-kpi-value">{data ? formatNumber(data.detectedCount, { grouping: true }) : '—'}<small>票</small></div><div className="analytics-kpi-caption"><span>首次入库的包裹票数</span>{data && <strong>当前完成 {formatNumber(data.completedCount, { grouping: true })} 票</strong>}</div></div>
      <div className="analytics-kpi analytics-kpi-exception"><div className="analytics-kpi-heading"><span>异常率 <Tooltip title="当前异常票数 / 所选日期范围内的入库票数"><InfoCircleOutlined className="analytics-kpi-help" /></Tooltip></span><div className="analytics-kpi-icon"><WarningOutlined /></div></div><div className="analytics-kpi-value">{exceptionRate ?? '—'}{exceptionRate !== null && <small>%</small>}</div><div className="analytics-kpi-caption"><span>当前异常票数 / 入库票数</span>{data && <strong>异常 {formatNumber(data.exceptionCount, { grouping: true })} 票</strong>}</div></div>
      <div className="analytics-kpi analytics-kpi-time"><div className="analytics-kpi-heading"><span>平均完成耗时 <Tooltip title="首次检测至完成的平均耗时，仅统计有效生命周期样本"><InfoCircleOutlined className="analytics-kpi-help" /></Tooltip></span><div className="analytics-kpi-icon"><ClockCircleOutlined /></div></div><div className="analytics-kpi-value">{data?.averageLifecycleSeconds == null ? '—' : formatNumber(data.averageLifecycleSeconds)}<small>秒</small></div><div className="analytics-kpi-caption"><span>有效样本的完成耗时平均</span></div></div>
    </div>
    <div className="analytics-chart-grid">
      <SectionCard className="analytics-trend-card" title={<div><div className="analytics-section-title">每日入库趋势</div><div className="analytics-section-subtitle">{applied.from} — {applied.to}</div></div>} extra={<div className="analytics-trend-legend"><span><i />入库票数</span><span className="exception"><i />当前异常</span></div>}>
        {loading ? <Skeleton active title={false} paragraph={{ rows: 7 }} /> : <AnalyticsTrend rows={data?.daily ?? []} unavailable={Boolean(error)} />}
        <div className="analytics-chart-note">按首次入库日归组；异常票数反映当前快照。精确数值见按日汇总。</div>
      </SectionCard>
      <SectionCard title={<div className="analytics-section-title">工作台分布</div>} extra={<span className="analytics-section-total">合计<strong>{data ? formatNumber(data.detectedCount, { grouping: true }) : '—'}</strong>票</span>}>
        {loading ? <Skeleton active title={false} paragraph={{ rows: 6 }} /> : <AnalyticsRanking rows={data?.workstations ?? []} total={data?.detectedCount ?? 0} kind="workstation" unavailable={Boolean(error)} />}
        {data && data.workstations.filter(row => row.count > 0).length > 5 && <div className="analytics-chart-note">展示票数最多的 5 项，完整分布见明细。</div>}
        {data?.workstationsTruncated && <div className="analytics-chart-note">工作台较多，当前仅返回部分分布。</div>}
        <Collapse className="analytics-details" bordered={false} items={[{ key: 'workstations', label: `查看工作台明细${data ? `（${data.workstations.length} 项）` : ''}`, children: <div className="analytics-detail-table"><DataTable<Distribution> rowKey={row => row.code ?? row.name} scroll={{ x: 420 }} loading={loading} locale={{ emptyText: error ? '报表暂不可用，请刷新重试' : '暂无来源包裹' }} dataSource={data?.workstations ?? []} columns={[{ title: '工作台', dataIndex: 'name', width: 180 }, { title: '票数', dataIndex: 'count', width: 110, render: (value: number) => formatNumber(value, { grouping: true }) }, { title: '占比', dataIndex: 'count', width: 100, render: (value: number, record) => record.designPreviewPercent ?? percent(value, data?.detectedCount ?? 0) }]} /></div> }]} />
      </SectionCard>
    </div>
    <div className="analytics-lower-grid">
      <SectionCard title={<div className="analytics-section-title">异常类型分布</div>} extra={<span className="analytics-section-total">异常<strong>{data ? formatNumber(data.exceptionCount, { grouping: true }) : '—'}</strong>票</span>}>
        {loading ? <Skeleton active title={false} paragraph={{ rows: 6 }} /> : <AnalyticsRanking rows={data?.exceptionTypes ?? []} total={data?.exceptionCount ?? 0} kind="exception" unavailable={Boolean(error)} />}
        {data && data.exceptionTypes.filter(row => row.count > 0).length > 5 && <div className="analytics-chart-note">展示票数最多的 5 项，完整分布见明细。</div>}
        <Collapse className="analytics-details" bordered={false} items={[{ key: 'exceptions', label: `查看异常明细${data ? `（${data.exceptionTypes.length} 项）` : ''}`, children: <div className="analytics-detail-table"><DataTable<Distribution> rowKey={row => row.code ?? row.name} scroll={{ x: 460 }} loading={loading} locale={{ emptyText: error ? '报表暂不可用，请刷新重试' : '暂无异常包裹' }} dataSource={data?.exceptionTypes ?? []} columns={[{ title: '异常类型', dataIndex: 'name', width: 230 }, { title: '票数', dataIndex: 'count', width: 110, render: (value: number) => formatNumber(value, { grouping: true }) }, { title: '占比', dataIndex: 'count', width: 100, render: (value: number, record) => record.designPreviewPercent ?? percent(value, data?.exceptionCount ?? 0) }]} /></div> }]} />
      </SectionCard>
      <SectionCard title={<div className="analytics-section-title">处理质量</div>}>
        <div className="analytics-quality-grid" aria-label="处理质量指标">
          <div className="analytics-quality-item"><div className="analytics-quality-label">NoRead 票数</div><div className="analytics-quality-value">{data ? formatNumber(data.noReadCount, { grouping: true }) : '—'}</div><div className="analytics-quality-caption">包裹快照 · 按首次入库日</div></div>
          <div className="analytics-quality-item"><div className="analytics-quality-label">格口不一致票数</div><div className="analytics-quality-value">{data ? formatNumber(data.chuteMismatchCount, { grouping: true }) : '—'}</div><div className="analytics-quality-caption">包裹快照 · 按首次入库日</div></div>
          <div className="analytics-quality-item"><div className="analytics-quality-label">失败尝试 / 处理事实</div><div className="analytics-quality-value">{data ? <>{formatNumber(data.failedAttemptCount, { grouping: true })} <small>/ {formatNumber(data.processingEventCount, { grouping: true })}</small></> : '—'}</div><div className="analytics-quality-caption">处理事实 · 按发生日</div></div>
          <div className="analytics-quality-item"><div className="analytics-quality-label">未绑定 DWS 事实</div><div className="analytics-quality-value">{data ? formatNumber(data.unboundDwsEventCount, { grouping: true }) : '—'}</div><div className="analytics-quality-caption">处理事实 · 按发生日</div></div>
        </div>
      </SectionCard>
    </div>
    <SectionCard className="analytics-detail-table" title={<div className="analytics-section-title">按日汇总</div>} extra={<span className="analytics-section-total">{data ? `${data.daily.length} 天` : '—'}</span>}>
      <DataTable<Daily> rowKey="date" tableLayout="fixed" scroll={{ x: 850 }} loading={loading} locale={{ emptyText: error ? '报表暂不可用，请刷新重试' : '该范围暂无来源包裹' }} dataSource={data?.daily ?? []} columns={[{ title: '日期', dataIndex: 'date', width: 170 }, { title: '入库票数', dataIndex: 'detectedCount', width: 150, render: (value: number) => <span className="analytics-detail-count">{formatNumber(value, { grouping: true })}</span> }, { title: '当前异常票数', dataIndex: 'exceptionCount', width: 160, render: (value: number) => formatNumber(value, { grouping: true }) }, { title: '异常率', width: 150, render: (_, record) => <span className={record.exceptionCount > 0 ? 'analytics-detail-rate' : 'analytics-metric-explanation'}>{percent(record.exceptionCount, record.detectedCount)}</span> }, { title: '平均完成耗时（秒）', dataIndex: 'averageLifecycleSeconds', width: 210, render: (value: number | null) => value == null ? '—' : formatNumber(value) }]} />
      <Collapse className="analytics-details" bordered={false} items={[{ key: 'extended', label: '每日扩展指标', children: <DataTable<Daily> rowKey="date" loading={loading} scroll={{ x: 850 }} dataSource={data?.daily ?? []} locale={{ emptyText: error ? '报表暂不可用，请刷新重试' : '该范围暂无来源包裹' }} columns={[{ title: '日期', dataIndex: 'date', width: 170 }, { title: '当前完成', dataIndex: 'completedCount', width: 150 }, { title: 'NoRead', dataIndex: 'noReadCount', width: 150 }, { title: '格口不一致', dataIndex: 'chuteMismatchCount', width: 150 }, { title: '有效耗时样本', dataIndex: 'lifecycleSampleCount', width: 170 }]} /> }]} />
    </SectionCard>
  </div>;
}
