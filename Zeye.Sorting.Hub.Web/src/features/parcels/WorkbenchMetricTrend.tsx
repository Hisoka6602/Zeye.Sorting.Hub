import { formatNumber } from '../../data/formatNumber';
import type { WorkbenchMetricDay, WorkbenchMetricKey } from './workbenchMetricDays';

interface TrendPoint { x: number; y: number }

/** 淡色辅助图只呈现真实每日数据，保留无分母日期的断点。 */
export function WorkbenchMetricTrend({ days, metric, title, loading }: {
  days: WorkbenchMetricDay[]; metric: WorkbenchMetricKey; title: string; loading: boolean;
}) {
  if (loading || !days.some(day => day.detectedCount > 0)) {
    return <div className="workbench-metric-trend is-empty">
      <span>{loading ? '正在读取每日分布…' : days.length ? '所选日期暂无入库记录' : '每日分布暂不可用'}</span>
    </div>;
  }

  const values = days.map(day => day[metric]);
  const maximum = Math.max(1, ...values.filter((value): value is number => value !== null));
  const step = 312 / Math.max(days.length - 1, 1);
  const points = values.map((value, index) => value === null ? null : {
    x: days.length === 1 ? 160 : 4 + index * step,
    y: 40 - value / maximum * 34,
  });
  const segments: TrendPoint[][] = [];
  let segment: TrendPoint[] = [];
  for (const point of points) {
    if (point) segment.push(point);
    else if (segment.length) { segments.push(segment); segment = []; }
  }
  if (segment.length) segments.push(segment);
  const lastPoint = points.reduce((last, point, index) => point ? index : last, -1);
  const formatValue = (value: number | null) => value === null ? '暂无入库数据'
    : `${formatNumber(value, { grouping: true })}${metric === 'exceptionPercent' ? '%' : ' 票'}`;

  return <div className="workbench-metric-trend">
    <svg viewBox="0 0 320 44" preserveAspectRatio="none" role="img"
      aria-label={`${title}每日分布，${days[0].date} 至 ${days[days.length - 1].date}，按首次入库日期统计`}>
      <line x1="4" x2="316" y1="40" y2="40" className="workbench-metric-trend-baseline" />
      {segments.filter(part => part.length > 1).map((part, index) => {
        const line = part.map(point => `${point.x},${point.y}`).join(' ');
        return <g key={index}>
          <polygon points={`${part[0].x},40 ${line} ${part[part.length - 1].x},40`} className="workbench-metric-trend-area" />
          <polyline points={line} className="workbench-metric-trend-line" vectorEffect="non-scaling-stroke" />
        </g>;
      })}
      {days.map((day, index) => {
        const point = points[index];
        const x = days.length === 1 ? 160 : 4 + index * step;
        const isolated = point && !points[index - 1] && !points[index + 1];
        return <g key={day.date} className={`workbench-metric-trend-day${isolated || index === lastPoint ? ' is-visible' : ''}`}>
          <title>{day.date}：{formatValue(day[metric])}</title>
          <rect x={Math.max(0, x - step / 2)} y="0" width={days.length === 1 ? 320 : step} height="44" fill="transparent" />
          {point && <circle cx={point.x} cy={point.y} r={isolated ? 3 : 2.5} />}
        </g>;
      })}
    </svg>
    <div className="workbench-metric-trend-caption" aria-hidden="true">
      <span>{days[0].date.slice(5).replace('-', '.')}</span>
      <span>{days.length === 1 ? '单日分布' : '按入库日分布'}</span>
      {days.length > 1 && <span>{days[days.length - 1].date.slice(5).replace('-', '.')}</span>}
    </div>
  </div>;
}
