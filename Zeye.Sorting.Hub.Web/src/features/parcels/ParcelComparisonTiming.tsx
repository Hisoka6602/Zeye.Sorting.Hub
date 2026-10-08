import { useMemo, useState } from 'react';
import { Button, Segmented, Select, Space } from 'antd';
import { SectionCard } from '../../components/SectionCard';
import { ParcelTimingChart } from './ParcelTimingChart';
import { comparisonIntervals } from './parcelComparisonModel';
import { ComparisonDifference, comparisonColor, comparisonLabel } from './ParcelComparisonMeasurements';
import { formatTimingMilliseconds, formatTimingTime, parcelTimingNodes, timingInterval, timingKinds, type TimingAxis, type TimingRow } from './parcelTimingModel';

/** 自选包裹共用真实时间图；阶段耗时与任意A/B实际节点间隔分别呈现。 */
export function ParcelComparisonTiming({ rows, ids, baselineId, axis, zoom, onAxis, onZoom }: {
  rows: TimingRow[]; ids: string[]; baselineId: string; axis: TimingAxis; zoom: number;
  onAxis: (axis: TimingAxis) => void; onZoom: (zoom: number) => void;
}) {
  const [selection, setSelection] = useState<{ first: string | null; second: string | null }>({ first: null, second: null });
  const nodes = useMemo(() => parcelTimingNodes(rows), [rows]);
  const first = nodes.find(node => node.key === selection.first) ?? null;
  const second = nodes.find(node => node.key === selection.second) ?? null;
  const interval = first && second ? timingInterval(first.ticks, second.ticks) : null;
  const nodeOptions = nodes.map(node => ({ value: node.key, label: `${comparisonLabel(node.mark.parcelId, ids)} · ${node.mark.title} · ${node.label} · ${formatTimingTime(node.ticks, true)}`, title: `${node.mark.parcelId} · ${node.mark.title} · ${formatTimingTime(node.ticks)}` }));
  const selectNode = (key: string) => setSelection(previous => ({ first: first && !second && first.key !== key ? previous.first : key, second: first && !second && first.key !== key ? key : null }));
  const rowLabels = Object.fromEntries(rows.map(row => [row.parcel.id, comparisonLabel(row.parcel.id, ids) + (row.parcel.id === baselineId ? ' · 基准' : '')]));
  const intervals = rows.map(row => comparisonIntervals(row));
  const baselineIndex = rows.findIndex(row => row.parcel.id === baselineId);
  const invalidTimes = rows.reduce((total, row) => total + row.invalidTimeCount, 0);
  return <>
    <SectionCard title="动作时序对比" className="parcel-timing-chart-card">
      <div className="parcel-timing-toolbar">
        <Segmented aria-label="时间轴模式" value={axis} onChange={value => onAxis(value as TimingAxis)} options={[{ value: 'absolute', label: '绝对时间' }, { value: 'detected', label: '检测对齐' }, { value: 'scan', label: '扫码对齐' }]} />
        <Space><span className="text-muted">缩放</span><Segmented aria-label="图表缩放" value={zoom} onChange={value => onZoom(Number(value))} options={[{ value: 1, label: '1×' }, { value: 2, label: '2×' }, { value: 4, label: '4×' }]} /></Space>
      </div>
      <div className="parcel-comparison-node-controls">
        <label>节点 A<Select aria-label="对比节点 A" showSearch allowClear optionFilterProp="label" value={first?.key} placeholder="点选图中节点或搜索动作" options={nodeOptions} onChange={value => setSelection(previous => ({ ...previous, first: value ?? null }))} /></label>
        <div className={'parcel-timing-selection-value ' + (interval !== null && interval < 0 ? 'is-negative' : '')} role="status"><span>实际时间 B − A</span><strong>{formatTimingMilliseconds(interval)}</strong></div>
        <label>节点 B<Select aria-label="对比节点 B" showSearch allowClear optionFilterProp="label" value={second?.key} placeholder="可跨包裹选择" options={nodeOptions} onChange={value => setSelection(previous => ({ ...previous, second: value ?? null }))} /></label>
        <Button onClick={() => setSelection({ first: null, second: null })}>清除节点</Button>
      </div>
      {(first || second) && <p className="parcel-comparison-note parcel-comparison-node-details">{[first, second].map((node, index) => node && <span key={index}>{index === 0 ? 'A' : 'B'}：{comparisonLabel(node.mark.parcelId, ids)} · {node.mark.title} · {formatTimingTime(node.ticks)}{node.mark.warning && ' · ' + node.mark.warning}</span>)}</p>}
      <div className="parcel-timing-legend">{timingKinds.map(kind => <span key={kind.key} className={`kind-${kind.key}`}><i />{kind.label}</span>)}<span className="legend-issue"><i />失败 / 异常</span><span>● 节点　□ 响应　! 时间提醒</span></div>
      <ParcelTimingChart rows={rows} rowLabels={rowLabels} axis={axis} zoom={zoom} first={first} second={second} onSelect={selectNode} onCompare={(a, b) => setSelection({ first: a, second: b })} />
      <p className="parcel-comparison-note">按选择顺序展示。对齐模式以每票自己的检测 / 扫码时间为 0，A/B 始终计算实际时间差；跨设备比较需核对来源时钟。点击动作数展开完整动作，缺失请求或响应时不补造窗口。{invalidTimes > 0 && ` ${invalidTimes} 个无效或缺失时间未绘入图中。`}</p>
    </SectionCard>
    <SectionCard title="关键阶段耗时" extra={<span className="text-muted">毫秒 · Δ 相对 {comparisonLabel(baselineId, ids)}</span>}>
      <div className="parcel-comparison-table-scroll" tabIndex={0} role="region" aria-label="关键阶段耗时对比表">
        <table className="parcel-comparison-table parcel-comparison-duration-table">
          <caption>真实节点的时间差；未提供端点或标记时间不可靠时显示未提供，负值表示节点顺序倒置。</caption>
          <thead><tr><th scope="col">阶段</th>{rows.map(row => <th key={row.parcel.id} scope="col" className={row.parcel.id === baselineId ? 'is-baseline' : ''} style={{ color: comparisonColor(row.parcel.id, ids) }}>{rowLabels[row.parcel.id]}</th>)}</tr></thead>
          <tbody>{intervals[0]?.map((stage, stageIndex) => <tr key={stage.label}><th scope="row">{stage.label}</th>{rows.map((row, index) => {
            const value = intervals[index][stageIndex].value;
            return <td key={row.parcel.id} className={row.parcel.id === baselineId ? 'is-baseline' : ''}><b className={value !== null && value < 0 ? 'is-negative' : ''}>{value === null ? '未提供有效端点' : formatTimingMilliseconds(value)}</b>
              <ComparisonDifference value={value} baseline={intervals[baselineIndex]?.[stageIndex].value ?? null} unit="ms" isBaseline={row.parcel.id === baselineId} /></td>;
          })}</tr>)}</tbody>
        </table>
      </div>
      <p className="parcel-comparison-note">显示首次格口请求，保留失败尝试。阶段间隔不以 Hub 入库时间代替业务时间；真实 0 ms 会保留，负值表示结束节点早于开始节点。</p>
    </SectionCard>
  </>;
}
