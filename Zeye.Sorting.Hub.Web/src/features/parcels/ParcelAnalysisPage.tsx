import { Alert, Button, DatePicker, Empty, Input, Select, Skeleton, Space, Switch, Tag } from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { formatNumber } from '../../data/formatNumber';
import { requestHttpApi } from '../../data/api/client';
import { localTime } from '../../data/api/operationalTypes';
import { useApiResource } from '../../data/api/useApiResource';
import type { ParcelAnalysis, ParcelAnalysisParcel, ParcelAnalysisView, ParcelChuteRoute } from '../../data/api/parcelAnalysisTypes';
import { DurationInterfaces, DurationSamples, durationMilliseconds } from './ParcelDurationTables';
import { ParcelChuteHeatmap } from './ParcelChuteHeatmap';
import { analysisApiPath, analysisDateOffset, analysisPercent, analysisValidation, chuteComparison, durationBucketLabel,
  durationTypes, readAnalysisFilters, validAnalysisDate } from './parcelAnalysisModel';
import './parcelAnalysis.css';

const viewLabels = {
  exceptions: { title: '异常分析', description: '按异常类型、NoRead 和路由阻断定位问题包裹，结合详情和动作时序追溯原因。' },
  duration: { title: '耗时分析', description: '分析 DWS 获取、格口决策、分拣执行和业务接口调用耗时，定位处理链路中的瓶颈。' },
  chutes: { title: '格口分析', description: '用格口热力图观察使用分布，按来源对照目标与实际流向，定位编码不一致和兜底使用。' },
};
const count = (value: number | undefined) => value === undefined ? '—' : formatNumber(value, { grouping: true });
const milliseconds = durationMilliseconds;
/** 请求键与数据一同保存，切换筛选时不闪现上一总体的结果。 */
async function loadAnalysis(path: string, signal?: AbortSignal, fresh = false) { return { path, report: await requestHttpApi<ParcelAnalysis>(fresh ? `${path}&refreshDurationSnapshot=true` : path, signal) }; }
/** 指标、单位与总体说明相邻，未知值保留横线。 */
function AnalysisMetric({ label, value, unit, caption }: { label: string; value: ReactNode; unit?: string; caption: string }) {
  return <article className="parcel-analysis-metric"><span>{label}</span><div><strong>{value}</strong>{unit && <small>{unit}</small>}</div><p>{caption}</p></article>;
}
/** 格口差异不使用数字编号推断，缺少原始编码时单独标记。 */
function ChuteTag({ route }: { route: Pick<ParcelChuteRoute, 'targetChuteCode' | 'actualChuteCode'> }) {
  const state = chuteComparison(route);
  return <Tag color={state === 'mismatch' ? 'orange' : state === 'match' ? 'green' : undefined}>{state === 'mismatch' ? '编码不一致' : state === 'match' ? '编码一致' : '信息不足'}</Tag>;
}

/** 三个分析页面共享日期总体和分页下钻，各自读取对应的数据库聚合。 */
function ParcelAnalysisPage({ view }: { view: ParcelAnalysisView }) {
  const [params, setParams] = useSearchParams();
  const [today] = useState(() => dayjs().format('YYYY-MM-DD'));
  const searchKey = params.toString();
  const filters = useMemo(() => readAnalysisFilters(new URLSearchParams(searchKey), today), [searchKey, today]);
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [workstation, setWorkstation] = useState(filters.workstationName);
  const [source, setSource] = useState(filters.sourceInstanceId);
  const [formError, setFormError] = useState<string | null>(null);
  useEffect(() => {
    setRange([dayjs(validAnalysisDate(filters.fromDate) ? filters.fromDate : analysisDateOffset(today, -6)),
      dayjs(validAnalysisDate(filters.toDate) ? filters.toDate : today)]);
    setWorkstation(filters.workstationName); setSource(filters.sourceInstanceId);
  }, [filters.fromDate, filters.toDate, filters.workstationName, filters.sourceInstanceId, today]);
  const invalid = analysisValidation(filters, view);
  const path = invalid ? null : analysisApiPath(filters, view);
  const forceRefresh = useRef(false);
  const loader = useCallback((key: string, signal?: AbortSignal) => { const fresh = forceRefresh.current; forceRefresh.current = false; return loadAnalysis(key, signal, fresh); }, []);
  const resource = useApiResource(path, loader, false, true);
  const refresh = () => { forceRefresh.current = true; resource.refresh(); };
  const data = resource.data?.path === path ? resource.data.report : undefined;
  const loading = resource.loading || Boolean(path && !data && !resource.error);
  const unavailable = resource.error && !data;
  const duration = data?.durationAnalysis;
  const durationIsCall = durationTypes.find(type => type.key === filters.durationType)?.group === 'api';
  const durationName = durationTypes.find(type => type.key === filters.durationType)?.label ?? '完成耗时';
  const durationUnit = durationIsCall ? '次' : '票';
  const sampleCount = duration?.sampleCount ?? data?.lifecycleSampleCount;
  const buckets = duration?.buckets ?? data?.durationBuckets ?? [];
  const filteredCount = duration?.filteredCount ?? data?.filteredCount;
  const update = (changes: Record<string, string | null>, pageNumber = '1') => setParams(previous => {
    const next = new URLSearchParams(previous);
    next.set('fromDate', filters.fromDate); next.set('toDate', filters.toDate); next.set('pageNumber', pageNumber);
    for (const [key, value] of Object.entries(changes)) { if (value === null || value === '') next.delete(key); else next.set(key, value); }
    return next;
  });
  const search = () => {
    if (!range?.[0] || !range[1]) { setFormError('请选择完整日期范围。'); return; }
    const next = { ...readAnalysisFilters(new URLSearchParams(), today), fromDate: range[0].format('YYYY-MM-DD'),
      durationType: filters.durationType,
      toDate: range[1].format('YYYY-MM-DD'), workstationName: workstation.trim(), sourceInstanceId: source.trim() };
    const error = analysisValidation(next, view);
    if (error) { setFormError(error); return; }
    setFormError(null);
    const nextParams = new URLSearchParams({ fromDate: next.fromDate, toDate: next.toDate });
    if (next.workstationName) nextParams.set('workstationName', next.workstationName);
    if (next.sourceInstanceId) nextParams.set('sourceInstanceId', next.sourceInstanceId);
    if (view === 'duration') nextParams.set('durationType', filters.durationType);
    if (analysisApiPath(next, view) === path) refresh(); else setParams(nextParams);
  };
  const reset = () => { setFormError(null); setParams({ fromDate: analysisDateOffset(today, -6), toDate: today }); refresh(); };
  const goRange = (days: number) => { setFormError(null); update({ fromDate: analysisDateOffset(today, 1 - days), toDate: today, minimumMilliseconds: null, maximumMilliseconds: null }); };
  const empty = (description: string) => <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={unavailable ? '分析数据暂不可用，请重试' : description} />;
  const stateLabel = (status: number) => ['待分拣', '已完成', '分拣异常'][status] ?? '未知状态';

  return <div className="parcel-analysis-page" aria-busy={loading}>
    <PageIntro title={viewLabels[view].title} description={viewLabels[view].description}
      action={<Link to={`/parcels/statistics?${new URLSearchParams({ fromDate: filters.fromDate, toDate: filters.toDate })}`}><Button>包裹统计</Button></Link>} />
    <SectionCard className="parcel-analysis-filter">
      <div className="parcel-analysis-filter-grid">
        <Field label="首次入库日期" wide><DatePicker.RangePicker aria-label="首次入库日期" value={range} onChange={setRange} separator="~" style={{ width: '100%' }} /></Field>
        <Field label="工作台"><Input aria-label="工作台筛选" value={workstation} onChange={event => setWorkstation(event.target.value)} onPressEnter={search} maxLength={128} allowClear placeholder="全部工作台（精确匹配）" /></Field>
        <Field label="来源实例"><Input aria-label="来源实例筛选" value={source} onChange={event => setSource(event.target.value)} onPressEnter={search} maxLength={96} allowClear placeholder="全部来源实例（精确匹配）" /></Field>
        <Space wrap><Button type="primary" aria-label="查询分析" icon={<SearchOutlined aria-hidden />} onClick={search}>查询</Button><Button onClick={reset}>重置</Button>
          <Button aria-label="刷新分析" icon={<ReloadOutlined aria-hidden />} onClick={refresh} loading={loading} disabled={Boolean(invalid)}>刷新</Button></Space>
      </div>
      <div className="parcel-analysis-scope"><span>{filters.fromDate} — {filters.toDate} · 按首次入库日归组，状态反映当前快照</span>
        <Space size={4}>{[7, 14, 30].map(days => <Button type="text" size="small" key={days} onClick={() => goRange(days)}>近 {days} 天</Button>)}</Space></div>
    </SectionCard>
    {(invalid || formError) && <Alert showIcon type="warning" message={formError || invalid} />}
    {resource.error && <Alert showIcon type="error" message={data ? '刷新失败，当前保留上次查询结果' : '包裹分析读取失败'} description={resource.error.message}
      action={<Button onClick={refresh}>重试</Button>} />}
    {view === 'duration' && <SectionCard title="耗时类型" className="parcel-duration-types">
      {(['stage', 'api'] as const).map(group => <div className="parcel-duration-type-row" key={group} role="group" aria-label={group === 'stage' ? '包裹阶段耗时' : '接口调用耗时'}>
        <span>{group === 'stage' ? '包裹阶段' : '接口调用'}</span><div>{durationTypes.filter(type => type.group === group).map(type =>
          <Button key={type.key} type={filters.durationType === type.key ? 'primary' : 'default'} aria-pressed={filters.durationType === type.key}
            onClick={() => update({ durationType: type.key, minimumMilliseconds: null, maximumMilliseconds: null })}>{type.label}</Button>)}</div>
      </div>)}
      <p className="parcel-analysis-note">{duration?.description ?? (filters.durationType === 'completion' ? '首次检测至实际完成，查看包裹的整体处理效率。' : '选择阶段或业务接口，查看对应的耗时分布和处理记录。')}
        <span className="parcel-duration-unit-note">{durationIsCall ? '按调用次数统计，包含失败与重试。' : '按包裹票数统计。'}</span></p>
      {duration && <p className="parcel-analysis-note">当前类型统计更新时间：{localTime(duration.generatedAt)}。点击刷新获取最新数据。</p>}
    </SectionCard>}
    <div className="parcel-analysis-metrics">
      {view === 'exceptions' ? <>
        <AnalysisMetric label="当前异常票数" value={count(data?.exceptionCount)} unit="票" caption={`首次入库总体 ${count(data?.parcelCount)} 票`} />
        <AnalysisMetric label="当前异常率" value={data ? analysisPercent(data.exceptionCount, data.parcelCount) : '—'} caption="当前异常票数 / 首次入库总体" />
        <AnalysisMetric label="NoRead 票数" value={count(data?.noReadCount)} unit="票" caption="识读状态或主条码为 NoRead" />
        <AnalysisMetric label="路由阻断票数" value={count(data?.routingBlockedCount)} unit="票" caption="已明确记录阻断正常路由" />
      </> : view === 'duration' ? <>
        <AnalysisMetric label={filters.durationType === 'completion' ? '有效完成样本' : `有效${durationIsCall ? '调用' : '阶段'}样本`} value={count(sampleCount)} unit={durationUnit}
          caption={duration ? `覆盖 ${count(duration.parcelCount)} 票包裹` : `当前已完成 ${count(data?.completedCount)} 票`} />
        <AnalysisMetric label={filters.durationType === 'completion' ? '平均完成耗时' : `平均${durationName}耗时`} value={milliseconds(duration?.averageMilliseconds ?? data?.averageMilliseconds)} unit="ms" caption="全部有效样本的平均耗时" />
        <AnalysisMetric label={`${durationName}中位数`} value={milliseconds(duration?.medianMilliseconds ?? data?.medianMilliseconds)} unit="ms" caption="50% 样本的耗时不高于此值" />
        <AnalysisMetric label={`${durationName} P95`} value={milliseconds(duration?.p95Milliseconds ?? data?.p95Milliseconds)} unit="ms" caption="95% 样本的耗时不高于此值" />
      </> : <>
        <AnalysisMetric label="格口可比样本" value={count(data?.comparableChuteCount)} unit="票" caption="目标与实际格口编码均已提供" />
        <AnalysisMetric label="格口编码不一致率" value={data ? analysisPercent(data.chuteMismatchCount, data.comparableChuteCount) : '—'} caption={`编码不一致 ${count(data?.chuteMismatchCount)} 票 / 可比样本`} />
        <AnalysisMetric label="使用兜底格口" value={count(data?.fallbackCount)} unit="票" caption="包裹明确记录使用兜底格口" />
        <AnalysisMetric label="格口信息不足" value={data ? count(data.parcelCount - data.comparableChuteCount) : '—'} unit="票" caption="目标或实际格口编码缺失" />
      </>}
    </div>
    {view !== 'chutes' ? <div className="parcel-analysis-chart-grid">
      <SectionCard title={view === 'exceptions' ? '异常类型分布' : `${durationName}分布`} extra={<span className="text-muted">点击分布查看{durationIsCall ? '调用' : '包裹'}明细</span>}>
        {loading && !data ? <Skeleton active paragraph={{ rows: 5 }} /> : view === 'exceptions'
          ? data?.exceptionTypes.length ? <div className="parcel-analysis-bars" aria-label="异常类型分布">{data.exceptionTypes.map(group => <button type="button" key={group.code ?? 'unknown'}
              disabled={group.code === null} className={filters.issue === 'exception' && filters.exceptionType === String(group.code) ? 'is-selected' : ''}
              onClick={() => update({ issue: 'exception', exceptionType: String(group.code) })} aria-label={`查看${group.name}包裹`}>
              <span title={group.name}>{group.name}</span><i><b style={{ width: `${data.exceptionCount > 0 ? group.count / data.exceptionCount * 100 : 0}%` }} /></i>
              <strong>{count(group.count)} 票</strong><small>{analysisPercent(group.count, data.exceptionCount)}</small>
            </button>)}</div> : empty('该日期和来源范围暂无当前异常包裹')
          : data && (sampleCount ?? 0) > 0 ? <div className="parcel-analysis-bars is-duration" aria-label={`${durationName}分布`}>{buckets.map(bucket => <button type="button" key={bucket.minimumMilliseconds}
              disabled={bucket.count === 0} className={filters.minimumMilliseconds === String(bucket.minimumMilliseconds) && filters.maximumMilliseconds === String(bucket.maximumMilliseconds ?? '') ? 'is-selected' : ''}
              onClick={() => update({ minimumMilliseconds: String(bucket.minimumMilliseconds), maximumMilliseconds: bucket.maximumMilliseconds === null ? null : String(bucket.maximumMilliseconds) })}
              aria-label={`查看耗时 ${durationBucketLabel(bucket)} 的${durationIsCall ? '调用' : '包裹'}`}>
              <span>{durationBucketLabel(bucket)}</span><i><b style={{ width: `${bucket.count / (sampleCount ?? 1) * 100}%` }} /></i>
              <strong>{count(bucket.count)} {durationUnit}</strong><small>{analysisPercent(bucket.count, sampleCount ?? 0)}</small>
            </button>)}</div> : empty(`该范围暂无有效${durationName}样本`)}
        <p className="parcel-analysis-note">{view === 'exceptions' ? '分母为当前异常票数；选择类型只筛选下方包裹明细。' : '分母为当前类型的全部有效样本；区间含下限、不含上限，单位毫秒。'}</p>
      </SectionCard>
      <SectionCard title="统计口径">
        {view === 'duration' ? <dl className="parcel-analysis-definitions">
          <div><dt>入库总体</dt><dd>{count(data?.parcelCount)} 票</dd></div>
          {duration ? <>
            <div><dt>{durationIsCall ? '观测调用' : '应观测阶段'}</dt><dd>{count(duration.observedCount)} {durationUnit}</dd></div>
            <div><dt>耗时缺失 / 无效</dt><dd>{count(duration.unavailableCount)} {durationUnit}</dd></div>
            {durationIsCall && <><div><dt>明确业务失败</dt><dd>{count(duration.failedCount)} 次</dd></div><div><dt>业务结果未知</dt><dd>{count(duration.unknownResultCount)} 次</dd></div></>}
          </> : <><div><dt>未完成</dt><dd>{data ? count(data.parcelCount - data.completedCount) : '—'} 票</dd></div>
            <div><dt>完成但耗时无效 / 缺失</dt><dd>{data ? count(data.completedCount - data.lifecycleSampleCount) : '—'} 票</dd></div></>}
          <div><dt>最短 / 最长耗时</dt><dd>{milliseconds(duration?.minimumMilliseconds ?? data?.minimumMilliseconds)} / {milliseconds(duration?.maximumMilliseconds ?? data?.maximumMilliseconds)} ms</dd></div>
        </dl> : <dl className="parcel-analysis-definitions">
          <div><dt>入库总体</dt><dd>{count(data?.parcelCount)} 票</dd></div><div><dt>当前已完成</dt><dd>{count(data?.completedCount)} 票</dd></div>
          <div><dt>当前待分拣</dt><dd>{data ? count(data.parcelCount - data.completedCount - data.exceptionCount) : '—'} 票</dd></div>
        </dl>}
        <p className="parcel-analysis-note">{view === 'duration'
          ? '只纳入有效耗时，真实 0 ms 保留；缺失、不可靠或倒序时间不计入分位数。接口优先使用请求与响应时间，只有来源明确上报耗时时会标记其依据。'
          : '异常按当前包裹状态计数；NoRead、路由阻断可与异常重叠。具体失败尝试和恢复过程可在包裹时序中查看。'}</p>
      </SectionCard>
    </div> : <>
    <ParcelChuteHeatmap data={data} loading={loading} unavailable={Boolean(unavailable)} params={params} filters={filters}
      onViewChange={changes => update(changes, filters.pageNumber)} onDrill={changes => update(changes)} />
    <SectionCard title="目标 → 实际格口流向" extra={<span className="text-muted">按来源实例与工作台区分格口</span>}>
      {data?.chuteRoutesTruncated && <Alert showIcon type="info" message="流向较多，当前仅展示票数最多的部分分组。请缩小日期或来源范围；总体指标仍包含全部包裹。" />}
      <DataTable<ParcelChuteRoute> rowKey={route => JSON.stringify([route.sourceInstanceId, route.workstationName, route.targetChuteCode, route.actualChuteCode])}
        loading={loading} countUnit="组" dataSource={data?.chuteRoutes ?? []} scroll={{ x: 960 }} pagination={{ pageSize: 10, showSizeChanger: false }} columns={[
          { title: '工作台 / 来源', width: 220, render: (_, route) => <><div>{route.workstationName || '未提供工作台'}</div><span className="text-muted">{route.sourceInstanceId || '未提供来源实例'}</span></> },
          { title: '目标格口', dataIndex: 'targetChuteCode', width: 130, render: value => value || '未提供' },
          { title: '实际格口', dataIndex: 'actualChuteCode', width: 130, render: value => value || '未提供' },
          { title: '对照', width: 140, render: (_, route) => <ChuteTag route={route} /> },
          { title: '票数', dataIndex: 'count', width: 100, render: value => count(value) },
          { title: '兜底票数', dataIndex: 'fallbackCount', width: 100, render: value => count(value) },
          { title: '操作', width: 130, fixed: 'right', render: (_, route) => <Button type="link" disabled={!route.sourceInstanceId?.trim() || !route.workstationName.trim() || !route.targetChuteCode?.trim() || !route.actualChuteCode?.trim()}
              onClick={() => update({ sourceInstanceId: route.sourceInstanceId, workstationName: route.workstationName,
                targetChuteCode: route.targetChuteCode, actualChuteCode: route.actualChuteCode, mismatchOnly: null, fallbackOnly: null })}>查看包裹</Button> },
        ]} locale={{ emptyText: unavailable ? '分析数据暂不可用' : '该范围暂无包裹格口记录' }} />
      <p className="parcel-analysis-note">格口差异忽略编码大小写和首尾空格；缺失编码单独列出。编码不一致的具体原因需结合指令、兜底和落格事实分析。</p>
    </SectionCard></>}
    {view === 'duration' && durationIsCall && <DurationInterfaces data={duration} loading={loading} />}
    <SectionCard title={view === 'exceptions' ? '问题包裹明细' : view === 'duration' ? `${durationName}明细` : '格口包裹明细'}
      extra={<span className="text-muted">筛选命中 {count(filteredCount)} {view === 'duration' ? durationUnit : '票'}</span>}>
      <div className="parcel-analysis-drill-filters">
        {view === 'exceptions' ? <>
          <label>问题范围 <Select aria-label="问题范围" value={filters.issue} onChange={value => update({ issue: value, exceptionType: null })}
            options={[{ value: 'exception', label: '当前异常' }, { value: 'noread', label: 'NoRead' }, { value: 'blocked', label: '路由阻断' }]} /></label>
          <label>异常类型 <Select aria-label="异常类型" value={filters.exceptionType || undefined} disabled={filters.issue !== 'exception'} allowClear placeholder="全部异常类型"
            onChange={value => update({ exceptionType: value ?? null })} options={data?.exceptionTypes.filter(group => group.code !== null).map(group => ({ value: String(group.code), label: group.name })) ?? []} /></label>
        </> : view === 'duration' ? <>
          <label>耗时下限 <Select aria-label="耗时下限" value={filters.minimumMilliseconds || undefined} allowClear placeholder="全部有效样本"
            onChange={value => update({ minimumMilliseconds: value ?? null, maximumMilliseconds: null })}
            options={[0, 100, 500, 1000, 2000, 5000, 10000].map(value => ({ value: String(value), label: `≥ ${count(value)} ms` }))} /></label>
          {filters.maximumMilliseconds && <Tag closable onClose={() => update({ maximumMilliseconds: null })}>耗时 &lt; {count(Number(filters.maximumMilliseconds))} ms</Tag>}
          <span className="text-muted">按当前类型耗时从长到短排列</span>
        </> : <>
          <label><Switch aria-label="只看格口编码不一致" checked={filters.mismatchOnly} onChange={checked => update({ mismatchOnly: checked ? 'true' : null, targetChuteCode: null, actualChuteCode: null })} /> 只看格口编码不一致</label>
          <label><Switch aria-label="只看兜底格口包裹" checked={filters.fallbackOnly} onChange={checked => update({ fallbackOnly: checked ? 'true' : null })} /> 只看兜底格口包裹</label>
          {(filters.targetChuteCode || filters.actualChuteCode) && <Tag closable onClose={() => update({ targetChuteCode: null, actualChuteCode: null })}>{filters.targetChuteCode || '全部目标'} → {filters.actualChuteCode || '全部实际'}</Tag>}
        </>}
      </div>
      {view === 'duration' && filters.durationType !== 'completion' ? <DurationSamples data={duration} loading={loading} unavailable={Boolean(unavailable)}
        page={Number(filters.pageNumber)} calls={Boolean(durationIsCall)} onPage={page => update({}, String(page))} />
      : <DataTable<ParcelAnalysisParcel> rowKey="id" countUnit="票" dataSource={data?.items ?? []} loading={loading} scroll={{ x: 1490 }}
        pagination={{ current: Number(filters.pageNumber), pageSize: 20, total: data?.filteredCount ?? 0, showSizeChanger: false, onChange: page => update({}, String(page)) }}
        locale={{ emptyText: unavailable ? '分析数据暂不可用，请重试' : '当前筛选条件没有匹配的包裹' }} columns={[
          { title: '包裹 ID', dataIndex: 'id', width: 190, render: value => <Link to={`/parcels/${encodeURIComponent(value)}`}>{value}</Link> },
          { title: '主条码', dataIndex: 'barCodes', width: 180, ellipsis: true, render: value => value || '未提供' },
          { title: '首次入库时间', dataIndex: 'createdTime', width: 230, render: value => localTime(value) },
          { title: '扫码时间', dataIndex: 'scannedTime', width: 230, render: value => localTime(value) },
          { title: '工作台 / 来源', width: 210, render: (_, parcel) => <><div>{parcel.workstationName || '未提供'}</div><span className="text-muted">{parcel.sourceInstanceId || '未提供来源实例'}</span></> },
          { title: '当前状态', width: 120, render: (_, parcel) => <Tag color={parcel.status === 2 ? 'red' : parcel.status === 1 ? 'green' : 'blue'}>{stateLabel(parcel.status)}</Tag> },
          ...(view === 'duration' ? [{ title: '完成耗时（ms）', dataIndex: 'lifecycleMilliseconds', width: 160, render: (value: number | null) => milliseconds(value) }]
            : view === 'exceptions' ? [{ title: '异常 / 阻断', width: 210, render: (_: unknown, parcel: ParcelAnalysisParcel) => <>{parcel.exceptionName || '—'}{parcel.isRoutingBlocked === true && <Tag color="orange">路由阻断</Tag>}</> }]
              : [{ title: '目标 → 实际', width: 210, render: (_: unknown, parcel: ParcelAnalysisParcel) => <><div>{parcel.targetChuteCode || '未提供'} → {parcel.actualChuteCode || '未提供'}</div><ChuteTag route={parcel} />{parcel.isFallbackChuteAssigned === true && <Tag color="blue">兜底</Tag>}</> }]),
          { title: '分析', width: 120, fixed: 'right', render: (_, parcel) => <Space><Link to={`/parcels/${encodeURIComponent(parcel.id)}`}>详情</Link><Link to={`/parcels/timing?id=${encodeURIComponent(parcel.id)}`}>时序</Link></Space> },
        ]} />}
    </SectionCard>
  </div>;
}

/** 当前异常及识读、路由问题的独立入口。 */
export function ParcelExceptionsPage() { return <ParcelAnalysisPage view="exceptions" />; }
/** 完成、DWS、阶段与业务接口调用耗时的独立入口。 */
export function ParcelDurationPage() { return <ParcelAnalysisPage view="duration" />; }
/** 目标和实际格口流向的独立入口。 */
export function ParcelChutesPage() { return <ParcelAnalysisPage view="chutes" />; }
