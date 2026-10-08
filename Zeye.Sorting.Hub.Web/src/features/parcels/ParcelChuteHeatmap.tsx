import { Alert, Button, Empty, Grid, Input, Segmented, Select, Skeleton, Tooltip } from 'antd';
import { CheckOutlined, SearchOutlined } from '@ant-design/icons';
import { useMemo, useState } from 'react';
import { SectionCard } from '../../components/SectionCard';
import { formatNumber } from '../../data/formatNumber';
import type { ParcelAnalysis } from '../../data/api/parcelAnalysisTypes';
import type { AnalysisFilters } from './parcelAnalysisModel';
import { chuteHeatmapDrillFilters, chuteHeatmapLevel, compareChuteCodes, groupChuteHeatmapCells,
  type ChuteHeatmapKind, type ChuteHeatmapMetric } from './parcelChuteHeatmapModel';
import './parcelChuteHeatmap.css';

const count = (value: number) => formatNumber(value, { grouping: true });
const metricLabels: Record<ChuteHeatmapMetric, string> = { count: '包裹量', mismatchCount: '编码不一致', fallbackCount: '兜底使用' };
const groupLabel = (group: { workstationName: string; sourceInstanceId: string | null }) =>
  `${group.workstationName || '未提供工作台'} / ${group.sourceInstanceId || '未提供来源实例'}`;

/** React 管理有界 DOM 热力单元；颜色只表达票数，选择用独立轮廓及勾选标记表达。 */
export function ParcelChuteHeatmap({ data, loading, unavailable, params, filters, onViewChange, onDrill }: {
  data: ParcelAnalysis | undefined; loading: boolean; unavailable: boolean; params: URLSearchParams; filters: AnalysisFilters;
  onViewChange: (changes: Record<string, string | null>) => void; onDrill: (changes: Record<string, string | null>) => void;
}) {
  const kind: ChuteHeatmapKind = params.get('heatmapKind') === 'target' ? 'target' : 'actual';
  const { md } = Grid.useBreakpoint();
  const batchSize = md ? 80 : 24;
  const requestedMetric = params.get('heatmapMetric');
  const metric: ChuteHeatmapMetric = requestedMetric === 'mismatchCount' || requestedMetric === 'fallbackCount' ? requestedMetric : 'count';
  const sort = params.get('heatmapSort') === 'heat' ? 'heat' : 'code';
  const search = params.get('heatmapSearch') ?? '';
  const report = kind === 'actual' ? data?.actualChuteHeatmap : data?.targetChuteHeatmap;
  const groups = useMemo(() => groupChuteHeatmapCells(report?.cells ?? []), [report]);
  const requestedScope = params.get('heatmapScope');
  const scope = requestedScope === 'all' ? 'all' : groups.find(group => group.key === requestedScope)?.key ?? groups[0]?.key;
  const scopeGroups = groups.filter(group => scope === 'all' || group.key === scope);
  const scopeCells = scopeGroups.flatMap(group => group.cells);
  const maximum = Math.max(0, ...scopeCells.map(cell => cell[metric]));
  const peak = scopeCells.find(cell => cell[metric] === maximum && maximum > 0);
  const matchingGroups = scopeGroups.map(group => ({ ...group, cells: group.cells
    .filter(cell => cell.chuteCode.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()))
    .sort((a, b) => (sort === 'heat' ? b[metric] - a[metric] : 0) || compareChuteCodes(a.chuteCode, b.chuteCode)) }));
  const matchingCount = matchingGroups.reduce((sum, group) => sum + group.cells.length, 0);
  const displayKey = JSON.stringify([kind, metric, scope, search, sort, batchSize]);
  const [expanded, setExpanded] = useState({ key: '', limit: 80 });
  let remaining = expanded.key === displayKey ? expanded.limit : batchSize;
  const shownGroups = matchingGroups.map(group => {
    const cells = group.cells.slice(0, remaining); remaining = Math.max(0, remaining - cells.length);
    return { ...group, cells };
  }).filter(group => group.cells.length);
  const shownCount = shownGroups.reduce((sum, group) => sum + group.cells.length, 0);
  const direction = kind === 'actual' ? '实际' : '目标';

  return <SectionCard title="格口热力图" className="parcel-chute-heatmap" extra={<span className="text-muted">颜色越深，票数越多</span>}>
    <div className="chute-heatmap-controls">
      <Segmented aria-label="热力图格口方向" value={kind} options={[{ label: '实际格口', value: 'actual' }, { label: '目标格口', value: 'target' }]}
        onChange={value => onViewChange({ heatmapKind: String(value) })} />
      <Segmented aria-label="热力图指标" value={metric} options={Object.entries(metricLabels).map(([value, label]) => ({ value, label }))}
        onChange={value => onViewChange({ heatmapMetric: String(value) })} />
      <label className="chute-heatmap-scope"><span>工作台 / 来源</span><Select aria-label="热力图工作台与来源" value={scope} showSearch optionFilterProp="label"
        disabled={loading || !groups.length} onChange={value => onViewChange({ heatmapScope: value })}
        options={[{ value: 'all', label: '全部工作台与来源（分组展示）' }, ...groups.map(group => ({ value: group.key, label: groupLabel(group) }))]} /></label>
    </div>
    {loading && !data ? <Skeleton active paragraph={{ rows: 5 }} /> : !report
      ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={unavailable ? '热力图数据暂不可用，请重试' : '热力图数据暂不可用，请刷新分析'} />
      : <>
        {report.truncated && <Alert showIcon type="info" message="格口数量超过返回预算，当前仅展示总票数最高的部分格口。各格口票数为完整聚合；请缩小日期或来源查看完整分布。" />}
        <div className="chute-heatmap-reading">
          <div><span>{report.truncated ? '已返回格口' : '已记录格口'}</span><strong>{count(scopeCells.length)}<small>个</small></strong></div>
          <div><span>本视图{metricLabels[metric]}</span><strong>{count(scopeCells.reduce((sum, cell) => sum + cell[metric], 0))}<small>票</small></strong></div>
          <div className="chute-heatmap-peak"><span>最高{metricLabels[metric]}格口</span><strong title={peak ? `${groupLabel(peak)} · ${peak.chuteCode}` : undefined}>{peak?.chuteCode ?? '—'}<small>{peak ? `${count(maximum)} 票` : '当前指标均为 0 票'}</small></strong></div>
        </div>
        <div className="chute-heatmap-tools">
          <div className="chute-heatmap-legend" data-metric={metric} aria-label={`线性色阶，0 至 ${maximum} 票`}>
            <span>0 票</span><i data-level="0" />{maximum > 0 && <>{[1, 2, 3, 4, 5].map(level => <i data-level={level} key={level} />)}<span>{count(maximum)} 票</span></>}
            <small>{maximum > 0 ? '线性刻度' : '当前指标均为 0 票'}</small>
          </div>
          <div className="chute-heatmap-tools-inputs">
            <Input aria-label="搜索热力图格口" prefix={<SearchOutlined aria-hidden />} placeholder="搜索格口编码" value={search} allowClear maxLength={128}
              onChange={event => onViewChange({ heatmapSearch: event.target.value || null })} />
            <Select aria-label="热力图排序" value={sort} options={[{ value: 'code', label: '按格口编码' }, { value: 'heat', label: '按热度降序' }]}
              onChange={value => onViewChange({ heatmapSort: value })} />
          </div>
        </div>
        {matchingCount === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={report.cells.length ? '没有匹配的格口编码' : `该范围暂无已记录的${direction}格口`} />
          : <div className="chute-heatmap-groups" data-metric={metric} aria-label={`${direction}格口${metricLabels[metric]}热力图`}>
            {shownGroups.map(group => <section className="chute-heatmap-group" key={group.key} aria-label={groupLabel(group)}>
              <div className="chute-heatmap-group-heading"><strong>{group.workstationName || '未提供工作台'}</strong><span>{group.sourceInstanceId || '未提供来源实例'}</span></div>
              <div className="chute-heatmap-grid">{group.cells.map(cell => {
                const drill = chuteHeatmapDrillFilters(cell, kind, metric);
                const selected = filters.workstationName === cell.workstationName && filters.sourceInstanceId === cell.sourceInstanceId
                  && (kind === 'actual' ? filters.actualChuteCode === cell.chuteCode && !filters.targetChuteCode : filters.targetChuteCode === cell.chuteCode && !filters.actualChuteCode)
                  && filters.mismatchOnly === (metric === 'mismatchCount') && filters.fallbackOnly === (metric === 'fallbackCount');
                return <Tooltip key={cell.chuteCode} title={<div>原始编码：{JSON.stringify(cell.chuteCode)}<br />全部 {count(cell.count)} 票 · 编码不一致 {count(cell.mismatchCount)} 票 · 兜底 {count(cell.fallbackCount)} 票
                  {!drill && <><br />{cell[metric] === 0 ? '该指标为 0 票' : '来源或工作台信息不足，无法精确下钻'}</>}</div>}>
                  <button type="button" className={`chute-heatmap-cell${selected ? ' is-selected' : ''}`} data-code={cell.chuteCode}
                    data-level={chuteHeatmapLevel(cell[metric], maximum)} disabled={!drill || loading} aria-pressed={selected}
                    aria-label={`${groupLabel(cell)}，${direction}格口 ${cell.chuteCode}，${metricLabels[metric]} ${cell[metric]} 票，全部 ${cell.count} 票，编码不一致 ${cell.mismatchCount} 票，兜底 ${cell.fallbackCount} 票`}
                    onClick={() => { if (drill) onDrill(drill); }}>
                    <span className="chute-heatmap-code" title={cell.chuteCode}>格口 {cell.chuteCode}</span>
                    <strong>{count(cell[metric])}<small>票</small></strong>
                    <span className="chute-heatmap-cell-caption">{metric === 'count' ? `不一致 ${count(cell.mismatchCount)} · 兜底 ${count(cell.fallbackCount)}` : `全部 ${count(cell.count)} 票`}</span>
                    {selected && <CheckOutlined className="chute-heatmap-selected-mark" aria-hidden />}
                  </button>
                </Tooltip>;
              })}</div>
            </section>)}
          </div>}
        {matchingCount > 0 && <div className="chute-heatmap-pagination"><span>已显示 {count(shownCount)} / {count(matchingCount)} 个格口</span>
          {shownCount < matchingCount && <Button onClick={() => setExpanded({ key: displayKey, limit: shownCount + batchSize })}>显示更多格口</Button>}</div>}
        <p className="chute-heatmap-note">点击格口查看对应指标的包裹。色阶以当前所选工作台 / 来源中已返回格口的最高票数为上限，搜索不改变色阶。</p>
        <p className="chute-heatmap-note">当前查询范围：{direction}编码已提供 {count(report.sampleCount)} 票，缺失 {count(report.missingCodeCount)} 票。仅展示已出现的原始编码；排列不代表设备物理位置。</p>
      </>}
  </SectionCard>;
}
