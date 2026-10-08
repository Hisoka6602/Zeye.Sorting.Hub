import { formatNumber } from '../../data/formatNumber';
import { Empty } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { formatTrendCount } from '../parcels/trendChartFormat';
import { analyticsCountAxis, analyticsPercent, type AnalyticsDaily, type AnalyticsDistribution } from './analyticsModel';

/** React owns the SVG; geometry follows the panel width without shrinking mobile labels. */
export function AnalyticsTrend({ rows, unavailable = false }: { rows: AnalyticsDaily[]; unavailable?: boolean }) {
  const container = useRef<HTMLDivElement>(null);
  const [availableWidth, setAvailableWidth] = useState(0);
  useEffect(() => {
    if (!container.current) return;
    const observer = new ResizeObserver(entries => setAvailableWidth(Math.round(entries[0].contentRect.width)));
    observer.observe(container.current);
    return () => observer.disconnect();
  }, []);

  const maximum = Math.max(0, ...rows.map(row => Math.max(row.detectedCount, row.exceptionCount)));
  const { upper, ticks } = analyticsCountAxis(maximum);
  const minimumSlot = Math.max(44, ...rows.map(row => (formatTrendCount(row.detectedCount).length + formatTrendCount(row.exceptionCount).length) * 7 + 12));
  const width = Math.max(availableWidth, rows.length * minimumSlot + 60, 320);
  const left = 44, right = 14, top = 28, baseline = 220;
  const slot = (width - left - right) / Math.max(rows.length, 1);
  const barWidth = Math.min(18, slot * .28);
  const heightOf = (value: number) => value / upper * (baseline - top);

  return <div ref={container} className="analytics-trend-container">
    {unavailable || maximum <= 0 ? <div className="analytics-chart-empty"><Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={unavailable ? '报表暂不可用，请刷新重试' : '该时间范围暂无入库包裹'} /></div>
      : <div className="analytics-trend-scroll" tabIndex={0} role="region" aria-label="每日入库趋势，日期较多时可横向滚动">
        <svg width={width} height="266" role="img" aria-label={`每日入库与当前异常票数；${rows.map(row => `${row.date} 入库 ${row.detectedCount} 票、当前异常 ${row.exceptionCount} 票`).join('；')}`}>
          {ticks.map(tick => {
            const y = baseline - heightOf(tick);
            return <g key={tick} aria-hidden="true">
              <line x1={left} y1={y} x2={width - right} y2={y} className="analytics-chart-gridline" />
              <text x={left - 10} y={y + 4} textAnchor="end" className="analytics-chart-tick">{formatTrendCount(tick)}</text>
            </g>;
          })}
          {rows.map((row, index) => {
            const x = left + slot * (index + .5);
            const inboundHeight = heightOf(row.detectedCount), exceptionHeight = heightOf(row.exceptionCount);
            const labelSpacing = (formatTrendCount(row.detectedCount).length + formatTrendCount(row.exceptionCount).length) * 3.5 + 8;
            const pairSpacing = Math.max(barWidth + 4, labelSpacing);
            const inboundX = x - pairSpacing / 2, exceptionX = x + pairSpacing / 2;
            return <g key={row.date}>
              <title>{`${row.date}：入库 ${formatNumber(row.detectedCount, { grouping: true })} 票，当前异常 ${formatNumber(row.exceptionCount, { grouping: true })} 票`}</title>
              <rect x={inboundX - barWidth / 2} y={baseline - inboundHeight} width={barWidth} height={inboundHeight} rx="3" className="analytics-chart-inbound" />
              <rect x={exceptionX - barWidth / 2} y={baseline - exceptionHeight} width={barWidth} height={exceptionHeight} rx="3" className="analytics-chart-exception" />
              <text x={inboundX} y={baseline - inboundHeight - 8} textAnchor="middle" className="analytics-chart-count">{formatTrendCount(row.detectedCount)}</text>
              <text x={exceptionX} y={baseline - exceptionHeight - 8} textAnchor="middle" className="analytics-chart-count analytics-chart-count-exception">{formatTrendCount(row.exceptionCount)}</text>
              <text x={x} y={baseline + 25} textAnchor="middle" className="analytics-chart-tick">{row.date.slice(5).replace('-', '/')}</text>
            </g>;
          })}
        </svg>
      </div>}
  </div>;
}

export function AnalyticsRanking({ rows, total, kind, unavailable = false }: { rows: AnalyticsDistribution[]; total: number; kind: 'exception' | 'workstation'; unavailable?: boolean }) {
  const ranked = [...rows].filter(row => row.count > 0).sort((a, b) => b.count - a.count).slice(0, 5);
  if (unavailable || !ranked.length) return <div className="analytics-chart-empty"><Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={unavailable ? '报表暂不可用，请刷新重试' : kind === 'exception' ? '该时间范围暂无异常包裹' : '该时间范围暂无来源包裹'} /></div>;
  return <div className={`analytics-ranking analytics-ranking-${kind}`} role="list" aria-label={kind === 'exception' ? '异常类型分布' : '工作台票数分布'}>
    {ranked.map((row, index) => {
      const share = row.designPreviewPercent ?? analyticsPercent(row.count, total);
      const width = total > 0 ? Math.min(100, row.count / total * 100) : 0;
      return <div className="analytics-ranking-row" role="listitem" key={row.code ?? row.name}>
        <span className="analytics-ranking-position" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
        <div className="analytics-ranking-content">
          <div className="analytics-ranking-label"><span>{row.name}</span><strong>{formatNumber(row.count, { grouping: true })} <small>票</small><span>{share}</span></strong></div>
          <div className="analytics-ranking-track" aria-hidden="true"><div style={{ width: `${width}%` }} /></div>
        </div>
      </div>;
    })}
  </div>;
}
