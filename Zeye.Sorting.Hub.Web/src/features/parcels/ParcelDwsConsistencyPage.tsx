import { Alert, Button, Collapse, DatePicker, Empty, Input, InputNumber, Select, Space, Switch, Tag } from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestHttpApi } from '../../data/api/client';
import { localTime } from '../../data/api/operationalTypes';
import { useApiResource } from '../../data/api/useApiResource';
import type { DwsConsistencyGroup, DwsConsistencySource, DwsMeasurementMetric, DwsMeasurementSample, ParcelDwsConsistency } from '../../data/api/parcelDwsConsistencyTypes';
import { ParcelDwsTrend } from './ParcelDwsTrend';
import { analysisDateOffset, validAnalysisDate } from './parcelAnalysisModel';
import { dwsApiPath, dwsPercent, dwsQueryParams, dwsScanReason, dwsValidation, dwsValue, readDwsFilters, type DwsFilters } from './parcelDwsConsistencyModel';
import './parcelAnalysis.css';
import './parcelDwsConsistency.css';

const thresholdInputs = [
  { key: 'weightToleranceGrams', label: '重量绝对差（g）' }, { key: 'weightTolerancePercent', label: '重量相对差（%）' },
  { key: 'volumeToleranceCm3', label: '体积绝对差（cm³）' }, { key: 'volumeTolerancePercent', label: '体积相对差（%）' },
  { key: 'scanDurationToleranceMilliseconds', label: '扫码耗时绝对差（ms）' }, { key: 'scanDurationTolerancePercent', label: '扫码耗时相对差（%）' },
  { key: 'referenceWeightGrams', label: '标准参考重量（g，可选）' }, { key: 'referenceVolumeCm3', label: '标准参考体积（cm³，可选）' },
] as const;
const emptyMetric: DwsMeasurementMetric = { count: 0, minimum: null, maximum: null, average: null, median: null, p95: null,
  spread: null, spreadPercent: null, reference: null, maximumReferenceDeviation: null, maximumReferenceDeviationPercent: null };
/** 共用全部量测，不将表格当前页当成统计总体。 */
function MetricCard({ label, value, unit, caption }: { label: string; value?: number; unit: string; caption: string }) {
  return <article className="parcel-analysis-metric"><span>{label}</span><div><strong>{dwsValue(value)}</strong><small>{unit}</small></div><p>{caption}</p></article>;
}
function GroupStatus({ group }: { group: DwsConsistencyGroup }) {
  return <Space size={4} wrap>{group.weightDeviates && <Tag color="orange">重量超差</Tag>}{group.volumeDeviates && <Tag color="orange">体积超差</Tag>}
    {group.scanDurationDeviates && <Tag color="orange">扫码耗时超差</Tag>}{group.referenceDeviates && <Tag color="red">偏离参考值</Tag>}
    {!group.weightDeviates && !group.volumeDeviates && !group.scanDurationDeviates && !group.referenceDeviates && <Tag color={group.isComparable ? 'green' : undefined}>{group.isComparable ? '阈值内' : '样本不足'}</Tag>}</Space>;
}
function MetricDetails({ metric, name, unit, timing = false }: { metric: DwsMeasurementMetric; name: string; unit: string; timing?: boolean }) {
  return <div className="dws-metric-details"><h3>{name}<span>{metric.count} 次有效样本</span></h3><dl>
    {timing && <><div><dt>平均耗时</dt><dd>{dwsValue(metric.average, unit)}</dd></div><div><dt>P95 耗时</dt><dd>{dwsValue(metric.p95, unit)}</dd></div></>}
    <div><dt>中位数</dt><dd>{dwsValue(metric.median, unit)}</dd></div><div><dt>最小 / 最大</dt><dd>{dwsValue(metric.minimum)} / {dwsValue(metric.maximum, unit)}</dd></div>
    <div><dt>测量极差</dt><dd>{dwsValue(metric.spread, unit)}</dd></div><div><dt>相对差</dt><dd>{dwsPercent(metric.spreadPercent)}</dd></div>
    {metric.reference != null && <><div><dt>标准参考值</dt><dd>{dwsValue(metric.reference, unit)}</dd></div><div><dt>最大参考偏差</dt><dd>{dwsValue(metric.maximumReferenceDeviation, unit)} · {dwsPercent(metric.maximumReferenceDeviationPercent)}</dd></div></>}
  </dl></div>;
}
/** 来源中位数逐条码对照，不直接比较不同包裹大小的平均值。 */
function SourceComparison({ sources, loading }: { sources: DwsConsistencySource[]; loading: boolean }) {
  return <DataTable<DwsConsistencySource> rowKey="sourceInstanceId" dataSource={sources} loading={loading} pagination={false} scroll={{ x: 1230 }} columns={[
    { title: '来源工作台 / 实例', width: 290, render: (_, row) => <div className="dws-source"><span>{row.workstationName || '未提供工作台名称'}</span><small>{row.sourceInstanceId}</small></div> },
    { title: '测量次数', dataIndex: 'measurementCount', width: 120 }, { title: '重复条码', dataIndex: 'repeatedBarcodeCount', width: 120 },
    { title: '内部超差条码', dataIndex: 'deviationBarcodeCount', width: 140 },
    { title: '跨来源重量偏差', dataIndex: 'medianWeightDeviationPercent', width: 170, render: dwsPercent },
    { title: '跨来源体积偏差', dataIndex: 'medianVolumeDeviationPercent', width: 170, render: dwsPercent },
    { title: '跨来源扫码耗时偏差', dataIndex: 'medianScanDurationDeviationPercent', width: 200, render: dwsPercent },
  ]} />;
}
/** 请求键和响应一同保存，切换日期和条码时不显示旧范围的数据。 */
async function loadConsistency(path: string, signal?: AbortSignal, refresh = false) {
  return { path, report: await requestHttpApi<ParcelDwsConsistency>(refresh ? `${path}&refresh=true` : path, signal) };
}
export function ParcelDwsConsistencyPage() {
  const [params, setParams] = useSearchParams(); const [today] = useState(() => dayjs().format('YYYY-MM-DD'));
  const searchKey = params.toString();
  const filters = useMemo(() => readDwsFilters(new URLSearchParams(searchKey), today), [searchKey, today]);
  const [draft, setDraft] = useState<DwsFilters>(filters); const [formError, setFormError] = useState<string | null>(null);
  useEffect(() => { setDraft(filters); setFormError(null); }, [filters]);
  const invalid = dwsValidation(filters); const path = invalid ? null : dwsApiPath(filters); const forceRefresh = useRef(false);
  const loader = useCallback((key: string, signal?: AbortSignal) => { const fresh = forceRefresh.current; forceRefresh.current = false; return loadConsistency(key, signal, fresh); }, []);
  const resource = useApiResource(path, loader, false, true);
  const data = resource.data?.path === path ? resource.data.report : undefined;
  const loading = resource.loading || Boolean(path && !data && !resource.error);
  const refresh = () => { forceRefresh.current = true; resource.refresh(); };
  const changeDraft = (key: keyof DwsFilters, value: string) => setDraft(previous => ({ ...previous, [key]: value }));
  const update = (changes: Partial<DwsFilters>) => setParams(dwsQueryParams({ ...filters, ...changes }));
  const search = () => {
    const next = { ...draft, barcode: draft.barcode.trim(), sourceInstanceId: draft.sourceInstanceId.trim(), workstationName: draft.workstationName.trim(),
      pageNumber: '1', measurementPageNumber: '1', detailBarcode: '' };
    const error = dwsValidation(next); if (error) { setFormError(error); return; }
    setFormError(null); if (dwsApiPath(next) === path) refresh(); else setParams(dwsQueryParams(next));
  };
  const detail = data?.detail;
  const qualityCount = (data?.missingIdentityCount ?? 0) + (data?.conflictingMeasurementCount ?? 0) + (data?.missingBarcodeCount ?? 0);
  return <div className="parcel-analysis-page dws-consistency-page" aria-busy={loading}>
    <PageIntro title="DWS 测量一致性" description="对比同一条码多次过机的重量、体积与扫码耗时，观察测量及识读响应的稳定性。"
      action={<Link to="/parcels/duration"><Button>耗时分析</Button></Link>} />
    <SectionCard>
      <div className="dws-filter-grid">
        <Field label="记录首次入库日期"><DatePicker.RangePicker aria-label="记录首次入库日期" style={{ width: '100%' }}
          value={validAnalysisDate(draft.fromDate) && validAnalysisDate(draft.toDate) ? [dayjs(draft.fromDate), dayjs(draft.toDate)] : null}
          onChange={range => setDraft(previous => ({ ...previous, fromDate: range?.[0]?.format('YYYY-MM-DD') ?? '', toDate: range?.[1]?.format('YYYY-MM-DD') ?? '' }))} /></Field>
        <Field label="精确条码"><Input aria-label="精确条码" maxLength={1024} value={draft.barcode} onChange={event => changeDraft('barcode', event.target.value)} onPressEnter={search} allowClear placeholder="全部条码，区分大小写" /></Field>
        <Field label="来源工作台"><Input aria-label="来源工作台" maxLength={128} value={draft.workstationName} onChange={event => changeDraft('workstationName', event.target.value)} onPressEnter={search} allowClear placeholder="全部工作台（精确匹配）" /></Field>
        <Field label="来源实例"><Input aria-label="来源实例" maxLength={96} value={draft.sourceInstanceId} onChange={event => changeDraft('sourceInstanceId', event.target.value)} onPressEnter={search} allowClear placeholder="全部来源实例（精确匹配）" /></Field>
      </div>
      <div className="dws-filter-actions"><Space wrap><Button type="primary" icon={<SearchOutlined aria-hidden />} onClick={search}>查询</Button>
        <Button onClick={() => { setParams(dwsQueryParams(readDwsFilters(new URLSearchParams(), today))); setFormError(null); }}>重置</Button>
        <Button icon={<ReloadOutlined aria-hidden />} loading={loading} disabled={Boolean(invalid)} onClick={refresh}>刷新</Button></Space>
        <Space size={4}>{[7, 14, 30].map(days => <Button type="text" key={days} onClick={() => update({ fromDate: analysisDateOffset(today, 1 - days), toDate: today, pageNumber: '1', measurementPageNumber: '1', detailBarcode: '' })}>近 {days} 天</Button>)}</Space>
      </div>
      <Collapse ghost items={[{ key: 'thresholds', label: '偏差阈值与标准参考值', children: <><div className="dws-threshold-grid">
        {thresholdInputs.map(({ key, label }) => <Field label={label} key={key}><InputNumber<string> aria-label={label} stringMode style={{ width: '100%' }} min="0" max="1000000000"
          value={draft[key] || null} onChange={value => changeDraft(key, value ?? '')} placeholder={key.startsWith('reference') ? '未指定' : undefined} /></Field>)}
      </div><p className="parcel-analysis-note">绝对差与相对差均超过阈值时标记超差。标准参考值仅用于当前精确条码查询，不会修改设备配置。调整后点击“查询”生效。</p></> }]} />
    </SectionCard>
    {(invalid || formError) && <Alert type="warning" showIcon message={formError || invalid} />}
    {resource.error && <Alert type="error" showIcon message={data ? '刷新失败，保留上次结果' : 'DWS 分析读取失败'} description={resource.error.message} action={<Button onClick={refresh} disabled={Boolean(invalid)}>重试</Button>} />}
    <div className="parcel-analysis-metrics">
      <MetricCard label="有效独立测量" value={data?.measurementCount} unit="次" caption="接收与绑定合并后的量测" />
      <MetricCard label="重复测量条码" value={data?.repeatedBarcodeCount} unit="组" caption="同一条码有两次及以上量测" />
      <MetricCard label="可比较条码" value={data?.comparableBarcodeCount} unit="组" caption="重量、体积或扫码耗时具备重复样本" />
      <MetricCard label="重复测量超差" value={data?.deviationBarcodeCount} unit="组" caption="重复测量差异超过当前阈值" />
    </div>
    <Alert type="info" showIcon message="扫码耗时反映包裹检测后取得有效条码的响应速度"
      description={<>{data ? `有效扫码耗时 ${dwsValue(data.scanTimingSampleCount)} 次；时间不可用 ${dwsValue(data.missingScanTimingCount)} 次。` : ''}
        使用来源程序的检测与有效条码接收记录。时间缺失、不可靠或倒序时保持未知，外部扫描上传接口的调用耗时可在“耗时分析”查看。</>} />
    {data && qualityCount > 0 && <Alert type="warning" showIcon message="部分记录未参与一致性比较" description={`缺少测量身份 ${dwsValue(data.missingIdentityCount)} 条；身份冲突 ${dwsValue(data.conflictingMeasurementCount)} 组；缺少有效条码 ${dwsValue(data.missingBarcodeCount)} 组。排除数量按所选日期与来源统计。`} />}
    <SectionCard title="条码差异排行" extra={data ? `${dwsValue(data.filteredCount)} 组` : undefined}>
      <div className="parcel-analysis-drill-filters"><label>只看超差 <Switch aria-label="只看超差" checked={filters.onlyDeviations} onChange={value => update({ onlyDeviations: value, pageNumber: '1' })} /></label>
        <label>排序 <Select aria-label="差异排行排序" value={filters.sortBy} onChange={value => update({ sortBy: value, pageNumber: '1' })} options={[{ value: 'weight', label: '重量相对差' }, { value: 'volume', label: '体积相对差' },
          { value: 'scan-duration', label: '扫码耗时相对差' }, { value: 'scan-duration-p95', label: '扫码耗时 P95' }, { value: 'count', label: '测量次数' }]} /></label>
        <span className="dws-muted">{data ? `快照 ${localTime(data.generatedAt)} · 合并重复记录 ${dwsValue(data.duplicateRecordCount)} 条` : '默认展示有重复测量的条码'}</span></div>
      <DataTable<DwsConsistencyGroup> rowKey="barcode" dataSource={data?.items ?? []} loading={loading} countUnit="组" scroll={{ x: 1760 }}
        locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={resource.error ? '数据暂不可用' : '当前范围暂无可比较的重复测量，或没有符合筛选的条码'} /> }}
        pagination={{ current: Number(filters.pageNumber), pageSize: 20, total: data?.filteredCount ?? 0, showSizeChanger: false, onChange: page => update({ pageNumber: String(page) }) }} columns={[
          { title: '条码', dataIndex: 'barcode', width: 260, ellipsis: true }, { title: '测量 / 包裹 / 来源', width: 180, render: (_, row) => `${row.measurementCount} 次 / ${row.parcelCount} 票 / ${row.sourceCount} 个` },
          { title: '重量中位数', width: 150, render: (_, row) => dwsValue(row.weight.median, 'g') },
          { title: '重量差异', width: 165, render: (_, row) => <div>{dwsValue(row.weight.spread, 'g')}<small className="dws-subvalue">{dwsPercent(row.weight.spreadPercent)}</small></div> },
          { title: '体积中位数', width: 150, render: (_, row) => dwsValue(row.volume.median, 'cm³') },
          { title: '体积差异', width: 165, render: (_, row) => <div>{dwsValue(row.volume.spread, 'cm³')}<small className="dws-subvalue">{dwsPercent(row.volume.spreadPercent)}</small></div> },
          { title: '扫码耗时中位数', width: 150, render: (_, row) => dwsValue(row.scanDuration?.median, 'ms') },
          { title: '扫码耗时 P95', width: 140, render: (_, row) => dwsValue(row.scanDuration?.p95, 'ms') },
          { title: '扫码耗时差异', width: 190, render: (_, row) => <div>{dwsValue(row.scanDuration?.spread, 'ms')}<small className="dws-subvalue">{dwsPercent(row.scanDuration?.spreadPercent)}</small></div> },
          { title: '状态', width: 180, render: (_, row) => <GroupStatus group={row} /> },
          { title: '操作', width: 100, fixed: 'right', render: (_, row) => <Button type={filters.detailBarcode === row.barcode ? 'primary' : 'link'} size="small" onClick={() => update({ detailBarcode: row.barcode, measurementPageNumber: '1' })}>测量对比</Button> },
        ]} />
      <p className="parcel-analysis-note">同条码可对应多票包裹，包裹身份分别保留。相对差反映重复量测的波动；无正基准时保持未知。</p>
    </SectionCard>
    {detail && <SectionCard title={<>测量对比 <span className="dws-detail-barcode">{detail.summary.barcode}</span></>}
      extra={<Space wrap><GroupStatus group={detail.summary} /><Button size="small" onClick={() => update({ detailBarcode: '', measurementPageNumber: '1' })}>收起</Button></Space>}>
      <div className="dws-detail-summary"><MetricDetails metric={detail.summary.weight} name="重量" unit="g" /><MetricDetails metric={detail.summary.volume} name="物理体积" unit="cm³" />
        <MetricDetails metric={detail.summary.scanDuration ?? emptyMetric} name="扫码耗时" unit="ms" timing /></div>
      <div className="dws-trends"><ParcelDwsTrend samples={detail.trend} metric="weightGrams" summary={detail.summary.weight} name="重量" unit="g" />
        <ParcelDwsTrend samples={detail.trend} metric="volumeCm3" summary={detail.summary.volume} name="物理体积" unit="cm³" />
        <ParcelDwsTrend samples={detail.scanTrend ?? []} metric="scanDurationMilliseconds" summary={detail.summary.scanDuration ?? emptyMetric} name="扫码耗时" unit="ms" /></div>
      <p className="parcel-analysis-note">{detail.trendTruncated || detail.scanTrendTruncated ? '每种趋势抽取最多 200 个真实点并保留首尾；统计仍使用全部有效样本。' : '重量和体积使用设备测量时间，扫码耗时使用来源条码接收时间。'} 缺失时间的记录及原因仍可在下方明细查看。</p>
      <h3 className="dws-subheading">该条码的来源对比</h3><SourceComparison sources={detail.sources} loading={loading} />
      <h3 className="dws-subheading">原始测量明细</h3>
      <DataTable<DwsMeasurementSample> rowKey="key" dataSource={detail.items} loading={loading} countUnit="次" scroll={{ x: 2040 }}
        pagination={{ current: Number(filters.measurementPageNumber), pageSize: 20, total: detail.measurementCount, showSizeChanger: false, onChange: page => update({ measurementPageNumber: String(page) }) }} columns={[
          { title: '测量时间', dataIndex: 'measuredAt', width: 230, render: value => value ? localTime(value) : '未提供' },
          { title: '来源工作台 / 实例', width: 220, render: (_, row) => <div className="dws-source"><span>{row.workstationName || '未提供'}</span><small>{row.sourceInstanceId}</small></div> },
          { title: '包裹编号', width: 180, render: (_, row) => row.parcelId ? <Link to={`/parcels/${row.parcelId}`}>{row.parcelId}</Link> : '未绑定包裹' },
          { title: '重量', dataIndex: 'weightGrams', width: 120, render: value => dwsValue(value, 'g') },
          { title: '长 × 宽 × 高（mm）', width: 230, render: (_, row) => [row.lengthMm, row.widthMm, row.heightMm].map(value => dwsValue(value)).join(' × ') },
          { title: '物理体积', dataIndex: 'volumeCm3', width: 140, render: value => dwsValue(value, 'cm³') },
          { title: '检测时间', dataIndex: 'scanStartedAt', width: 210, render: value => value ? localTime(value) : '未提供' },
          { title: '有效条码接收时间', dataIndex: 'scanCompletedAt', width: 210, render: value => value ? localTime(value) : '未提供' },
          { title: '扫码耗时', width: 170, render: (_, row) => <div>{dwsValue(row.scanDurationMilliseconds, 'ms')}
            <small className="dws-subvalue">{row.scanDurationMilliseconds == null ? dwsScanReason(row.scanTimingUnavailableReason) : row.scanTimingBasis === 'source-received' ? '来源接收时间' : '来源接收事件时间'}</small></div> },
          { title: '测量身份', dataIndex: 'messageIdentity', width: 240, ellipsis: true },
          { title: '关联状态', width: 120, render: (_, row) => <Tag color={row.bindingConfirmed ? 'green' : undefined}>{row.bindingConfirmed ? '绑定已确认' : '已接收'}</Tag> },
        ]} />
    </SectionCard>}
    {filters.detailBarcode && !detail && !loading && data && <Alert showIcon type="info" message="所选条码在当前范围内没有有效测量，请调整筛选或重新选择。" />}
    <SectionCard title="来源工作台对比">
      <SourceComparison sources={data?.sources ?? []} loading={loading} />
      <p className="parcel-analysis-note">内部超差按各来源自己的重复样本判断；跨来源偏差仅比较共有条码的测量中位数，缺少共同样本时显示“—”。{data?.sourcesTruncated && '当前展示测量量或超差最多的 100 个来源，可筛选单一来源继续查看。'}</p>
      <p className="parcel-analysis-note">来源代表工作台实例。包装变化、条码复用、量测姿态和设备安装位置也可能引起差异；扫码耗时包含检测到条码可用的过程，未单独采集解码芯片耗时。检验量测准确度需要已知重量、体积的标准包裹参考值。</p>
    </SectionCard>
  </div>;
}
