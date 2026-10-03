import { Button, DatePicker, Input, Select } from 'antd';
import { type Dayjs } from 'dayjs';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { ApiFeedback } from '../../components/ApiFeedback';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { useApiResource } from '../../data/api/useApiResource';
import { localTime, type AuditItem, type PagedResult } from '../../data/api/operationalTypes';
import { describeAuditRequest } from './requestDescriptions';
import './audit.css';
export function AuditListPage() {
  const navigate = useNavigate();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [status, setStatus] = useState<number>();
  const [success, setSuccess] = useState<string>();
  const [trace, setTrace] = useState('');
  const [path, setPath] = useState('');
  const [applied, setApplied] = useState('');
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(10);
  const resource = useApiResource<PagedResult<AuditItem>>('/api/audit/web-requests?pageNumber=' + page + '&pageSize=' + size + '&includeTotalCount=true' + applied);
  const search = () => {
    const query = new URLSearchParams();
    if (status) query.set('statusCode', String(status));
    if (success) query.set('isSuccess', String(success === '是'));
    if (trace.trim()) query.set('traceId', trace.trim());
    if (path.trim()) query.set('requestPathKeyword', path.trim());
    if (range?.[0]) query.set('startedAtStart', range[0].format('YYYY-MM-DD HH:mm:ss'));
    if (range?.[1]) query.set('startedAtEnd', range[1].format('YYYY-MM-DD HH:mm:ss'));
    setPage(1); setApplied('&' + query); resource.refresh();
  };
  const reset = () => { setRange(null); setStatus(undefined); setSuccess(undefined); setTrace(''); setPath(''); setPage(1); setApplied(''); resource.refresh(); };
  return <>
    <PageIntro title="请求审计" description="按时间、路径和追踪 ID 排查请求。" />
    <SectionCard className="filter-card audit-list-filter"><div className="audit-filter-rows">
      <div className="audit-filter-row audit-filter-row-top">
        <Field label="请求时间" wide><DatePicker.RangePicker separator="~" value={range} onChange={setRange} showTime style={{ width: '100%' }} /></Field>
        <Field label="状态码"><Select allowClear placeholder="请选择状态码" value={status} onChange={setStatus} options={[200, 201, 204, 400, 401, 403, 404, 409, 429, 500, 503].map(value => ({ value }))} /></Field>
        <Field label="是否成功"><Select allowClear placeholder="请选择" value={success} onChange={setSuccess} options={['是', '否'].map(value => ({ value }))} /></Field>
      </div>
      <div className="audit-filter-row audit-filter-row-bottom">
        <Field label="TraceId"><Input placeholder="请输入完整 TraceId" value={trace} onChange={event => setTrace(event.target.value)} onPressEnter={search} /></Field>
        <Field label="路径关键字" wide><Input placeholder="请输入请求路径关键字" value={path} onChange={event => setPath(event.target.value)} onPressEnter={search} /></Field>
        <FilterActions onSearch={search} onReset={reset} extra={<Button onClick={resource.refresh}>刷新</Button>} />
      </div>
    </div></SectionCard>
    <InfoAlert message="请求详情遵循服务器的鉴权和脱敏策略。审计服务未开启时会显示请求失败。" />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    <SectionCard className="table-card audit-list-table"><DataTable<AuditItem> loading={resource.loading} dataSource={resource.data?.items ?? []} pagination={{ current: page, pageSize: size, total: Number(resource.data?.totalCount ?? 0), onChange: (next, nextSize) => { setPage(next); setSize(nextSize); } }} columns={[
      { title: '开始时间', dataIndex: 'startedAt', width: 177, render: localTime }, { title: '方法', dataIndex: 'requestMethod', width: 93 }, { title: '请求路径', dataIndex: 'requestPath', width: 227, render: (value: string) => <span className="audit-request-path" title={value}>{value}</span> },
      { title: '中文说明', key: 'description', width: 280, render: (_, item) => <span className="audit-request-description">{describeAuditRequest(item.requestMethod, item.requestPath)}</span> },
      { title: '状态码', dataIndex: 'statusCode', width: 93, render: (value: number) => <StatusTag value={value} /> }, { title: '耗时 ms', dataIndex: 'durationMs', width: 98 },
      { title: 'TraceId', dataIndex: 'traceId', width: 197, ellipsis: true }, { title: 'CorrelationId', dataIndex: 'correlationId', width: 191, ellipsis: true },
      { title: '操作', width: 70, fixed: 'right', render: (_, item) => <Button type="link" className="table-link" onClick={() => navigate('/audit/requests/' + encodeURIComponent(item.id))}>查看</Button> },
    ]} /></SectionCard>
  </>;
}
