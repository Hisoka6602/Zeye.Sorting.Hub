import { Alert, Button, Collapse, Empty, Input, Modal, Select, Segmented, Skeleton, Space, Tag, Typography } from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestHttpApi } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import { localTime } from '../../data/api/operationalTypes';
import type { ParcelTiming, ParcelTimingCandidate, ParcelTimingCandidates } from '../../data/api/parcelTimingTypes';
import { ParcelTimingChart } from './ParcelTimingChart';
import { timingOverview } from './parcelTimingPresentation';
import { buildParcelTimingRows, formatTimingMilliseconds, formatTimingTime, parcelTimingNodes, parseTimingTime, timingInterval,
  timingKinds, validTimingParcelId, type TimingAxis, type TimingMark, type TimingNode } from './parcelTimingModel';
import './parcelTiming.css';

type SearchBy = 'auto' | 'id' | 'barcode';
interface CandidateSearch { query: string; searchBy: SearchBy }
interface NodeSelection { anchorId: string; first: string | null; second: string | null }
/** 统一展示格式与原始精度的节点计算分开。 */
const displayTimingTime = (value: bigint | null) => localTime(formatTimingTime(value));
const relativeParcelLabel = (index: number) => index === 0 ? '目标包裹' : `${index < 0 ? '前' : '后'} ${Math.abs(index)} 票`;
const nodeActionLabel = (node: TimingNode) => `${node.label}${node.mark.attemptNumber !== null ? ` · 第 ${node.mark.attemptNumber} 次` : ''}`;

/** 精确查询、重复条码确认、邻近11票甘特图与真实节点间隔共用一个页面。 */
export function ParcelTimingPage() {
  const [params, setParams] = useSearchParams();
  const anchorId = params.get('id');
  const axis: TimingAxis = params.get('axis') === 'scan' ? 'scan' : params.get('axis') === 'detected' ? 'detected' : 'absolute';
  const zoom = ['2', '4'].includes(params.get('zoom') ?? '') ? Number(params.get('zoom')) : 1;
  const [query, setQuery] = useState(anchorId ?? '');
  const [searchBy, setSearchBy] = useState<SearchBy>('auto');
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [emptyQuery, setEmptyQuery] = useState<string | null>(null);
  const [candidates, setCandidates] = useState<ParcelTimingCandidates | null>(null);
  const [candidateSearch, setCandidateSearch] = useState<CandidateSearch | null>(null);
  const [selection, setSelection] = useState<NodeSelection | null>(null);
  const searchController = useRef<AbortController | null>(null);
  useEffect(() => () => searchController.current?.abort(), []);
  const validId = validTimingParcelId(anchorId);
  const resource = useApiResource<ParcelTiming>(validId ? `/api/parcels/timing/${encodeURIComponent(anchorId!)}` : null, requestHttpApi, false, true);
  const data = resource.data?.anchorId === anchorId ? resource.data : undefined;
  const rows = useMemo(() => data ? buildParcelTimingRows(data) : [], [data]);
  const nodes = useMemo(() => parcelTimingNodes(rows), [rows]);
  const nodeMap = useMemo(() => new Map(nodes.map(node => [node.key, node])), [nodes]);
  const rowMap = useMemo(() => new Map(rows.map(row => [row.parcel.id, row])), [rows]);
  const anchorRow = rows.find(row => row.parcel.id === anchorId);
  const anchorOverview = anchorRow ? timingOverview(anchorRow) : null;
  const defaultFirst = anchorOverview?.detected;
  const defaultSecond = anchorOverview?.landing;
  const currentSelection = selection?.anchorId === anchorId ? selection : {
    anchorId: anchorId ?? '', first: defaultFirst ? `${defaultFirst.key}:start` : null, second: defaultSecond ? `${defaultSecond.key}:start` : null,
  };
  const first = currentSelection.first ? nodeMap.get(currentSelection.first) ?? null : null;
  const second = currentSelection.second ? nodeMap.get(currentSelection.second) ?? null : null;
  const interval = first && second ? timingInterval(first.ticks, second.ticks) : null;
  const allMarks = rows.flatMap(row => row.marks);
  const invalidTimes = rows.reduce((count, row) => count + row.invalidTimeCount, 0);
  const nodeOptions = useMemo(() => {
    const byParcel = new Map<string, TimingNode[]>();
    for (const node of nodes) {
      const group = byParcel.get(node.mark.parcelId);
      if (group) group.push(node); else byParcel.set(node.mark.parcelId, [node]);
    }
    return rows.flatMap(row => {
      const parcelNodes = byParcel.get(row.parcel.id);
      if (!parcelNodes?.length) return [];
      const context = relativeParcelLabel(row.relativeIndex);
      return [{ label: <div className="parcel-timing-node-group">
        <div><b>{context}</b><span title={row.parcel.barCodes || '未提供条码'}>{row.parcel.barCodes || '未提供条码'}</span></div>
        <small>ID {row.parcel.id}</small>
      </div>, options: parcelNodes.map(node => ({ value: node.key,
        label: `${nodeActionLabel(node)} · ${formatTimingTime(node.ticks, true)} · ${context}`,
        searchText: `${context} ${row.parcel.barCodes ?? ''} ${row.parcel.id} ${nodeActionLabel(node)} ${displayTimingTime(node.ticks)}`,
      })) }];
    });
  }, [nodes, rows]);
  const renderNodeLabel = ({ value, label }: { value?: string | number; label?: ReactNode }) => {
    const node = nodeMap.get(String(value));
    const row = node && rowMap.get(node.mark.parcelId);
    if (!node || !row) return label;
    return <span className="parcel-timing-node-value" title={`${row.parcel.barCodes || '未提供条码'} · ID ${row.parcel.id} · ${nodeActionLabel(node)} · ${displayTimingTime(node.ticks)}`}>
      <small>{row.relativeIndex === 0 ? '目标' : relativeParcelLabel(row.relativeIndex)}</small>
      <b>{nodeActionLabel(node)}</b><time>{formatTimingTime(node.ticks, true)}</time>
    </span>;
  };
  const renderNodeOption = ({ value, label }: { value?: string | number | null; label?: ReactNode }) => {
    const node = nodeMap.get(String(value));
    return node ? <div className="parcel-timing-node-option"><b>{nodeActionLabel(node)}</b><time>{displayTimingTime(node.ticks)}</time></div> : label;
  };
  const updateParam = (name: string, value: string | null) => setParams(previous => {
    const next = new URLSearchParams(previous);
    if (value === null) next.delete(name); else next.set(name, value);
    return next;
  });
  const chooseParcel = (id: string) => {
    searchController.current?.abort(); setSearching(false); setCandidates(null); setSearchError(null); setEmptyQuery(null);
    setSelection(null); updateParam('id', id);
    if (id === anchorId) resource.refresh();
  };
  const search = async (input: CandidateSearch, pageNumber = 1, fresh = true) => {
    const text = input.query.trim();
    if (!text) { setSearchError('请输入包裹 ID 或完整条码。'); return; }
    searchController.current?.abort();
    const controller = new AbortController(); searchController.current = controller;
    setSearching(true); setSearchError(null); setEmptyQuery(null);
    if (fresh) { setCandidates(null); setCandidateSearch({ ...input, query: text }); updateParam('id', null); setSelection(null); }
    try {
      const result = await requestHttpApi<ParcelTimingCandidates>(`/api/parcels/timing/candidates?${new URLSearchParams({ query: text, searchBy: input.searchBy, pageNumber: String(pageNumber) })}`, controller.signal);
      if (controller.signal.aborted) return;
      if (!result.totalCount) { setEmptyQuery(text); setCandidates(null); }
      else if (result.totalCount === 1 && result.items[0]) chooseParcel(result.items[0].id);
      else setCandidates(result);
    } catch (error) {
      if (!controller.signal.aborted) setSearchError(error instanceof Error ? error.message : '查询失败，请重试。');
    } finally { if (!controller.signal.aborted) setSearching(false); }
  };
  const submit = (event: FormEvent) => { event.preventDefault(); void search({ query, searchBy }); };
  const selectNode = (key: string) => setSelection({ anchorId: anchorId ?? '',
    first: first && !second && first.key !== key ? first.key : key,
    second: first && !second && first.key !== key ? key : null });
  const setNode = (boundary: 'first' | 'second', value: string | undefined) => setSelection({ anchorId: anchorId ?? '',
    first: first?.key ?? null, second: second?.key ?? null, [boundary]: value ?? null });
  const compareNodes = (firstKey: string, secondKey: string) => setSelection({ anchorId: anchorId ?? '', first: firstKey, second: secondKey });
  const resetComparison = () => setSelection({ anchorId: anchorId ?? '',
    first: defaultFirst ? defaultFirst.key + ':start' : null, second: defaultSecond ? defaultSecond.key + ':start' : null });
  const compareScanToRequest = () => setSelection({ anchorId: anchorId ?? '',
    first: anchorOverview?.scan ? anchorOverview.scan.key + ':start' : null,
    second: anchorOverview?.request ? anchorOverview.request.key + ':start' : null });
  const compactNodeDescription = (node: TimingNode | null) => node ? <span>
    <b>{node.label}</b><small>{formatTimingTime(node.ticks, true)} · {node.mark.parcelId === anchorId ? '目标包裹' : 'ID ' + node.mark.parcelId}</small>
  </span> : <span><b>选择节点</b><small>点击图中节点或使用下方选择器</small></span>;
  const nodeDescription = (node: TimingNode | null) => node ? <>
    <b>{node.label}</b><span>ID {node.mark.parcelId}</span><time>{displayTimingTime(node.ticks)}</time>
    <span>{node.mark.source}{node.mark.warning ? ` · ${node.mark.warning}` : ''}</span>
  </> : <span className="text-muted">点选图中的节点或从上方选择。</span>;

  return <div className="parcel-timing-page">
    <PageIntro title="包裹时序" description="定位一票包裹，对照前后各 5 票从首次检测到落格回传的动作时序，分析检测、扫码、请求格口等节点的时间间隔。" />
    <SectionCard className="parcel-timing-search-card">
      <form className="parcel-timing-search" onSubmit={submit}>
        <label htmlFor="parcel-timing-query">包裹 ID / 条码</label>
        <Select aria-label="查询方式" value={searchBy} onChange={setSearchBy} options={[{ value: 'auto', label: '自动识别' }, { value: 'id', label: '包裹 ID' }, { value: 'barcode', label: '完整条码' }]} />
        <Input id="parcel-timing-query" value={query} onChange={event => setQuery(event.target.value)} placeholder="输入包裹 ID 或完整条码" allowClear maxLength={1024} />
        <Button type="primary" htmlType="submit" aria-label="查询时序" loading={searching} icon={<SearchOutlined aria-hidden />}>查询时序</Button>
      </form>
      <p className="parcel-timing-hint">按完整条码精确查询；重复条码或数字条码与 ID 同时命中时，请选择对应包裹。</p>
    </SectionCard>
    {searchError && <Alert type="error" showIcon message="包裹查询失败" description={searchError} action={<Button onClick={() => void search(candidateSearch ?? { query, searchBy }, candidates?.pageNumber ?? 1, !candidates)}>重试</Button>} />}
    {emptyQuery && <Alert type="info" showIcon message={`未找到“${emptyQuery}”对应的包裹`} description="请检查包裹 ID 或完整条码。" />}
    {anchorId && !validId && <Alert type="error" showIcon message="链接中的包裹 ID 无效" description="请重新输入包裹 ID 或完整条码查询。" />}
    {validId && resource.error && <Alert type="error" showIcon message={data ? '时序刷新失败' : '时序加载失败'} description={resource.error.message} action={<Button onClick={resource.refresh}>重试</Button>} />}
    {validId && !data && (resource.loading || !resource.error) ? <SectionCard title="动作时序"><Skeleton active paragraph={{ rows: 11 }} /></SectionCard> : data ? <>
      <SectionCard title={<Space wrap><span>动作时序</span><Tag color="blue">前 {data.beforeCount} 票 + 目标包裹 + 后 {data.afterCount} 票</Tag><span className="text-muted parcel-timing-count">共 {data.items.length} 票</span></Space>}
        extra={<Button aria-label="刷新" icon={<ReloadOutlined aria-hidden />} loading={resource.loading} onClick={resource.refresh}>刷新</Button>} className="parcel-timing-chart-card">
        <div className="parcel-timing-toolbar">
          <Segmented aria-label="时间轴模式" value={axis} onChange={value => updateParam('axis', String(value))} options={[{ value: 'absolute', label: '绝对时间' }, { value: 'detected', label: '检测对齐' }, { value: 'scan', label: '扫码对齐' }]} />
          <Space><span className="text-muted">缩放</span><Segmented aria-label="图表缩放" value={zoom} onChange={value => updateParam('zoom', String(value))} options={[{ value: 1, label: '1×' }, { value: 2, label: '2×' }, { value: 4, label: '4×' }]} /></Space>
        </div>
        <div className="parcel-timing-selection" role="group" aria-label="当前节点比较">
          <div className="parcel-timing-selection-node"><i aria-hidden>A</i>{compactNodeDescription(first)}</div>
          <div className={`parcel-timing-selection-value ${interval !== null && interval < 0 ? 'is-negative' : ''}`}><span>节点 B − A{interval !== null && interval < 0 ? ' · B 早于 A' : ''}</span><strong>{formatTimingMilliseconds(interval)}</strong></div>
          <div className="parcel-timing-selection-node is-second"><i aria-hidden>B</i>{compactNodeDescription(second)}</div>
          <div className="parcel-timing-comparison-presets">
            <Button size="small" type="text" onClick={resetComparison}>检测 → {anchorOverview?.landing?.title === '处理完成' ? '完成' : '落格'}</Button>
            <Button size="small" type="text" onClick={compareScanToRequest}>扫码 → 首次请求</Button>
          </div>
        </div>
        <div className="parcel-timing-legend">{timingKinds.map(kind => <span key={kind.key} className={`kind-${kind.key}`}><i />{kind.label}</span>)}<span className="legend-issue"><i />失败 / 异常</span><span>● 节点　□ 响应　! 异常 / 时间提醒</span></div>
        <div className="parcel-timing-legend" aria-label="区间颜色说明"><span><i className="parcel-timing-band-key is-detection" />首次检测 → 扫码</span><span><i className="parcel-timing-band-key" />扫码后的等待 / 节点间隔</span><span><i className="parcel-timing-band-key is-request" />请求 → 响应窗口</span><span><i className="parcel-timing-band-key is-stage" />格口分配 → 分拣（请求起止缺失）</span></div>
        <ParcelTimingChart rows={rows} axis={axis} zoom={zoom} first={first} second={second} onSelect={selectNode} onCompare={compareNodes} />
        <div className="parcel-timing-chart-footnote"><span>业务起点使用来源首次检测；Hub首次入库时间单独标注。默认比较检测到落格，右侧耗时为扫码到首次格口请求。</span>
          <span>概览覆盖首次检测、扫码、请求、分拣、落格与回传。点击动作数或异常标记展开 DWS、扫描上传等完整动作与重试。</span>
          <span>色条可点击比较端点。缺少请求开始时，蓝色虚线条显示扫码到已记录的格口或分拣节点；紫色斜纹条表示格口分配到分拣阶段。</span>
          <span>按扫码时间、包裹 ID 升序排列。点选两个节点比较间隔，可跨包裹选择。</span>
          {(data.beforeCount < 5 || data.afterCount < 5) && <span>一侧不足 5 票时，显示全部现有记录。</span>}
          {invalidTimes > 0 && <Typography.Text type="warning">{invalidTimes} 个无效或缺失时间未绘入图中，完整记录可在包裹详情查看。</Typography.Text>}</div>
      </SectionCard>
      <SectionCard title="节点间隔" extra={<Button onClick={() => setSelection({ anchorId: anchorId ?? '', first: null, second: null })}>清除选择</Button>}>
        <div className="parcel-timing-node-selectors">
          <label>节点 A<Select aria-label="节点 A" showSearch allowClear optionFilterProp="searchText" value={first?.key} placeholder="选择开始节点" options={nodeOptions}
            labelRender={renderNodeLabel} optionRender={renderNodeOption} listItemHeight={56} listHeight={336} classNames={{ popup: { root: 'parcel-timing-node-popup' } }} onChange={value => setNode('first', value)} /></label>
          <label>节点 B<Select aria-label="节点 B" showSearch allowClear optionFilterProp="searchText" value={second?.key} placeholder="选择结束节点" options={nodeOptions}
            labelRender={renderNodeLabel} optionRender={renderNodeOption} listItemHeight={56} listHeight={336} classNames={{ popup: { root: 'parcel-timing-node-popup' } }} onChange={value => setNode('second', value)} /></label>
        </div>
        <div className="parcel-timing-comparison">
          <div className="parcel-timing-node-summary"><Tag color="blue">A</Tag>{nodeDescription(first)}</div>
          <div className={`parcel-timing-interval ${interval !== null && interval < 0 ? 'is-negative' : ''}`} aria-live="polite"><span>节点 B − 节点 A</span><strong>{formatTimingMilliseconds(interval)}</strong><span>{interval !== null && interval < 0 ? '节点 B 早于节点 A' : first && second ? `${(interval! / 1000).toLocaleString('zh-CN', { maximumFractionDigits: 7 })} s` : '选择两个节点后显示间隔'}</span></div>
          <div className="parcel-timing-node-summary"><Tag color="purple">B</Tag>{nodeDescription(second)}</div>
        </div>
        <p className="parcel-timing-hint">时间统一显示三位毫秒，间隔计算保留原始精度。只有响应而缺少开始时间时，显示响应节点，不补造请求窗口。跨设备比较需确保来源时钟一致。</p>
      </SectionCard>
      <Collapse className="parcel-timing-details" items={[{ key: 'actions', label: `动作明细（${allMarks.length}）`, children:
        <DataTable<TimingMark> rowKey="key" dataSource={allMarks} scroll={{ x: 1150 }} pagination={{ pageSize: 20, showSizeChanger: false }} columns={[
          { title: '包裹 ID', width: 180, render: (_, mark) => <Link to={`/parcels/${encodeURIComponent(mark.parcelId)}`}>{mark.parcelId}</Link> },
          { title: '动作', width: 170, render: (_, mark) => <span>{mark.title}{mark.attemptNumber !== null ? ` · 第 ${mark.attemptNumber} 次` : ''}</span> },
          { title: '开始 / 发生时间', width: 225, render: (_, mark) => <Button type="link" className="table-link" onClick={() => selectNode(`${mark.key}:start`)}>{formatTimingTime(mark.start)}</Button> },
          { title: '响应 / 结束时间', width: 225, render: (_, mark) => mark.end !== null ? <Button type="link" className="table-link" onClick={() => selectNode(`${mark.key}:end`)}>{formatTimingTime(mark.end)}</Button> : '未提供' },
          { title: '时间差', width: 115, render: (_, mark) => formatTimingMilliseconds(mark.end === null ? null : timingInterval(mark.start, mark.end)) },
          { title: '状态 / 来源', render: (_, mark) => <span>{[mark.state, mark.source, mark.warning].filter(Boolean).join(' · ')}</span> },
        ]} /> }]} />
    </> : !searching && !validId && !emptyQuery && !searchError ? <SectionCard><Empty description="输入包裹 ID 或完整条码，查看前后包裹的动作时序。" /></SectionCard> : null}
    <Modal title="请选择包裹" open={Boolean(candidates)} footer={null} width={1050} onCancel={() => { searchController.current?.abort(); setSearching(false); setCandidates(null); setSearchError(null); }}>
      <p>“{candidateSearch?.query}”命中 {candidates?.totalCount} 票包裹，请结合扫码时间和来源选择本次分析的目标。</p>
      {candidates && <DataTable<ParcelTimingCandidate> rowKey="id" dataSource={candidates.items} loading={searching} scroll={{ x: 910 }}
        pagination={{ current: candidates.pageNumber, pageSize: candidates.pageSize, total: candidates.totalCount, showSizeChanger: false, onChange: page => { if (candidateSearch) void search(candidateSearch, page, false); } }} columns={[
          { title: '包裹 ID', dataIndex: 'id', width: 190 }, { title: '条码', dataIndex: 'barCodes', width: 200 },
          { title: '扫码时间', width: 240, render: (_, parcel) => formatTimingTime(parseTimingTime(parcel.scannedTime)) },
          { title: '来源', width: 220, render: (_, parcel) => <><div>{parcel.workstationName || '未提供工作台'}</div><div className="text-muted">{parcel.sourceInstanceId || '未提供来源实例'}</div>{parcel.sourceRunId && <div className="text-muted">批次 {parcel.sourceRunId}</div>}</> },
          { title: '操作', width: 90, render: (_, parcel) => <Button type="primary" size="small" onClick={() => chooseParcel(parcel.id)}>选择</Button> },
        ]} />}
    </Modal>
  </div>;
}
