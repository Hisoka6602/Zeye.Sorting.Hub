import { useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { Alert, Button, Empty, Input, Modal, Select, Skeleton, Space, Tag } from 'antd';
import { CloseOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { DataTable } from '../../components/DataTable';
import { requestHttpApi } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import type { ParcelComparison } from '../../data/api/parcelComparisonTypes';
import type { ParcelTimingCandidate } from '../../data/api/parcelTimingTypes';
import { localTime } from '../../data/api/operationalTypes';
import { buildParcelTimingRows, type TimingAxis } from './parcelTimingModel';
import { comparisonIds, comparisonLimit } from './parcelComparisonModel';
import { useParcelComparisonLookup } from './useParcelComparisonLookup';
import { ParcelComparisonMeasurements, comparisonColor, comparisonLabel } from './ParcelComparisonMeasurements';
import { ParcelComparisonTiming } from './ParcelComparisonTiming';
import './parcelTiming.css';
import './parcelComparison.css';

/** 精确识别多票包裹，URL可恢复选择、基准和视图；事实只读且有界。 */
export function ParcelComparisonPage() {
  const [params, setParams] = useSearchParams();
  const selection = comparisonIds(params.get('ids'));
  const ids = selection.ids, key = ids.join(',');
  const [query, setQuery] = useState('');
  const [searchBy, setSearchBy] = useState<'auto' | 'id' | 'barcode'>('auto');
  const update = (name: string, value: string) => setParams(current => { const next = new URLSearchParams(current); next.set(name, value); return next; });
  const applyIds = (values: string[]) => {
    setParams(current => {
    const next = new URLSearchParams(current);
    if (values.length) next.set('ids', values.join(',')); else next.delete('ids');
    if (!values.includes(next.get('baseline') ?? '')) next.delete('baseline');
    return next;
    });
  };
  const lookup = useParcelComparisonLookup(ids, applyIds);
  const resource = useApiResource<ParcelComparison>(key ? `/api/parcels/timing/compare?${new URLSearchParams({ ids: key })}` : null, requestHttpApi, false, true);
  const data = resource.data?.requestedIds.join(',') === key ? resource.data : undefined;
  const items = data?.items ?? [];
  const baseline = items.find(item => item.timing.id === (params.get('baseline') ?? ids[0])) ?? items[0];
  const baselineId = baseline?.timing.id ?? '';
  const axis: TimingAxis = params.get('axis') === 'absolute' ? 'absolute' : params.get('axis') === 'detected' ? 'detected' : 'scan';
  const zoom = params.get('zoom') === '4' ? 4 : params.get('zoom') === '2' ? 2 : 1;
  const rows = useMemo(() => buildParcelTimingRows({ anchorId: baselineId, beforeCount: 0, afterCount: 0, items: items.map(item => item.timing) }), [data, baselineId]);

  return <div className="parcel-comparison-page parcel-timing-page">
    <PageIntro title="包裹对比" description="选择多票包裹，比较重量、尺寸、体积和动作时序，定位包裹之间的处理差异。" action={<Link to="/parcels"><Button>包裹台账</Button></Link>} />
    <SectionCard title="选择对比包裹" extra={<Tag>{ids.length} / {comparisonLimit} 票</Tag>} className="parcel-comparison-search-card">
      <form onSubmit={event => { event.preventDefault(); lookup.start(query, searchBy); }} className="parcel-comparison-input-form">
        <div className="parcel-comparison-query">
          <label htmlFor="parcel-comparison-query">包裹 ID / 完整条码</label>
          <Input.TextArea id="parcel-comparison-query" value={query} onChange={event => setQuery(event.target.value)} placeholder={'每行一个，也可用逗号分隔\n可混合输入包裹 ID 和条码'} autoSize={{ minRows: 3, maxRows: 6 }} maxLength={8200} disabled={lookup.resolving} />
        </div>
        <div className="parcel-comparison-input-actions">
          <label>查询方式<Select aria-label="查询方式" value={searchBy} onChange={setSearchBy} disabled={lookup.resolving} options={[{ value: 'auto', label: '自动识别 ID / 条码' }, { value: 'id', label: '包裹 ID' }, { value: 'barcode', label: '完整条码' }]} /></label>
          <Button type="primary" htmlType="submit" icon={<PlusOutlined aria-hidden />} loading={lookup.busy && !lookup.candidates} disabled={ids.length >= comparisonLimit || Boolean(lookup.candidates)}>查询并添加</Button>
          {lookup.busy && !lookup.candidates && <Button onClick={lookup.cancel}>取消查询</Button>}
        </div>
      </form>
      <p className="parcel-comparison-note">精确查询历史包裹，重复条码或数字条码与 ID 同时命中时需选择。最多对比 8 票，所有差值以选中的基准包裹计算。</p>
      {lookup.error && !lookup.candidates && <Alert type="error" showIcon message={lookup.error} />}
      {lookup.notices.length > 0 && <Alert type="info" showIcon message="查询结果提示" description={<ul className="parcel-comparison-notices">{lookup.notices.map((notice, index) => <li key={index}>{notice}</li>)}</ul>} />}
      {ids.length > 0 && <>
        <div className="parcel-comparison-selected-heading"><b>已选择 {ids.length} 票</b><Space><Button size="small" icon={<ReloadOutlined aria-hidden />} loading={resource.loading} onClick={resource.refresh} disabled={lookup.resolving}>刷新对比</Button><Button size="small" onClick={() => applyIds([])} disabled={lookup.resolving}>清空选择</Button></Space></div>
        <div className="parcel-comparison-selected">{ids.map(id => {
          const item = items.find(item => item.timing.id === id), isBaseline = id === baselineId;
          return <div className={'parcel-comparison-selected-item ' + (isBaseline ? 'is-baseline' : '')} key={id}>
            <div className="parcel-comparison-selected-title"><b style={{ color: comparisonColor(id, ids) }}>{comparisonLabel(id, ids)}</b><span title={item?.timing.barCodes}>{item?.timing.barCodes || (data?.missingIds.includes(id) ? '未找到包裹' : '包裹 ID')}</span>
              <Button type="text" size="small" icon={<CloseOutlined aria-hidden />} aria-label={'移除 ' + comparisonLabel(id, ids) + ' 包裹 ' + id} onClick={() => applyIds(ids.filter(value => value !== id))} disabled={lookup.resolving} /></div>
            <small>ID {id}</small><div className="parcel-comparison-selected-meta"><span title={item?.timing.sourceInstanceId ?? ''}>{item?.timing.workstationName || '—'}</span>
              <Button type="text" size="small" disabled={!item || lookup.resolving} onClick={() => update('baseline', id)} aria-label={'将 ' + comparisonLabel(id, ids) + ' 设为基准'}>{isBaseline ? '✓ 对比基准' : '设为基准'}</Button></div>
          </div>;
        })}</div>
      </>}
    </SectionCard>
    {selection.error && <Alert type="error" showIcon message={selection.error} action={<Button onClick={() => applyIds([])}>清空链接选择</Button>} />}
    {resource.error && key && <Alert type="error" showIcon message={data ? '对比刷新失败，以下保留上次结果' : '包裹对比加载失败'} description={resource.error.message} action={<Button onClick={resource.refresh}>重试</Button>} />}
    {data && data.missingIds.length > 0 && <Alert type="warning" showIcon message="部分包裹未找到" description={`${data.missingIds.join('、')}，可能尚未入库或已被清理，请移除或重新查询。${baseline && !items.some(item => item.timing.id === (params.get('baseline') ?? ids[0])) ? ' 当前以 ' + comparisonLabel(baselineId, ids) + ' 为基准。' : ''}`} />}
    {key && !data && (resource.loading || !resource.error) ? <SectionCard><Skeleton active paragraph={{ rows: 8 }} /></SectionCard> : baseline ? <>
      {items.length === 1 && <Alert type="info" showIcon message="已加载1票包裹，再添加至少1票即可查看差异。" />}
      <ParcelComparisonMeasurements items={items} ids={ids} baseline={baseline} />
      <ParcelComparisonTiming key={key} rows={rows} ids={ids} baselineId={baselineId} axis={axis} zoom={zoom} onAxis={value => update('axis', value)} onZoom={value => update('zoom', String(value))} />
    </> : !key || data ? <SectionCard className="parcel-comparison-empty"><Empty description={data ? '所选包裹均未找到，请重新查询。' : '添加两票或更多包裹，开始对比分析。'} /><p>可比较重量和体积差值、长宽高，以及检测、扫码、请求格口、落格等动作的时间间隔。</p></SectionCard> : null}
    <Modal title="条码对应多个包裹，请选择" open={Boolean(lookup.candidates)} width={1000} onCancel={lookup.cancel} footer={<Space><Button onClick={lookup.cancel}>结束查询</Button><Button onClick={lookup.skip} disabled={lookup.busy}>跳过此项，继续查询</Button></Space>}>
      <p>“{lookup.activeQuery}”命中 {lookup.candidates?.totalCount} 票。请结合扫码时间、工作台与来源批次选择，确认后继续查询下一项。</p>
      {lookup.error && <Alert type="error" showIcon message="候选加载失败" description={lookup.error} action={<Button onClick={() => void lookup.page(lookup.candidates?.pageNumber ?? 1)}>重试</Button>} />}
      {lookup.candidates && <DataTable<ParcelTimingCandidate> rowKey="id" countUnit="票" dataSource={lookup.candidates.items} loading={lookup.busy} scroll={{ x: 850 }} pagination={{ current: lookup.candidates.pageNumber, pageSize: lookup.candidates.pageSize, total: lookup.candidates.totalCount, showSizeChanger: false, onChange: page => void lookup.page(page) }} columns={[
        { title: '包裹 ID', dataIndex: 'id', width: 190 }, { title: '主条码', dataIndex: 'barCodes', width: 180 },
        { title: '扫码时间', width: 215, render: (_, parcel) => localTime(parcel.scannedTime) },
        { title: '工作台 / 来源', width: 220, render: (_, parcel) => <><div>{parcel.workstationName || '未提供'}</div><small>{parcel.sourceInstanceId || '未提供来源实例'}{parcel.sourceRunId && <><br />批次 {parcel.sourceRunId}</>}</small></> },
        { title: '操作', width: 115, render: (_, parcel) => <Button size="small" type="primary" onClick={() => lookup.choose(parcel)} disabled={lookup.busy || lookup.pendingIds.includes(parcel.id)}>{lookup.pendingIds.includes(parcel.id) ? '已添加' : '选择此票'}</Button> },
      ]} />}
    </Modal>
  </div>;
}
