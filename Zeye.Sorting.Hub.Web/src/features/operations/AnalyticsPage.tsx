import { App, Button, DatePicker, Select } from 'antd';
import { InfoCircleOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useMemo, useState } from 'react';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { VectorIcon } from '../../components/VectorIcon';
import { useApiResource } from '../../data/api/useApiResource';

/** 后端分别按首次入库日和事实发生日计算，两个总体不能混用。 */
interface Analytics {
  fromDate: string;
  toDate: string;
  detectedCount: number;
  completedCount: number;
  exceptionCount: number;
  noReadCount: number;
  chuteMismatchCount: number;
  averageLifecycleSeconds: number | null;
  daily: Daily[];
  exceptionTypes: Distribution[];
  workstations: Distribution[];
  workstationsTruncated: boolean;
  processingEventCount: number;
  failedAttemptCount: number;
  unboundDwsEventCount: number;
}

interface Daily {
  date: string;
  detectedCount: number;
  completedCount: number;
  exceptionCount: number;
  noReadCount: number;
  chuteMismatchCount: number;
  averageLifecycleSeconds: number | null;
  lifecycleSampleCount: number;
}

interface Distribution { code: string | null; name: string; count: number; designPreviewPercent?: string }

/** 无分母或无有效样本时显示未知，而不伪造零比例。 */
function percent(numerator: number, denominator: number): string {
  return denominator > 0 ? `${(numerator / denominator * 100).toFixed(2)}%` : '—';
}

/** 来自真实持久化快照与处理事实的有界运营报表。 */
export function AnalyticsPage() {
  const designPreview = import.meta.env.MODE === 'design-preview';
  const { message } = App.useApp();
  const initialRange: [Dayjs, Dayjs] = designPreview
    ? [dayjs('2026-09-19'), dayjs('2026-09-25')]
    : [dayjs().subtract(6, 'day').startOf('day'), dayjs().startOf('day')];
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(initialRange);
  const [site, setSite] = useState('华东分拨中心');
  const [line, setLine] = useState('全部产线');
  const [appliedSite, setAppliedSite] = useState(site);
  const [appliedLine, setAppliedLine] = useState(line);
  const [applied, setApplied] = useState({ from: initialRange[0].format('YYYY-MM-DD'), to: initialRange[1].format('YYYY-MM-DD') });
  const path = useMemo(() => `/api/parcels/analytics?fromDate=${encodeURIComponent(applied.from)}&toDate=${encodeURIComponent(applied.to)}${designPreview ? `&site=${encodeURIComponent(appliedSite)}&line=${encodeURIComponent(appliedLine)}` : ''}`, [applied, appliedSite, appliedLine, designPreview]);
  const { data, loading, error, refresh } = useApiResource<Analytics>(path);
  const exceptionRate = data && data.detectedCount > 0 ? (data.exceptionCount / data.detectedCount * 100).toFixed(2) : null;

  const onSearch = () => {
    if (!range?.[0] || !range[1]) { message.warning('请选择完整日期范围'); return; }
    if (range[1].isBefore(range[0]) || range[1].startOf('day').diff(range[0].startOf('day'), 'day') > 30) {
      message.warning('日期范围最多为 31 天'); return;
    }
    setApplied({ from: range[0].format('YYYY-MM-DD'), to: range[1].format('YYYY-MM-DD') });
    if (designPreview) { setAppliedSite(site); setAppliedLine(line); }
  };
  const onReset = () => {
    const next: [Dayjs, Dayjs] = designPreview
      ? [dayjs('2026-09-19'), dayjs('2026-09-25')]
      : [dayjs().subtract(6, 'day').startOf('day'), dayjs().startOf('day')];
    setRange(next);
    setApplied({ from: next[0].format('YYYY-MM-DD'), to: next[1].format('YYYY-MM-DD') });
    if (designPreview) { setSite('华东分拨中心'); setLine('全部产线'); setAppliedSite('华东分拨中心'); setAppliedLine('全部产线'); }
  };

  return <div className="analytics-page">
    <PageIntro title="分析报表" planned={designPreview} description="基于分拣数据的运营概览，提供关键指标统计与趋势分析。" />
    {designPreview ? <InfoAlert message="统计口径与聚合接口待接入" />
      : error ? <InfoAlert type="error" closable={false} message={`报表读取失败：${error.message}`} />
      : <InfoAlert closable={false} message={loading ? '正在读取真实报表数据…' : data?.detectedCount || data?.processingEventCount ? '统计来自已持久化的包裹和处理事实' : '该时间范围暂无来源包裹或处理事实'}
          description="包裹指标按首次入库日归组，反映当前快照；失败尝试和未绑定 DWS 按事实发生日计数，使用独立总体。" />}
    <SectionCard className="filter-card"><div className={`filter-grid${designPreview ? '' : ' analytics-filter-grid'}`}>
      {designPreview && <Field label="站点"><Select value={site} onChange={setSite} options={['华东分拨中心', '华南分拨中心', '全部站点'].map(value => ({ value, label: value }))} /></Field>}
      {designPreview && <Field label="产线"><Select value={line} onChange={setLine} options={['全部产线', '产线 1', '产线 2', '产线 3'].map(value => ({ value, label: value }))} /></Field>}
      <Field label="日期范围" wide><DatePicker.RangePicker separator="~" value={range} onChange={setRange} style={{ width: '100%' }} /></Field>
      <FilterActions onSearch={onSearch} onReset={onReset} extra={designPreview ? undefined : <Button onClick={refresh} loading={loading}>刷新</Button>} />
    </div></SectionCard>
    <div className="three-cols analytics-metrics">
      <div className="metric-card"><VectorIcon name="parcels" surface="analytics" className="metric-reference-icon" size={50} glyphSize={28} color="#1677ff" background="#eaf3ff" borderRadius={12} /><div className="metric-title">件量 <InfoCircleOutlined className="text-muted" /></div><div className="metric-number">{data ? data.detectedCount.toLocaleString() : '—'} <small>件</small></div><div className="metric-caption">统计范围：{applied.from} ~ {applied.to}<br />口径：{designPreview ? '已完成分拣的包裹件数' : '首次入库的包裹件数'}</div></div>
      <div className="metric-card"><VectorIcon name="errors" className="metric-reference-icon" size={50} glyphSize={28} color="#ff4d4f" background="#fff0f0" borderRadius={12} /><div className="metric-title">异常率 <InfoCircleOutlined className="text-muted" /></div><div className="metric-number">{exceptionRate ?? '—'} {exceptionRate !== null && <small>%</small>}</div><div className="metric-caption">统计范围：{applied.from} ~ {applied.to}<br />口径：{designPreview ? '异常件数 / 总件数' : '当前异常件数 / 入库件数'}</div></div>
      <div className="metric-card"><VectorIcon name="time" className="metric-reference-icon" size={50} glyphSize={28} color="#00b96b" background="#eafbf0" borderRadius={12} /><div className="metric-title">平均分拣时效 <InfoCircleOutlined className="text-muted" /></div><div className="metric-number">{data?.averageLifecycleSeconds == null ? '—' : data.averageLifecycleSeconds.toFixed(1)} <small>秒</small></div><div className="metric-caption">统计范围：{applied.from} ~ {applied.to}<br />口径：{designPreview ? '包裹进入至分拣完成的平均用时' : '有效样本的完成耗时平均'}</div></div>
    </div>
    <div className="two-cols analytics-distribution"><SectionCard title="按异常类型分布"><DataTable<Distribution> rowKey={row => row.code ?? row.name} tableLayout="fixed" scroll={{ x: undefined }} loading={loading} locale={{ emptyText: '暂无异常包裹' }} dataSource={data?.exceptionTypes ?? []} columns={[{ title: '异常类型', dataIndex: 'name', width: 217 }, { title: '件数', dataIndex: 'count', width: 162, render: (value: number) => value.toLocaleString() }, { title: '占比', dataIndex: 'count', render: (value: number, record) => record.designPreviewPercent ?? percent(value, data?.exceptionCount ?? 0) }]} /></SectionCard>
      <SectionCard title="按工作台分布" extra={data?.workstationsTruncated ? '仅显示前几项' : undefined}><DataTable<Distribution> rowKey={row => row.code ?? row.name} tableLayout="fixed" scroll={{ x: undefined }} loading={loading} locale={{ emptyText: '暂无来源包裹' }} dataSource={data?.workstations ?? []} columns={[{ title: '工作台', dataIndex: 'name', width: 219 }, { title: '件数', dataIndex: 'count', width: 175, render: (value: number) => value.toLocaleString() }, { title: '占比', dataIndex: 'count', render: (value: number, record) => record.designPreviewPercent ?? percent(value, data?.detectedCount ?? 0) }]} /></SectionCard></div>
    <SectionCard title="按日汇总" className="analytics-daily"><DataTable<Daily> rowKey="date" tableLayout="fixed" scroll={{ x: undefined }} loading={loading} locale={{ emptyText: '该范围暂无来源包裹' }} dataSource={data?.daily ?? []} columns={[{ title: '日期', dataIndex: 'date', width: 226 }, { title: '件量', dataIndex: 'detectedCount', width: 184, render: (value: number) => value.toLocaleString() }, { title: '异常件数', dataIndex: 'exceptionCount', width: 213, render: (value: number) => value.toLocaleString() }, { title: '异常率', width: 204, render: (_, record) => percent(record.exceptionCount, record.detectedCount) }, { title: '平均分拣时效（秒）', dataIndex: 'averageLifecycleSeconds', render: (value: number | null) => value == null ? '—' : value.toFixed(1) }]} /></SectionCard>
    <div className="analytics-fact-strip" aria-label="处理质量指标">
      <div><span>NoRead 件数</span><strong>{data ? data.noReadCount.toLocaleString() : '—'}</strong></div>
      <div><span>格口不一致件数</span><strong>{data ? data.chuteMismatchCount.toLocaleString() : '—'}</strong></div>
      <div><span>失败尝试 / 处理事实</span><strong>{data ? `${data.failedAttemptCount.toLocaleString()} / ${data.processingEventCount.toLocaleString()}` : '—'}</strong></div>
      <div><span>未绑定 DWS 事实</span><strong>{data ? data.unboundDwsEventCount.toLocaleString() : '—'}</strong></div>
    </div>
    <SectionCard title="每日扩展指标" className="analytics-detail-daily"><DataTable<Daily> rowKey="date" loading={loading} dataSource={data?.daily ?? []} locale={{ emptyText: '该范围暂无来源包裹' }} columns={[{ title: '日期', dataIndex: 'date' }, { title: '当前完成', dataIndex: 'completedCount' }, { title: 'NoRead', dataIndex: 'noReadCount' }, { title: '格口不一致', dataIndex: 'chuteMismatchCount' }, { title: '有效耗时样本', dataIndex: 'lifecycleSampleCount' }]} /></SectionCard>
  </div>;
}
