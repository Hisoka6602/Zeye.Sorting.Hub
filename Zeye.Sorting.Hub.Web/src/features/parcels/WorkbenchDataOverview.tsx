import { formatNumber } from '../../data/formatNumber';
import {
  AppstoreOutlined, ArrowRightOutlined, CheckCircleOutlined, FieldTimeOutlined, InfoCircleOutlined,
  ReloadOutlined, ScanOutlined, WarningOutlined,
} from '@ant-design/icons';
import { Alert, App, Button, DatePicker, Empty, Spin, Tooltip } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { SectionCard } from '../../components/SectionCard';
import { useApiResource } from '../../data/api/useApiResource';
import { ExceptionDonut, ExceptionShareDonut, WorkstationBars, stationShade, type Distribution } from './WorkbenchDistributionCharts';
import { formatTrendCount, trendValueLabelIndexes } from './trendChartFormat';
import { sortingThroughputMetric } from './sortingThroughputMetric';
import { workbenchMetricDays, type WorkbenchMetricDay } from './workbenchMetricDays';
import { WorkbenchMetricTrend } from './WorkbenchMetricTrend';

interface DailySorting { date: string; sortedCount: number }
interface WorkbenchAnalytics {
  detectedCount: number;
  completedCount: number;
  exceptionCount: number;
  noReadCount: number;
  medianCreationIntervalMilliseconds: number | null;
  minimumCreationIntervalMilliseconds: number | null;
  actualSortingThroughputPerHour: number | null;
  theoreticalSortingThroughputPerHour: number | null;
  creationIntervalSampleCount: number;
  exceptionTypes: Distribution[];
  workstations: Distribution[];
  workstationsTruncated: boolean;
  daily: Omit<WorkbenchMetricDay, 'exceptionPercent'>[];
  dailySorting: DailySorting[];
}

interface DateRange { from: string; to: string }

function initialRange(): [Dayjs, Dayjs] {
  return import.meta.env.MODE === 'design-preview'
    ? [dayjs('2026-08-26'), dayjs('2026-09-25')]
    : [dayjs().subtract(30, 'day').startOf('day'), dayjs().startOf('day')];
}

function percent(part: number, total: number): string {
  return total > 0 ? `${formatNumber(part / total * 100)}%` : '—';
}

function Metric({ title, value, suffix, note, hint, icon, tone, label, comparison, trend, sideVisual }: {
  title: string; value: string; suffix?: string; note: string; hint?: string; icon: ReactNode; tone: string;
  label?: string; comparison?: { label: string; value: string; note: string };
  trend?: ReactNode; sideVisual?: ReactNode;
}) {
  return <div className={`metric-card workbench-data-metric tone-${tone}${sideVisual ? ' has-side-visual' : ''}`}>
    <div className="workbench-data-metric-top">
      <span>{title}{hint && <Tooltip title={hint}><span tabIndex={0} aria-label={`${title}说明`}> <InfoCircleOutlined /></span></Tooltip>}</span><span className="workbench-data-metric-icon" aria-hidden="true">{icon}</span>
    </div>
    {comparison ? <div className="workbench-data-throughput" aria-label="实际与理论分拣时效">
      {[{ label, value, note }, comparison].map(item => <div className="workbench-data-throughput-stat" key={item.label}>
        <span className="workbench-data-throughput-label">{item.label}</span>
        <div className="workbench-data-metric-value">{item.value}{suffix && item.value !== '—' && <small>{suffix}</small>}</div>
        <div className="workbench-data-metric-note">{item.note}</div>
      </div>)}
    </div> : <>
      {sideVisual ? <div className="workbench-data-metric-row">
        <div className="workbench-data-metric-value">{value}{suffix && value !== '—' && <small>{suffix}</small>}</div>
        {sideVisual}
      </div> : <>
        <div className="workbench-data-metric-value">{value}{suffix && value !== '—' && <small>{suffix}</small>}</div>
        {trend}
      </>}
      <div className="workbench-data-metric-note">{note}</div>
    </>}
  </div>;
}

/** 全部日期随容器宽度展示，窄屏仅减少文字标签而保留每一天的柱形。 */
function DailyTrend({ rows, range }: { rows: DailySorting[]; range: DateRange }) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const [chartWidth, setChartWidth] = useState(1000);
  const byDate = new Map(rows.map(row => [row.date, row.sortedCount]));
  const start = dayjs(range.from);
  const length = dayjs(range.to).diff(start, 'day') + 1;
  const days = Array.from({ length }, (_, index) => {
    const date = start.add(index, 'day').format('YYYY-MM-DD');
    return { date, count: byDate.get(date) ?? 0 };
  });
  const max = Math.max(...days.map(day => day.count), 1);
  const unit = Math.pow(10, Math.floor(Math.log10(Math.max(max / 4, 1))));
  const step = Math.max(1, Math.ceil(max / 4 / unit) * unit);
  const upper = step * 4;
  const plot = { x: 56, y: 30, width: Math.max(1, chartWidth - 86), height: 168 };
  const barStep = plot.width / days.length;
  const labelEvery = Math.max(1, Math.ceil(days.length / Math.max(2, Math.floor(plot.width / 70))));
  const valueLabelIndexes = trendValueLabelIndexes(days.map(day => day.count), plot.width);
  const activeDays = days.filter(day => day.count > 0);
  const activeIndexes = new Map(activeDays.map((day, index) => [day.date, index]));
  const total = activeDays.reduce((sum, day) => sum + day.count, 0);

  useEffect(() => {
    const container = viewportRef.current;
    if (!container) return;
    const observer = new ResizeObserver(([entry]) => setChartWidth(Math.max(1, entry.contentRect.width)));
    observer.observe(container);
    return () => observer.disconnect();
  }, []);

  return <div className="workbench-data-chart-area">
    <div className="workbench-data-chart-viewport" ref={viewportRef}>
      <svg className="workbench-data-chart" viewBox={`0 0 ${chartWidth} 242`} role="img" aria-label={`${range.from} 至 ${range.to} 的每日完成分拣票数柱状图，共 ${formatNumber(total, { grouping: true })} 票`}>
      {[0, 1, 2, 3, 4].map(index => {
        const y = plot.y + index * plot.height / 4;
        return <g key={index}>
          <line x1={plot.x} x2={plot.x + plot.width} y1={y} y2={y} stroke="#e8edf5" />
          <text x={plot.x - 12} y={y + 4} textAnchor="end" className="workbench-data-axis">{formatTrendCount((4 - index) * step)}</text>
        </g>;
      })}
      {days.map((day, index) => {
        const height = day.count / upper * plot.height;
        const width = Math.min(42, barStep * .54);
        const x = plot.x + index * barStep + (barStep - width) / 2;
        const y = plot.y + plot.height - height;
        return <g key={day.date}>
          <rect x={x} y={y} width={width} height={height} rx="3" fill={stationShade(activeIndexes.get(day.date) ?? 0, activeDays.length)}>
            <title>{day.date}：{formatNumber(day.count, { grouping: true })} 票</title>
          </rect>
          {valueLabelIndexes.has(index) && <text x={x + width / 2} y={Math.max(18, y - 8)} textAnchor="middle" className="workbench-data-value-label" aria-hidden="true">{formatTrendCount(day.count)}</text>}
          {(index === days.length - 1 || index % labelEvery === 0 && (days.length - 1 - index) * barStep >= 54) &&
            <text x={plot.x + index * barStep + barStep / 2} y="225" textAnchor="middle" className="workbench-data-axis">{day.date.slice(5)}</text>}
        </g>;
      })}
      </svg>
    </div>
    {max >= 1_000 && <p className="workbench-data-chart-units">K = 千 · M = 百万 · B = 十亿；柱形提示显示精确票数</p>}
  </div>;
}

/** 与分析报表共用真实聚合接口；无来源数据时保留空态，不显示示例数字。 */
export function WorkbenchDataOverview() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(() => initialRange());
  const [applied, setApplied] = useState<DateRange>(() => {
    const [from, to] = initialRange();
    return { from: from.format('YYYY-MM-DD'), to: to.format('YYYY-MM-DD') };
  });
  const path = useMemo(() => `/api/parcels/analytics?fromDate=${encodeURIComponent(applied.from)}&toDate=${encodeURIComponent(applied.to)}`, [applied]);
  const { data, loading, error, refresh } = useApiResource<WorkbenchAnalytics>(path);
  const query = () => {
    if (!range?.[0] || !range[1]) { message.warning('请选择完整日期范围'); return; }
    if (range[1].isBefore(range[0]) || range[1].startOf('day').diff(range[0].startOf('day'), 'day') > 30) {
      message.warning('日期范围最多为 31 天'); return;
    }
    const next = { from: range[0].format('YYYY-MM-DD'), to: range[1].format('YYYY-MM-DD') };
    if (next.from === applied.from && next.to === applied.to) refresh();
    else setApplied(next);
  };
  const number = (value?: number) => value === undefined ? '—' : formatNumber(value, { grouping: true });
  const throughput = sortingThroughputMetric(data);
  const metricDays = useMemo(() => workbenchMetricDays(data?.daily, applied), [data, applied]);

  return <section className="workbench-data" aria-labelledby="workbench-data-title">
    <div className="workbench-data-heading">
      <div><h2 id="workbench-data-title">分拣表现</h2><p>卡片按首次入库日期统计，每日分拣趋势按完成日期统计</p></div>
      <Button type="link" icon={<ArrowRightOutlined />} iconPosition="end" onClick={() => navigate('/analytics')}>查看完整报表</Button>
    </div>
    <div className="workbench-data-filter">
      <span className="workbench-data-filter-label">日期范围</span>
      <div className="workbench-data-range-desktop"><DatePicker.RangePicker value={range} onChange={setRange} allowClear={false} aria-label="数据概览日期范围" /></div>
      <div className="workbench-data-range-mobile">
        <div><label htmlFor="workbench-data-from">开始日期</label><DatePicker id="workbench-data-from" value={range?.[0]} onChange={value => setRange(current => [value, current?.[1] ?? null])} allowClear={false} /></div>
        <div><label htmlFor="workbench-data-to">结束日期</label><DatePicker id="workbench-data-to" value={range?.[1]} onChange={value => setRange(current => [current?.[0] ?? null, value])} allowClear={false} /></div>
      </div>
      <Button type="primary" onClick={query} loading={loading}>查询</Button>
      <Button icon={<ReloadOutlined />} onClick={refresh} disabled={loading} aria-label="刷新数据概览">刷新</Button>
      <span className="workbench-data-filter-note">最长 31 天 · 当前快照</span>
    </div>
    {error && <Alert className="workbench-data-error" type="error" showIcon message={`数据概览读取失败：${error.message}`} action={<Button size="small" onClick={refresh}>重试</Button>} />}
    <div className="workbench-data-metrics">
      <Metric title="检测入库" value={number(data?.detectedCount)} suffix="票" note="时间范围内首次入库" icon={<AppstoreOutlined />} tone="blue"
        trend={<WorkbenchMetricTrend days={metricDays} metric="detectedCount" title="检测入库" loading={loading} />} />
      <Metric title="已完成分拣" value={number(data?.completedCount)} suffix="票" note="入库包裹的当前完成数" icon={<CheckCircleOutlined />} tone="green"
        trend={<WorkbenchMetricTrend days={metricDays} metric="completedCount" title="已完成分拣" loading={loading} />} />
      <Metric title="分拣异常" value={number(data?.exceptionCount)} suffix="票" note="入库包裹的当前异常数" icon={<WarningOutlined />} tone="red"
        trend={<WorkbenchMetricTrend days={metricDays} metric="exceptionCount" title="分拣异常" loading={loading} />} />
      <Metric title="异常占比" value={data ? percent(data.exceptionCount, data.detectedCount) : '—'} note="反映入库包裹的异常情况" icon={<WarningOutlined />} tone="orange"
        sideVisual={<ExceptionShareDonut count={data?.exceptionCount} total={data?.detectedCount} loading={loading} />} />
      <Metric title="NoRead 票数" value={number(data?.noReadCount)} suffix="票" note="条码未读或 NoRead 状态" icon={<ScanOutlined />} tone="blue"
        trend={<WorkbenchMetricTrend days={metricDays} metric="noReadCount" title="NoRead 票数" loading={loading} />} />
      <Metric title="分拣时效" label="实际时效" value={throughput.actual.value} suffix="票/小时" note={throughput.actual.note}
        comparison={{ label: '理论时效', ...throughput.theoretical }} hint={throughput.hint} icon={<FieldTimeOutlined />} tone="green" />
    </div>
    <div className="workbench-data-distributions">
      <SectionCard title="异常类型分布" extra={data && !loading ? <span className="workbench-data-panel-badge">共 {number(data.exceptionCount)} 票</span> : undefined} className="workbench-data-panel workbench-data-panel-exceptions">
        <p className="workbench-data-panel-caption">当前异常构成 · 数量与占比</p>
        {loading ? <div className="workbench-distribution-empty"><Spin /></div>
          : <ExceptionDonut rows={data?.exceptionTypes ?? []} total={data?.exceptionCount ?? 0} />}
      </SectionCard>
      <SectionCard title="工作台票数分布" extra={data && !loading ? <span className="workbench-data-panel-badge is-blue">共 {number(data.detectedCount)} 票</span> : undefined} className="workbench-data-panel workbench-data-panel-stations">
        <p className="workbench-data-panel-caption">按工作台比较 · 柱顶显示票数</p>
        {loading ? <div className="workbench-distribution-empty"><Spin /></div>
          : <WorkstationBars rows={data?.workstations ?? []} total={data?.detectedCount ?? 0} />}
      </SectionCard>
    </div>
    <SectionCard title="每日分拣趋势" extra={<span className="workbench-data-panel-note">{applied.from} 至 {applied.to} · 票</span>} className="workbench-data-trend">
      {loading ? <div className="workbench-data-chart-empty"><Spin /></div>
        : data?.dailySorting?.length ? <DailyTrend rows={data.dailySorting} range={applied} />
          : <div className="workbench-data-chart-empty"><Empty description="该时间范围暂无完成分拣数据" /></div>}
    </SectionCard>
  </section>;
}
