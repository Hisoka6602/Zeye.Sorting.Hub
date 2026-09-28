import { Button, DatePicker, Input, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { auditRecords, type AuditRecord } from '../../data/mock/observability';

const dateRange = () => [dayjs('2026-09-25 00:00:00'), dayjs('2026-09-25 23:59:59')] as [Dayjs, Dayjs];

export function AuditListPage() {
  const navigate = useNavigate();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(dateRange());
  const [status, setStatus] = useState<number>();
  const [success, setSuccess] = useState<string>();
  const [trace, setTrace] = useState('');
  const [path, setPath] = useState('');
  const [applied, setApplied] = useState({ status: 0, success: '', trace: '', path: '', from: '', to: '' });
  const search = () => setApplied({ status: status || 0, success: success || '', trace: trace.trim(), path: path.trim(), from: range?.[0]?.format('YYYY-MM-DD HH:mm:ss') || '', to: range?.[1]?.format('YYYY-MM-DD HH:mm:ss') || '' });
  const reset = () => { setRange(dateRange()); setStatus(undefined); setSuccess(undefined); setTrace(''); setPath(''); setApplied({ status: 0, success: '', trace: '', path: '', from: '', to: '' }); };
  const filtered = useMemo(() => auditRecords.filter(item =>
    (!applied.status || item.status === applied.status) && (!applied.success || (applied.success === '是' ? item.status < 400 : item.status >= 400)) &&
    (!applied.trace || item.trace.toLowerCase().includes(applied.trace.toLowerCase())) && (!applied.path || item.path.toLowerCase().includes(applied.path.toLowerCase())) &&
    (!applied.from || item.time >= applied.from) && (!applied.to || item.time <= applied.to)
  ), [applied]);
  return <>
    <PageIntro title="请求审计" description="按时间、路径和追踪 ID 排查请求。" />
    <SectionCard className="filter-card audit-list-filter"><div className="audit-filter-rows">
      <div className="audit-filter-row audit-filter-row-top">
        <Field label="请求时间" wide><DatePicker.RangePicker separator="~" value={range} onChange={value => setRange(value)} showTime style={{ width: '100%' }} /></Field>
        <Field label="状态码"><Select allowClear placeholder="请选择状态码" value={status} onChange={setStatus} options={[200, 400, 500].map(value => ({ value }))} /></Field>
        <Field label="是否成功"><Select allowClear placeholder="请选择" value={success} onChange={setSuccess} options={['是', '否'].map(value => ({ value }))} /></Field>
      </div>
      <div className="audit-filter-row audit-filter-row-bottom">
        <Field label="TraceId"><Input placeholder="请输入 TraceId" value={trace} onChange={event => setTrace(event.target.value)} onPressEnter={search} /></Field>
        <Field label="路径关键字" wide><Input placeholder="请输入请求路径关键字，如 /api/package" value={path} onChange={event => setPath(event.target.value)} onPressEnter={search} /></Field>
        <FilterActions onSearch={search} onReset={reset} />
      </div>
    </div></SectionCard>
    <InfoAlert message={<><b>提示：</b> 由于安全策略限制，部分请求的详细信息可能无法查看。如需更高权限，请联系系统管理员。</>} />
    <SectionCard className="table-card audit-list-table"><DataTable dataSource={filtered} tableLayout="fixed" scroll={{ x: undefined }} columns={[
      { title: '开始时间', dataIndex: 'time', width: 177 }, { title: '方法', dataIndex: 'method', width: 93 }, { title: '请求路径', dataIndex: 'path', width: 227 },
      { title: '状态码', dataIndex: 'status', width: 93, render: (value: number) => <StatusTag value={value} /> }, { title: '耗时 ms', dataIndex: 'duration', width: 98 },
      { title: 'TraceId', dataIndex: 'trace', width: 197, ellipsis: true }, { title: 'CorrelationId', dataIndex: 'correlation', width: 191, ellipsis: true },
      { title: '操作', render: (_, item: AuditRecord) => <Button type="link" className="table-link" onClick={() => navigate(`/audit/requests/${item.id}`)}>查看</Button> },
    ]} /></SectionCard>
  </>;
}
