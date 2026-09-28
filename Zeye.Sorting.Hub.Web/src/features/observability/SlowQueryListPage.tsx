import { Button, Input, InputNumber, Select } from 'antd';
import dayjs from 'dayjs';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { slowQueries } from '../../data/mock/observability';

export function SlowQueryListPage() {
  const navigate = useNavigate();
  const [fingerprint, setFingerprint] = useState('');
  const [minimumMs, setMinimumMs] = useState<number | null>(null);
  const [minimumCalls, setMinimumCalls] = useState<number | null>(null);
  const [errorType, setErrorType] = useState<string>();
  const [applied, setApplied] = useState({ fingerprint: '', ms: 0, calls: 0, error: '' });
  const [refreshed, setRefreshed] = useState('2026-09-25 16:20');
  const search = () => setApplied({ fingerprint: fingerprint.trim(), ms: minimumMs || 0, calls: minimumCalls || 0, error: errorType || '' });
  const reset = () => { setFingerprint(''); setMinimumMs(null); setMinimumCalls(null); setErrorType(undefined); setApplied({ fingerprint: '', ms: 0, calls: 0, error: '' }); };
  const filtered = useMemo(() => slowQueries.filter(item =>
    (!applied.fingerprint || item.fingerprint.includes(applied.fingerprint)) && item.average >= applied.ms && item.calls >= applied.calls &&
    (!applied.error || (applied.error === '超时' ? item.timeout > 0 : applied.error === '错误' ? item.errors > 0 : item.deadlocks > 0))
  ), [applied]);
  return <>
    <PageIntro title="慢查询画像" description="从当前进程内的快照中识别耗时较高的 SQL，定位性能瓶颈。" />
    <InfoAlert message={`当前进程内观测窗口快照，快照生成时间：${refreshed}`} />
    <SectionCard className="filter-card slow-query-filter" title="当前快照内筛选"><div className="filter-grid">
      <Field label="SQL 指纹"><Input placeholder="请输入 SQL 指纹" value={fingerprint} onChange={event => setFingerprint(event.target.value)} onPressEnter={search} /></Field>
      <Field label="最小平均耗时（ms）"><InputNumber min={0} placeholder="请输入" value={minimumMs} onChange={setMinimumMs} style={{ width: '100%' }} /></Field>
      <Field label="最小调用次数"><InputNumber min={0} placeholder="请输入" value={minimumCalls} onChange={setMinimumCalls} style={{ width: '100%' }} /></Field>
      <Field label="错误类型"><Select allowClear placeholder="请选择错误类型" value={errorType} onChange={setErrorType} options={['超时', '错误', '死锁'].map(value => ({ value }))} /></Field>
      <FilterActions onSearch={search} onReset={reset} extra={<Button onClick={() => setRefreshed(dayjs().format('YYYY-MM-DD HH:mm'))}>刷新</Button>} />
    </div></SectionCard>
    <SectionCard className="table-card slow-query-table"><DataTable dataSource={filtered} tableLayout="fixed" scroll={{ x: undefined }} columns={[
      { title: 'SQL 指纹', dataIndex: 'fingerprint', width: 183 }, { title: '调用次数', dataIndex: 'calls', width: 114, render: (value: number) => value.toLocaleString() },
      { title: '平均耗时 ms', dataIndex: 'average', width: 135, render: (value: number) => value.toLocaleString('en-US', { minimumFractionDigits: 1 }) }, { title: 'P95 ms', dataIndex: 'p95', width: 104, render: (value: number) => value.toLocaleString('en-US', { minimumFractionDigits: 1 }) }, { title: 'P99 ms', dataIndex: 'p99', width: 108, render: (value: number) => value.toLocaleString('en-US', { minimumFractionDigits: 1 }) },
      { title: '最大耗时 ms', dataIndex: 'max', width: 132, render: (value: number) => value.toLocaleString('en-US', { minimumFractionDigits: 1 }) }, { title: '超时 / 错误 / 死锁', width: 172, render: (_, row) => <><span className="query-timeout">{row.timeout}</span> / {row.errors} / {row.deadlocks}</> },
      { title: '最近出现', dataIndex: 'lastSeen', width: 196 }, { title: '操作', render: (_, row) => <Button type="link" className="table-link" onClick={() => navigate(`/diagnostics/slow-queries/${row.fingerprint}`)}>查看</Button> },
    ]} /></SectionCard>
  </>;
}
