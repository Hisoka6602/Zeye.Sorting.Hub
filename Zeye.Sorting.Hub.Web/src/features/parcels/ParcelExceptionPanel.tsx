import { Collapse, Descriptions, Tag, Typography } from 'antd';
import { SectionCard } from '../../components/SectionCard';
import type { ParcelDetail } from '../../data/api/parcelTypes';
import { processingStages } from '../../data/api/parcelTypes';
import { ParcelFacts, parcelFactValue } from './ParcelFacts';
import { exceptionRecordKeys, parcelExceptionDetails } from './parcelExceptionDetails';
import './parcelException.css';

export function ParcelExceptionPanel({ parcel }: { parcel: ParcelDetail | undefined }) {
  const exception = parcelExceptionDetails(parcel);
  if (!exception) return null;

  return <SectionCard title="包裹异常" className="parcel-exception-panel" extra={<Tag color={exception.current ? 'error' : 'default'}>{exception.current ? '当前异常' : '异常记录'}</Tag>}>
    <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} items={[
      { key: 'type', label: '异常类型', children: <Typography.Text type="danger">{exception.type}</Typography.Text> },
      { key: 'source', label: '来源异常代码', children: exception.sourceCode ? <Typography.Text copyable>{exception.sourceCode}</Typography.Text> : '未提供' },
      { key: 'rule', label: '异常判定规则', span: 2, children: exception.rule },
      { key: 'details', label: '异常详细信息', span: 2, children: exception.messages.length
        ? <div className="parcel-exception-messages">{exception.messages.map((message, index) => <p key={index}>{message}</p>)}</div>
        : '未提供异常详细信息' },
    ]} />
    {exception.records.length || exception.interfaceErrors.length ? <Collapse className="parcel-exception-records" items={[
      ...exception.records.map(record => ({
        key: record.recordId,
        label: `${parcelFactValue('occurredAt', record.occurredAt)} · ${processingStages[record.stage] ?? record.stage}`,
        children: <ParcelFacts facts={record} keys={exceptionRecordKeys(record)} />,
      })),
      ...exception.interfaceErrors.map((request, index) => ({ key: `interface-${index}`, label: `外部接口异常 ${index + 1}`, children: <ParcelFacts facts={request} /> })),
    ]} /> : null}
  </SectionCard>;
}
