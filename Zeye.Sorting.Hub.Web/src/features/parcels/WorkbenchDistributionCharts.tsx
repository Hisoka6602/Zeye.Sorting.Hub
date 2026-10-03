import { formatNumber } from '../../data/formatNumber';
import { Empty } from 'antd';

export interface Distribution { code: string | null; name: string; count: number }

const exceptionColors = ['#e45c6a', '#ef8672', '#e7aa66', '#8b99df', '#6fa8dd', '#b8c6da'];
const stationColors = ['#376ce7', '#5485ee', '#709bf2', '#8aaef5', '#adc6f8'];

/** 在工作台柱形图的蓝色色阶中取连续颜色，供每日趋势保持同一视觉语言。 */
export function stationShade(index: number, count: number): string {
  if (count <= 1) return stationColors[0];
  const position = index / (count - 1) * (stationColors.length - 1);
  const lower = Math.floor(position);
  const fraction = position - lower;
  const start = stationColors[lower];
  const end = stationColors[Math.min(lower + 1, stationColors.length - 1)];
  const component = (offset: number) => Math.round(
    parseInt(start.slice(offset, offset + 2), 16) * (1 - fraction)
    + parseInt(end.slice(offset, offset + 2), 16) * fraction,
  );
  return `rgb(${component(1)}, ${component(3)}, ${component(5)})`;
}

function share(count: number, total: number): string {
  return total > 0 ? `${formatNumber(count / total * 100)}%` : '0%';
}

function positiveRows(rows: Distribution[]): Distribution[] {
  return rows.filter(row => row.count > 0).sort((a, b) => b.count - a.count);
}

/** KPI 占比使用查询范围的入库总体，剩余部分包含所有非异常状态。 */
export function ExceptionShareDonut({ count, total, loading }: { count?: number; total?: number; loading: boolean }) {
  const available = count !== undefined && total !== undefined && Number.isFinite(count) && Number.isFinite(total)
    && total > 0 && count >= 0 && count <= total;
  if (loading || !available) return <div className="workbench-metric-trend is-empty">
    <span>{loading ? '正在读取占比…' : total === 0 ? '所选日期暂无入库记录' : '占比数据暂不可用'}</span>
  </div>;

  const remainder = total - count;
  const percentage = count / total * 100;
  const countText = formatNumber(count, { grouping: true });
  const remainderText = formatNumber(remainder, { grouping: true });
  return <div className="workbench-metric-share">
    <svg viewBox="0 0 72 72" role="img" aria-label={`异常占比饼图：异常包裹 ${countText} 件，占 ${share(count, total)}；其余包裹 ${remainderText} 件，占 ${share(remainder, total)}`}>
      <circle cx="36" cy="36" r="25" fill="none" strokeWidth="12" className="workbench-metric-share-rest">
        <title>其余包裹：{remainderText} 件，占 {share(remainder, total)}</title>
      </circle>
      <circle cx="36" cy="36" r="25" fill="none" strokeWidth="12" pathLength="100"
        strokeDasharray={`${percentage} ${100 - percentage}`} transform="rotate(-90 36 36)" className="workbench-metric-share-exception">
        <title>异常包裹：{countText} 件，占 {share(count, total)}</title>
      </circle>
    </svg>
    <div className="workbench-metric-share-legend" aria-label="异常占比图例">
      <div><span className="workbench-metric-share-dot is-exception" aria-hidden="true" /><span>异常包裹</span><strong>{countText} <small>件</small></strong></div>
      <div><span className="workbench-metric-share-dot" aria-hidden="true" /><span>其余包裹</span><strong>{remainderText} <small>件</small></strong></div>
    </div>
  </div>;
}

export function ExceptionDonut({ rows, total }: { rows: Distribution[]; total: number }) {
  if (total <= 0) return <div className="workbench-distribution-empty"><Empty description="该时间范围暂无异常包裹" /></div>;

  const sorted = positiveRows(rows);
  const visibleCount = sorted.length <= 5 ? 5 : 4;
  const slices = sorted.slice(0, visibleCount).map(row => ({ name: row.name, count: row.count }));
  const remaining = sorted.slice(visibleCount).reduce((sum, row) => sum + row.count, 0);
  if (remaining > 0) slices.push({ name: '其他类型', count: remaining });
  const uncategorized = Math.max(0, total - sorted.reduce((sum, row) => sum + row.count, 0));
  if (uncategorized > 0) slices.push({ name: '未分类', count: uncategorized });
  const chartTotal = slices.reduce((sum, row) => sum + row.count, 0);
  const circumference = 2 * Math.PI * 78;
  let offset = 0;

  return <div className="workbench-donut-layout">
    <svg className="workbench-donut" viewBox="0 0 220 220" role="img" aria-label={`异常类型占比环形图，共 ${total} 件；${slices.map(row => `${row.name} ${row.count} 件`).join('；')}`}>
      <circle cx="110" cy="110" r="78" fill="none" stroke="#edf2f8" strokeWidth="25" />
      {slices.map((slice, index) => {
        const span = slice.count / chartTotal * circumference;
        const start = offset;
        offset += span;
        return <circle key={slice.name} cx="110" cy="110" r="78" fill="none"
          stroke={exceptionColors[index % exceptionColors.length]} strokeWidth="25"
          strokeDasharray={`${Math.max(span - 3, 1)} ${circumference}`}
          strokeDashoffset={-start} transform="rotate(-90 110 110)">
          <title>{slice.name}：{formatNumber(slice.count, { grouping: true })} 件，占异常 {share(slice.count, total)}</title>
        </circle>;
      })}
      <text x="110" y="103" textAnchor="middle" className="workbench-donut-total">{formatNumber(total, { grouping: true })}</text>
      <text x="110" y="126" textAnchor="middle" className="workbench-donut-unit">件异常</text>
    </svg>
    <div className="workbench-donut-legend" aria-label="异常类型图例">
      {slices.map((slice, index) => <div className="workbench-donut-legend-item" key={slice.name}>
        <span className="workbench-donut-swatch" style={{ backgroundColor: exceptionColors[index % exceptionColors.length] }} aria-hidden="true" />
        <span className="workbench-donut-label">{slice.name}</span>
        <strong>{formatNumber(slice.count, { grouping: true })} <small>件</small></strong>
        <span className="workbench-donut-share">{share(slice.count, total)}</span>
      </div>)}
    </div>
  </div>;
}

function niceStep(rough: number): number {
  const magnitude = Math.pow(10, Math.floor(Math.log10(Math.max(rough, 1))));
  const value = rough / magnitude;
  const factor = value <= 1 ? 1 : value <= 2 ? 2 : value <= 5 ? 5 : 10;
  return factor * magnitude;
}

export function WorkstationBars({ rows, total }: { rows: Distribution[]; total: number }) {
  if (total <= 0) return <div className="workbench-distribution-empty"><Empty description="该时间范围暂无来源包裹" /></div>;

  const stations = positiveRows(rows).slice(0, 4).map(row => ({ name: row.name, count: row.count }));
  const remaining = Math.max(0, total - stations.reduce((sum, row) => sum + row.count, 0));
  if (remaining > 0) stations.push({ name: stations.length ? '其他/未记录' : '未记录工作台', count: remaining });
  const step = niceStep(Math.max(...stations.map(row => row.count)) * 1.12 / 3);
  const upper = step * 3;

  return <div className="workbench-bars" role="img" aria-label={`工作台件量柱状图，共 ${total} 件；${stations.map(row => `${row.name} ${row.count} 件`).join('；')}`}>
    <div className="workbench-bars-axis" aria-hidden="true">
      {[3, 2, 1, 0].map((tick, index) => <span key={tick} style={{ top: `${index * 100 / 3}%` }}>{formatNumber(tick * step, { grouping: true })}</span>)}
    </div>
    <div className="workbench-bars-body">
      <div className="workbench-bars-grid" aria-hidden="true">
        {[0, 1, 2, 3].map(index => <span key={index} style={{ top: `${index * 100 / 3}%` }} />)}
      </div>
      <div className="workbench-bars-columns">
        {stations.map((station, index) => <div className="workbench-bars-column" key={station.name}
          title={`${station.name}：${formatNumber(station.count, { grouping: true })} 件，占总件量 ${share(station.count, total)}`}>
          <div className="workbench-bars-track">
            <strong>{formatNumber(station.count, { grouping: true })}</strong>
            <div className="workbench-bars-bar" style={{ height: `${station.count / upper * 100}%`, backgroundColor: stationColors[index % stationColors.length] }} />
          </div>
          <span className="workbench-bars-name">{station.name}</span>
          <small>{share(station.count, total)}</small>
        </div>)}
      </div>
    </div>
  </div>;
}
