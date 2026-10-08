import { CopyOutlined, FileTextOutlined } from '@ant-design/icons';
import { Collapse, Typography } from 'antd';
import { StatusTag } from '../../components/StatusTag';
import { parcelFieldLabels, processingStages } from '../../data/api/parcelTypes';
import { ParcelFacts, parcelFactValue } from './ParcelFacts';
import { formatParcelDetailPayload, groupParcelDetailRecordFields, isMissingParcelDetailValue, parcelDetailApiTitle } from './parcelDetailRecordFields';
import { parcelProcessingFieldLabels, type ParcelProcessingEvent } from './parcelProcessingTimeline';
import './parcelDetailRecords.css';

/** 标量事实直接展示，元数据与长报文按需展开。 */
const factGroupIds = new Set(['facts', 'overview', 'measurement', 'binding', 'timing', 'routing']);

/** 长报文独立显示、限制阅读高度，复制始终使用完整原文。 */
function DetailPayload({ field, value, label }: { field: string; value: unknown; label: string }) {
  const original = typeof value === 'string' ? value : parcelFactValue(field, value);
  return <section className="parcel-record-payload" aria-label={label}>
    <div className="parcel-record-payload-heading">
      <span><FileTextOutlined aria-hidden="true" />{label}</span>
      <Typography.Text className="parcel-record-copy" copyable={{ text: original, icon: <CopyOutlined />, tooltips: ['复制完整原文', '已复制'] }}>复制原文</Typography.Text>
    </div>
    <pre tabIndex={0} aria-label={`${label}内容`}>{formatParcelDetailPayload(original)}</pre>
  </section>;
}

/** 单条明细的摘要、有效字段和按需展开的原文，复用已有业务分类。 */
function DetailRecord({ row, index, title, event, api, processing }: { row: Record<string, unknown>; index: number; title: string; event?: ParcelProcessingEvent; api: boolean; processing: boolean }) {
  const groups = groupParcelDetailRecordFields(row, processing);
  const facts = groups.filter(group => factGroupIds.has(group.id));
  const sections = groups.filter(group => !factGroupIds.has(group.id));
  const business = event?.title ?? (api ? parcelDetailApiTitle(row.apiType) : processing && typeof row.stage === 'number' ? processingStages[row.stage] ?? title : title);
  const timeKey = ['occurredAt', 'recordedAt', 'capturedTime', 'weighingTime', 'generatedTime', 'measurementTime', 'requestTime', 'resultTime', 'receiveTime'].find(key => row[key] != null && row[key] !== '');
  const provider = event?.provider || (typeof row.provider === 'string' ? row.provider : '');
  const attempt = event?.attemptNumber ?? (typeof row.attemptNumber === 'number' && row.attemptNumber > 0 ? row.attemptNumber : null);
  const hasDuration = !isMissingParcelDetailValue(row.elapsedMilliseconds);
  const state = event?.state || (row.isSuccess === true ? '执行成功' : row.isSuccess === false ? '执行失败' : '');
  const labels: Record<string, string> = {
    ...(event ? parcelProcessingFieldLabels(event) : {}),
    apiType: '接口类型编码', stage: processing ? '处理阶段' : '来源阶段', provider: '业务 Provider',
    requestStatus: '业务请求状态', responseStatusCode: 'HTTP 状态码',
    ...(event && !event.isIssue ? { exception: '调用诊断内容', errorMessage: '调用诊断内容' } : {}),
  };
  return <article className={`parcel-detail-record${processing ? ' parcel-processing-record' : ''}`} aria-label={`${business}记录 ${index + 1}`}>
    <div className="parcel-record-heading">
      <span className="parcel-record-index" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
      <h4>{business}</h4>
      {state && <StatusTag value={state} tone={event?.color ?? (row.isSuccess === false ? 'red' : 'green')} />}
      {timeKey && <time title={parcelFieldLabels[timeKey]}>{parcelFactValue(timeKey, row[timeKey])}</time>}
    </div>
    {(provider || attempt != null || event || hasDuration) && <div className="parcel-record-context">
      {provider && <span>{provider}</span>}
      {attempt != null && <span>第 {attempt} 次尝试</span>}
      {hasDuration && <span>耗时 <strong>{parcelFactValue('elapsedMilliseconds', row.elapsedMilliseconds)}</strong> ms</span>}
      {(business === 'Provider 交互' || business === 'Provider 调用') && <span>来源未提供明确业务类型</span>}
    </div>}
    {facts.length > 0 && <div className={`parcel-record-facts${processing ? ' parcel-record-fact-groups' : ''}`}>{facts.map(group => <section key={group.id} className="parcel-record-fact-group" data-group={group.id} aria-label={group.title}>
      {processing && <h5>{group.title}</h5>}
      <ParcelFacts facts={row} keys={group.keys} labels={labels} />
    </section>)}</div>}
    {sections.length > 0 && <Collapse className="parcel-record-sections" ghost size="small" defaultActiveKey={processing || event?.isIssue || row.isSuccess === false || api && !isMissingParcelDetailValue(row.exception) ? ['diagnostic'] : []} items={sections.map(group => ({
      key: group.id,
      label: <span className="parcel-record-section-label">{group.title}<span>{group.keys.length} 项</span></span>,
      children: group.id === 'identity' ? <ParcelFacts facts={row} keys={group.keys} labels={labels} />
        : group.id === 'missing' ? <ul className="parcel-record-missing">{group.keys.map(key => <li key={key}><span>{labels[key] ?? parcelFieldLabels[key] ?? key}</span><span>未提供</span></li>)}</ul>
          : group.keys.map(key => <DetailPayload key={key} field={key} value={row[key]} label={labels[key] ?? parcelFieldLabels[key] ?? key} />),
    }))} />}
  </article>;
}

/** 各类附属明细共用同一记录布局，不修改接口数据或处理轨迹。 */
export function ParcelDetailRecords({ rows, title, category, eventsByRecord }: { rows: Record<string, unknown>[]; title: string; category: string; eventsByRecord?: ReadonlyMap<string, ParcelProcessingEvent> }) {
  return <div className="parcel-detail-records">{rows.map((row, index) => <DetailRecord key={`${typeof row.recordId === 'string' ? row.recordId : category}-${index}`} row={row} index={index} title={title} api={category === 'apiRequests'} processing={category === 'processingRecords'} event={(category === 'apiRequests' || category === 'processingRecords') && typeof row.recordId === 'string' ? eventsByRecord?.get(row.recordId) : undefined} />)}</div>;
}
