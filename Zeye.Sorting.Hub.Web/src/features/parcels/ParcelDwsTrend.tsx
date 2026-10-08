import { Empty } from 'antd';
import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { localTime } from '../../data/api/operationalTypes';
import { formatNumber } from '../../data/formatNumber';
import type { DwsMeasurementMetric, DwsMeasurementSample } from '../../data/api/parcelDwsConsistencyTypes';
import { dwsPlotSamples, dwsValue, type DwsPlotMetric } from './parcelDwsConsistencyModel';

const colors = ['#5478cb', '#42a18b', '#c68d35', '#ad6fc4', '#da7984', '#527786'];
/** 三种指标独立纵轴，各自真实时间横轴；散点不暗示不同过机记录之间连续变化。 */
export function ParcelDwsTrend({ samples, metric, name, unit, summary }: {
  samples: DwsMeasurementSample[]; metric: DwsPlotMetric; name: string; unit: string; summary: DwsMeasurementMetric;
}) {
  const id = useId();
  const points = useMemo(() => dwsPlotSamples(samples, metric), [samples, metric]);
  const sources = useMemo(() => [...new Set(points.map(point => point.sample.sourceInstanceId))].sort(), [points]);
  const figure = useRef<HTMLElement>(null); const [width, setWidth] = useState(680);
  const hasPoints = points.length > 0;
  useEffect(() => {
    const element = figure.current; if (!element) return;
    const observer = new ResizeObserver(entries => setWidth(Math.max(310, Math.min(680, entries[0].contentRect.width))));
    observer.observe(element); return () => observer.disconnect();
  }, [hasPoints]);
  const timeName = metric === 'scanDurationMilliseconds' ? '有效条码接收时间' : '测量时间';
  if (!points.length) return <div className="dws-trend"><h3>{name}趋势</h3><Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={`暂无带${timeName}的有效样本`} /></div>;
  const values = points.map(point => point.value);
  if (summary.reference != null) values.push(summary.reference);
  if (summary.median != null) values.push(summary.median);
  const minimum = Math.min(...values), maximum = Math.max(...values);
  const padding = Math.max((maximum - minimum) * .15, Math.max(maximum, 1) * .005);
  const low = Math.max(0, minimum - padding), high = maximum + padding;
  const first = points[0].at, last = points[points.length - 1].at;
  const right = width - 34;
  const x = (at: number) => first === last ? (78 + right) / 2 : 78 + (at - first) / (last - first) * (right - 78);
  const y = (value: number) => 207 - (value - low) / (high - low) * 169;
  return <figure className="dws-trend" ref={figure}>
    <figcaption><h3>{name}趋势</h3><span>{unit} · {points.length} 个测量点</span></figcaption>
    <svg viewBox={`0 0 ${width} 270`} role="img" aria-labelledby={`${id}-title ${id}-desc`}>
      <title id={`${id}-title`}>{name}重复测量趋势</title><desc id={`${id}-desc`}>横轴为{timeName}，纵轴为{name}（{unit}）。最小{dwsValue(summary.minimum)}，最大{dwsValue(summary.maximum)}，中位数{dwsValue(summary.median)}。所有测量值可在下方表格查看。</desc>
      {[0, 1, 2, 3].map(index => { const value = low + (high - low) * index / 3; return <g key={index}><line x1="78" x2={right} y1={y(value)} y2={y(value)} stroke="#e9edf5" /><text x="66" y={y(value) + 4} textAnchor="end">{formatNumber(value, { grouping: true })}</text></g>; })}
      {summary.median != null && <g><line x1="78" x2={right} y1={y(summary.median)} y2={y(summary.median)} stroke="#66748d" strokeDasharray="4 4" /><text x={right} y="16" textAnchor="end">中位数 {dwsValue(summary.median, unit)}</text></g>}
      {summary.reference != null && <g><line x1="78" x2={right} y1={y(summary.reference)} y2={y(summary.reference)} stroke="#ba7435" strokeDasharray="8 3" /><text x="78" y={width < 450 ? 31 : 16}>参考值 {dwsValue(summary.reference, unit)}</text></g>}
      {points.map(({ sample, at, value, timestamp }) => <circle key={sample.key} cx={x(at)} cy={y(value)} r="4.5" fill={colors[sources.indexOf(sample.sourceInstanceId) % colors.length]} fillOpacity=".8" stroke="white" strokeWidth="1">
        <title>{sample.workstationName || sample.sourceInstanceId} · {localTime(timestamp)} · {dwsValue(value, unit)}</title></circle>)}
      <text x="78" y="234">{localTime(points[0].timestamp)}</text>
      {first !== last && <text x={right} y="253" textAnchor="end">{localTime(points[points.length - 1].timestamp)}</text>}
    </svg>
    <div className="dws-trend-legend" aria-label="来源工作台图例">{sources.slice(0, 6).map((source, index) => <span key={source}><i style={{ background: colors[index] }} />{points.find(point => point.sample.sourceInstanceId === source)?.sample.workstationName || source}</span>)}
      {sources.length > 6 && <span>其余 {sources.length - 6} 个来源请查看明细</span>}</div>
  </figure>;
}
