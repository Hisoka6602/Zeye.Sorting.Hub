import { Collapse, Descriptions, Tag, Typography } from 'antd';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { ParcelDetail } from '../../data/api/parcelTypes';
import type { ParcelProcessingEvent } from './parcelProcessingTimeline';
import { parcelFactValue } from './ParcelFacts';
import { ParcelDetailRecords } from './ParcelDetailRecords';
import { isMissingParcelDetailValue, parcelDetailApiTitle } from './parcelDetailRecordFields';
import { parcelExceptionDetails } from './parcelExceptionDetails';
import { localTime } from '../../data/api/operationalTypes';
import './parcelException.css';

/** 折叠摘要沿用处理轨迹的业务类型和状态，不从诊断原文猜测调用结果。 */
function ExceptionRecordLabel({ title, kind, index, event, time, duration }: { title: string; kind: string; index: number; event?: ParcelProcessingEvent; time?: string; duration?: unknown }) {
  return <div className="parcel-exception-record-label">
    <div className="parcel-exception-record-title"><span>{title}</span>{event?.state && <StatusTag value={event.state} tone={event.color} />}</div>
    <div className="parcel-exception-record-meta"><span>{kind} · {String(index + 1).padStart(2, '0')}</span>{time && <time>{localTime(time)}</time>}{!isMissingParcelDetailValue(duration) && <span>耗时 {parcelFactValue('elapsedMilliseconds', duration)} ms</span>}</div>
  </div>;
}

/** 异常摘要控制长文高度，详细记录保留全部字段与原文并按用途分组。 */
export function ParcelExceptionPanel({ parcel, events }: { parcel: ParcelDetail | undefined; events?: readonly ParcelProcessingEvent[] }) {
  const exception = parcelExceptionDetails(parcel, events);
  if (!exception) return null;

  return <SectionCard title="包裹异常" className="parcel-exception-panel" extra={<Tag color={exception.current ? 'error' : 'default'}>{exception.current ? '当前异常' : '异常记录'}</Tag>}>
    <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} items={[
      { key: 'type', label: '异常类型', children: <Typography.Text type="danger">{exception.type}</Typography.Text> },
      { key: 'source', label: '来源异常代码', children: exception.sourceCode ? <Typography.Text copyable>{exception.sourceCode}</Typography.Text> : '未提供' },
      { key: 'rule', label: '异常判定规则', span: 'filled', children: exception.rule },
      { key: 'details', label: '异常详细信息', span: 'filled', children: exception.messages.length
        ? <div className="parcel-exception-messages">{exception.messages.map((message, index) => <Typography.Paragraph key={index} ellipsis={{ rows: 2, expandable: 'collapsible', symbol: expanded => expanded ? '收起全文' : '展开全文' }} copyable={{ text: message, tooltips: ['复制完整原文', '已复制'] }}>{message}</Typography.Paragraph>)}</div>
        : '未提供异常详细信息' },
    ]} />
    {exception.records.length || exception.interfaceErrors.length ? <Collapse className="parcel-exception-records" items={[
      ...exception.records.map((record, index) => ({
        key: record.recordId,
        label: <ExceptionRecordLabel title={exception.recordTitles[record.recordId]} kind="处理记录" index={index} event={exception.eventsByRecord.get(record.recordId)} time={parcelFactValue('occurredAt', record.occurredAt)} />,
        children: <ParcelDetailRecords rows={[{ ...record }]} title="异常处理记录" category="processingRecords" eventsByRecord={exception.eventsByRecord} />,
      })),
      ...exception.interfaceErrors.map((request, index) => {
        const event = typeof request.recordId === 'string' ? exception.eventsByRecord.get(request.recordId) : undefined;
        const requestTime = request.requestTime ?? event?.record.occurredAt;
        return {
          key: `interface-${index}`,
          label: <ExceptionRecordLabel title={event?.title ?? parcelDetailApiTitle(request.apiType)} kind="外部接口记录" index={index} event={event} time={!isMissingParcelDetailValue(requestTime) ? parcelFactValue('requestTime', requestTime) : undefined} duration={request.elapsedMilliseconds} />,
          children: <ParcelDetailRecords rows={[request]} title="外部接口记录" category="apiRequests" eventsByRecord={exception.eventsByRecord} />,
        };
      }),
    ]} /> : null}
  </SectionCard>;
}
