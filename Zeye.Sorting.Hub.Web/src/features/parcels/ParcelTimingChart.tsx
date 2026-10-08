import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { Link } from 'react-router';
import { Button } from 'antd';
import { localTime } from '../../data/api/operationalTypes';
import { createTimingScale, formatTimingMilliseconds, formatTimingTime, timingInterval, timingPosition,
  parseTimingTime, timingReference, type TimingAxis, type TimingMark, type TimingNode, type TimingRow, type TimingScale } from './parcelTimingModel';
import { groupTimingDiagnostics, layoutTimingOverview, timingAxisTicks, timingOverview, timingOverviewSpans } from './parcelTimingPresentation';

interface ChartProps {
  rows: TimingRow[]; axis: TimingAxis; zoom: number; first: TimingNode | null; second: TimingNode | null;
  onSelect: (key: string) => void; onCompare: (first: string, second: string) => void;
  /** 自选包裹对比覆盖邻票名称，锚点在该模式下表示对比基准。 */
  rowLabels?: Record<string, string>;
}
interface PlotProps {
  row: TimingRow; scale: TimingScale; width: number; ticks: number[];
  first: TimingNode | null; second: TimingNode | null; onSelect: (key: string) => void;
}
const positionStyle = (value: number) => ({ left: value });
const rowName = (row: TimingRow) => row.relativeIndex === 0 ? '目标包裹' : row.relativeIndex < 0 ? '前 ' + -row.relativeIndex + ' 票' : '后 ' + row.relativeIndex + ' 票';
const markClass = (mark: TimingMark) => ['parcel-timing-mark', 'kind-' + mark.kind, mark.issue ? 'has-issue' : '', mark.warning ? 'has-warning' : ''].join(' ');
const markDescription = (mark: TimingMark) => [mark.title, mark.attemptNumber !== null ? '第 ' + mark.attemptNumber + ' 次' : '', mark.state, mark.source, mark.warning].filter(Boolean).join(' · ');
const missingReference = (row: TimingRow, axis: TimingAxis) => axis !== 'absolute' && timingReference(row, axis) === null;

function TimingGrid({ scale, ticks }: Pick<PlotProps, 'scale' | 'ticks'>) {
  return <>
    {ticks.map(tick => <i className="parcel-timing-gridline" key={tick} style={{ left: (tick - scale.min) / (scale.max - scale.min) * 100 + '%' }} />)}
    {scale.axis !== 'absolute' && <i className="parcel-timing-reference" style={{ left: -scale.min / (scale.max - scale.min) * 100 + '%' }} />}
  </>;
}

function TimingEndpoint({ mark, boundary, x, y, first, second, onSelect }: {
  mark: TimingMark; boundary: 'start' | 'end'; x: number; y: number;
} & Pick<PlotProps, 'first' | 'second' | 'onSelect'>) {
  const key = mark.key + ':' + boundary;
  const selected = first?.key === key ? 'A' : second?.key === key ? 'B' : '';
  const time = boundary === 'start' ? mark.start : mark.end!;
  const label = boundary === 'start' ? mark.startLabel : mark.endLabel;
  const duration = mark.end === null ? null : timingInterval(mark.start, mark.end);
  return <button type="button" className={['parcel-timing-node', boundary === 'end' ? 'parcel-timing-end' : '', selected ? 'is-selected' : ''].join(' ')}
    style={{ left: x, top: y - 8 }} onClick={() => onSelect(key)} data-node-key={key}
    aria-label={[mark.parcelId, mark.title, label, formatTimingTime(time)].join(' · ')} aria-pressed={Boolean(selected)}
    title={markDescription(mark) + '\n' + label + '：' + formatTimingTime(time) + (duration !== null ? '\n调用耗时：' + formatTimingMilliseconds(duration) : '')}>
    <span className="parcel-timing-dot">{selected}</span>
  </button>;
}

function TimingWindow({ mark, startX, endX, y }: { mark: TimingMark; startX: number; endX: number | null; y: number }) {
  return endX === null ? null : <div className="parcel-timing-bar" data-duration-ms={timingInterval(mark.start, mark.end!)}
    style={{ left: Math.min(startX, endX), top: y - 4, width: Math.abs(endX - startX) }}
    title={markDescription(mark) + ' · ' + formatTimingMilliseconds(timingInterval(mark.start, mark.end!))} />;
}

function TimingOverviewPlot({ row, scale, width, ticks, first, second, onSelect, onExpand, onCompare }: PlotProps & {
  onExpand: () => void; onCompare: ChartProps['onCompare'];
}) {
  const missing = missingReference(row, scale.axis);
  const milestones = missing ? [] : layoutTimingOverview(row, scale, width, [first, second]);
  const spans = missing ? [] : timingOverviewSpans(row);
  const diagnostics = missing ? [] : groupTimingDiagnostics(row, scale, width);
  const positions = milestones.flatMap(item => item.endX === null ? [item.startX] : [item.startX, item.endX]);
  const aX = first?.mark.parcelId === row.parcel.id ? timingPosition(first.ticks, row, scale) * width : null;
  const bX = second?.mark.parcelId === row.parcel.id ? timingPosition(second.ticks, row, scale) * width : null;
  return <div className="parcel-timing-track is-overview" style={{ width }}>
    <TimingGrid scale={scale} ticks={ticks} />
    {positions.length > 1 && <div className="parcel-timing-rail" style={{ left: Math.min(...positions), width: Math.max(...positions) - Math.min(...positions) }} />}
    {spans.map(span => {
      const startX = timingPosition(span.start.start, row, scale) * width;
      const endX = timingPosition(span.end.start, row, scale) * width;
      const duration = timingInterval(span.start.start, span.end.start);
      const description = `${span.label} · ${formatTimingMilliseconds(duration)}${span.partial ? ' · 请求起止未提供，按已记录节点展示' : ''}`;
      return <button type="button" key={span.kind} className={[
        span.kind === 'detection' ? 'parcel-timing-detection-gap' : span.kind === 'wait' ? 'parcel-timing-scan-gap' : 'parcel-timing-stage-gap',
        span.partial ? 'is-partial' : '', duration < 0 ? 'is-negative' : '',
      ].join(' ')} style={{ left: Math.min(startX, endX), width: Math.abs(endX - startX) }}
        data-duration-ms={duration} data-span-kind={span.kind} aria-label={`${row.parcel.id} · 比较${description}`}
        title={`${description}\n${formatTimingTime(span.start.start)} → ${formatTimingTime(span.end.start)}`}
        onClick={() => onCompare(span.start.key + ':start', span.end.key + ':start')} />;
    })}
    {milestones.map(({ mark, startX, endX, label, labelX, labelLane }) => <div className={markClass(mark)} key={mark.key}>
      <TimingWindow mark={mark} startX={startX} endX={endX} y={32} />
      <TimingEndpoint mark={mark} boundary="start" x={startX} y={32} first={first} second={second} onSelect={onSelect} />
      {endX !== null && <TimingEndpoint mark={mark} boundary="end" x={endX} y={32} first={first} second={second} onSelect={onSelect} />}
      {labelLane !== null && <button type="button" className="parcel-timing-direct-label" style={{ left: labelX, top: labelLane === 0 ? 3 : 39 }}
        title={markDescription(mark)} onClick={() => onSelect(mark.key + ':start')} aria-label={'选择 ' + row.parcel.id + ' 的 ' + mark.title + ' 节点'}>{label}</button>}
    </div>)}
    {diagnostics.map(group => <div className={'parcel-timing-diagnostic ' + (group.issue ? 'is-issue' : 'is-warning')} key={group.marks[0].key}>
      <i style={{ left: group.x, width: group.endX - group.x }} />
      <button type="button" style={positionStyle(group.x)} onClick={onExpand}
        aria-label={'展开 ' + row.parcel.id + ' 的 ' + group.marks.length + ' 个异常或时间提醒'}
        title={group.marks.map(markDescription).join('\n')}><span aria-hidden>!</span>{group.marks.length > 1 && <b>{group.marks.length}</b>}</button>
    </div>)}
    {!missing && aX !== null && bX !== null && <div className="parcel-timing-measure-line" style={{ left: Math.min(aX, bX), width: Math.abs(aX - bX) }} />}
    {missing ? <span className="parcel-timing-missing">缺少{scale.axis === 'detected' ? '首次检测' : '扫码'}时间，切换绝对时间查看</span>
      : !milestones.length ? <button type="button" className="parcel-timing-overview-empty" onClick={onExpand}>{row.marks.length ? '无关键节点，展开查看 ' + row.marks.length + ' 个动作' : '暂无有效时间节点'}</button> : null}
  </div>;
}

/** 完整动作使用固定名称列，每条窗口单独一行，密集重试不会迫使概览增高。 */
function TimingAction({ mark, row, scale, width, ticks, first, second, onSelect }: PlotProps & { mark: TimingMark }) {
  const missing = missingReference(row, scale.axis);
  const startX = timingPosition(mark.start, row, scale) * width;
  const endX = mark.end === null ? null : timingPosition(mark.end, row, scale) * width;
  const duration = mark.end === null ? null : timingInterval(mark.start, mark.end);
  return <div className={'parcel-timing-action ' + (mark.issue ? 'has-issue' : '')} data-mark-key={mark.key}>
    <div className={'parcel-timing-action-label kind-' + mark.kind} title={markDescription(mark)}>
      <span><i />{mark.title}{mark.attemptNumber !== null ? ' #' + mark.attemptNumber : ''}{mark.issue && !/异常|失败/.test(mark.title) ? ' · 异常' : ''}</span>
      <small>{mark.warning || [mark.state, mark.source].filter(Boolean).join(' · ')}</small>
    </div>
    <div className="parcel-timing-track is-detail" style={{ width }}>
      <TimingGrid scale={scale} ticks={ticks} />
      {missing ? <span className="parcel-timing-missing">{formatTimingTime(mark.start, true)}{mark.end !== null ? ' → ' + formatTimingTime(mark.end, true) : ''}</span> : <div className={markClass(mark)}>
        <TimingWindow mark={mark} startX={startX} endX={endX} y={24} />
        <TimingEndpoint mark={mark} boundary="start" x={startX} y={24} first={first} second={second} onSelect={onSelect} />
        {endX !== null && <TimingEndpoint mark={mark} boundary="end" x={endX} y={24} first={first} second={second} onSelect={onSelect} />}
        <span className={'parcel-timing-action-time ' + (startX > width - 115 ? 'label-left' : '')} style={positionStyle(startX)}>
          {formatTimingTime(mark.start, true)}
        </span>
        {duration !== null && <span className="parcel-timing-inline-duration" style={positionStyle(Math.max(4, Math.min(width - 128, Math.min(startX, endX!))))}>{formatTimingMilliseconds(duration)}</span>}
      </div>}
    </div>
    <div className={'parcel-timing-action-duration ' + (duration !== null && duration < 0 ? 'is-negative' : '')}>
      <span>{duration !== null ? formatTimingMilliseconds(duration) : mark.startLabel === '请求' || mark.startLabel === '开始' ? '缺少结束时间' : '时间节点'}</span>
    </div>
  </div>;
}

function TimingChartRow({ row, scale, width, ticks, first, second, onSelect, onCompare, expanded, onToggle, onExpand, label }: PlotProps & {
  onCompare: ChartProps['onCompare']; expanded: boolean; onToggle: () => void; onExpand: () => void;
  label?: string;
}) {
  const target = row.relativeIndex === 0;
  const overview = timingOverview(row);
  const compare = () => { if (overview.scan && overview.request) onCompare(overview.scan.key + ':start', overview.request.key + ':start'); };
  const value = formatTimingMilliseconds(overview.scanToRequest);
  const missing = !overview.scan ? '缺少扫码' : !overview.request ? '缺少请求时间' : '首次请求';
  const registeredAt = parseTimingTime(row.parcel.createdTime);
  const registeredLabel = localTime(formatTimingTime(registeredAt));
  return <div className={'parcel-timing-parcel ' + (target ? 'is-target' : '')}>
    <div className={'parcel-timing-chart-row ' + (target ? 'is-target' : '')} data-parcel-id={row.parcel.id}>
      <div className="parcel-timing-row-label">
        <div className="parcel-timing-row-identity"><span className={'parcel-timing-relative ' + (target ? 'is-target' : '')}>{label ?? rowName(row)}</span>
          <Link to={'/parcels/' + encodeURIComponent(row.parcel.id)} title={row.parcel.barCodes || '未提供条码'}>{row.parcel.barCodes || '未提供条码'}</Link></div>
        <span className="parcel-timing-id" title={row.parcel.id}>ID {row.parcel.id}</span>
        <span className="parcel-timing-registration" title={'Hub首次入库：' + registeredLabel + '；与来源业务发生时间分别记录'}>
          Hub入库 <time>{registeredLabel}</time>
        </span>
        <div className="parcel-timing-row-meta">
          <span className="parcel-timing-source" title={row.parcel.sourceInstanceId ?? row.parcel.workstationName}>{row.parcel.workstationName || row.parcel.sourceInstanceId || '未提供来源'}</span>
          <button type="button" onClick={onToggle} className="parcel-timing-expand" aria-expanded={expanded}
            aria-controls={'timing-actions-' + row.parcel.id} aria-label={(expanded ? '收起' : '展开') + ' ' + row.parcel.id + ' 的全部 ' + row.marks.length + ' 个动作'}>
            <span className={'parcel-timing-chevron ' + (expanded ? 'is-open' : '')} aria-hidden />{row.marks.length} 动作{overview.diagnosticCount > 0 && <b className={'parcel-timing-diagnostic-count ' + (overview.hasIssue ? 'is-issue' : 'is-warning')} title={overview.diagnosticCount + ' 个异常或时间提醒'}>! {overview.diagnosticCount}</b>}
          </button>
        </div>
        <button type="button" className="parcel-timing-mobile-metric" disabled={overview.scanToRequest === null} onClick={compare}
          aria-label={'比较 ' + row.parcel.id + ' 扫码到首次请求格口'}>扫码 → 格口 <b>{overview.scanToRequest === null ? missing : value}</b></button>
      </div>
      <TimingOverviewPlot row={row} scale={scale} width={width} ticks={ticks} first={first} second={second} onSelect={onSelect} onExpand={onExpand} onCompare={onCompare} />
      <div className={'parcel-timing-row-metric ' + (overview.scanToRequest !== null && overview.scanToRequest < 0 ? 'is-negative' : '')}>
        <button type="button" className="parcel-timing-metric-button" disabled={overview.scanToRequest === null} onClick={compare}
          aria-label={'比较 ' + row.parcel.id + ' 扫码到首次请求格口'} title="以这票包裹的扫码和首次格口请求作为节点 A、B">
          <strong>{value}</strong><span>{overview.scanToRequest !== null && overview.scanToRequest < 0 ? '请求早于扫码' : missing}</span>
        </button>
      </div>
    </div>
    {expanded && <div className="parcel-timing-actions" id={'timing-actions-' + row.parcel.id} role="group" aria-label={(label ?? rowName(row)) + '的完整动作'}>
      {row.marks.length ? row.marks.map(mark => <TimingAction key={mark.key} mark={mark} row={row} scale={scale} width={width} ticks={ticks} first={first} second={second} onSelect={onSelect} />)
        : <div className="parcel-timing-action-empty">没有可绘制的有效时间，完整记录可在包裹详情查看。</div>}
    </div>}
  </div>;
}

/** 单实例、最多11票的HTML时间图；原始精度、窗口端点与URL缩放沿用现有数据模型。 */
export function ParcelTimingChart({ rows, axis, zoom, first, second, onSelect, onCompare, rowLabels }: ChartProps) {
  const viewport = useRef<HTMLDivElement>(null);
  const targetId = rows.find(row => row.relativeIndex === 0)?.parcel.id;
  const [availableWidth, setAvailableWidth] = useState(1000);
  const [expansion, setExpansion] = useState<{ targetId: string | undefined; ids: string[] }>({ targetId, ids: [] });
  const expanded = new Set(expansion.targetId === targetId ? expansion.ids : []);
  const allExpanded = rows.length > 0 && rows.every(row => expanded.has(row.parcel.id));
  const changeExpansion = (id: string, forceOpen = false) => setExpansion(previous => {
    const ids = new Set(previous.targetId === targetId ? previous.ids : []);
    if (!forceOpen && ids.has(id)) ids.delete(id); else ids.add(id);
    return { targetId, ids: [...ids] };
  });
  const locateTarget = () => {
    const element = viewport.current;
    const target = element?.querySelector<HTMLElement>('.parcel-timing-chart-row.is-target');
    if (element && target) element.scrollTop += target.getBoundingClientRect().top - element.getBoundingClientRect().top - (element.clientHeight - target.offsetHeight) / 2;
  };
  useEffect(() => {
    const element = viewport.current;
    if (!element) return;
    const observer = new ResizeObserver(entries => setAvailableWidth(entries[0].contentRect.width));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  const narrow = availableWidth < 640;
  const labelWidth = narrow ? 176 : 252;
  const metricWidth = narrow ? 0 : 148;
  const width = Math.max(520, Math.floor(availableWidth - labelWidth - metricWidth - 2)) * zoom;
  const scale = useMemo(() => createTimingScale(rows, axis), [rows, axis]);
  const ticks = useMemo(() => timingAxisTicks(scale, width), [scale, width]);
  useEffect(locateTarget, [targetId, narrow]);
  useEffect(() => {
    const element = viewport.current;
    const target = rows.find(row => row.relativeIndex === 0);
    if (!element || !target || !narrow || zoom !== 1) return;
    const overview = timingOverview(target);
    const times = [overview.detected?.start, overview.scan?.start].filter((time): time is bigint => time !== undefined);
    if (missingReference(target, axis) || !times.length) return;
    const xs = times.map(time => timingPosition(time, target, scale) * width);
    const visible = Math.max(1, element.clientWidth - labelWidth - metricWidth);
    element.scrollLeft = Math.max(0, (Math.min(...xs) + Math.max(...xs)) / 2 - visible / 2);
  }, [rows, scale, width, narrow, zoom, availableWidth, labelWidth, metricWidth]);
  const timeAt = (offset: number) => scale.origin + BigInt(Math.round(offset * 10000));
  const subMillisecond = scale.max - scale.min < 2;
  const columnStyle = { '--timing-label-width': labelWidth + 'px', '--timing-metric-width': metricWidth + 'px' } as CSSProperties;
  return <>
    <div className="parcel-timing-axis-context">
      <span className="parcel-timing-axis-note">{axis === 'absolute'
        ? '本地时间 ' + formatTimingTime(timeAt(scale.min)) + ' — ' + formatTimingTime(timeAt(scale.max)) + (subMillisecond ? '（刻度为相对最早节点的毫秒）' : '')
        : (axis === 'detected' ? '首次检测' : '扫码') + ' = 0 ms · 对比每票的处理节奏；A/B 间隔仍按实际时间计算'}</span>
      <div className="parcel-timing-chart-tools">
        {narrow && <span className="parcel-timing-pan-hint">左右滑动时间轴</span>}
        <Button size="small" type="text" onClick={() => setExpansion({ targetId, ids: allExpanded ? [] : rows.map(row => row.parcel.id) })}>{allExpanded ? '收起全部动作' : '展开全部动作'}</Button>
        <Button size="small" type="text" onClick={locateTarget}>{rowLabels ? '定位基准' : '定位目标'}</Button>
      </div>
    </div>
    <div className={'parcel-timing-chart-viewport ' + (narrow ? 'is-narrow' : '')} ref={viewport} role="region" aria-label={rowLabels ? '包裹对比动作甘特图' : '包裹动作时序甘特图'} tabIndex={0} style={columnStyle}>
      <div className="parcel-timing-chart-canvas" style={{ width: width + labelWidth + metricWidth }}>
        <div className="parcel-timing-chart-axis">
          <div className="parcel-timing-axis-label">包裹 / 条码<span>点击动作数展开</span></div>
          <div className="parcel-timing-ticks" style={{ width }}>
            {ticks.map(tick => <span key={tick} style={{ left: Math.max(5, Math.min(width - 5, (tick - scale.min) / (scale.max - scale.min) * width)),
              transform: (tick - scale.min) / (scale.max - scale.min) * width < 55 ? 'none' : (tick - scale.min) / (scale.max - scale.min) * width > width - 55 ? 'translateX(-100%)' : 'translateX(-50%)' }}
              title={axis === 'absolute' ? formatTimingTime(timeAt(tick)) : formatTimingMilliseconds(tick)}>
              {axis === 'absolute' && !subMillisecond ? formatTimingTime(timeAt(tick), true) : formatTimingMilliseconds(tick)}
            </span>)}
          </div>
          <div className="parcel-timing-metric-heading">扫码 → 首次请求<span>点击耗时进行比较</span></div>
        </div>
        {rows.map(row => <TimingChartRow key={row.parcel.id} row={row} scale={scale} width={width} ticks={ticks} first={first} second={second}
          label={rowLabels?.[row.parcel.id]}
          onSelect={onSelect} onCompare={onCompare} expanded={expanded.has(row.parcel.id)}
          onToggle={() => changeExpansion(row.parcel.id)} onExpand={() => changeExpansion(row.parcel.id, true)} />)}
      </div>
    </div>
  </>;
}
