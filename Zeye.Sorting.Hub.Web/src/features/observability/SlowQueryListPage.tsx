import { Button, Input, InputNumber, Select } from 'antd';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { ApiFeedback } from '../../components/ApiFeedback';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { useApiResource } from '../../data/api/useApiResource';
import { localTime, type SlowQuerySnapshot } from '../../data/api/operationalTypes';
import { formatNumber, formatNumericInput } from '../../data/formatNumber';
export function SlowQueryListPage() {
  const navigate = useNavigate();
  const resource = useApiResource<SlowQuerySnapshot>('/api/diagnostics/slow-queries');
  const [fingerprint, setFingerprint] = useState('');
  const [minimumMs, setMinimumMs] = useState<number | null>(null);
  const [minimumCalls, setMinimumCalls] = useState<number | null>(null);
  const [errorType, setErrorType] = useState<string>();
  const [kind, setKind] = useState<string>();
  const [applied, setApplied] = useState({ fingerprint: '', ms: 0, calls: 0, error: '', kind: '' });
  const search = () => setApplied({ fingerprint: fingerprint.trim(), ms: minimumMs || 0, calls: minimumCalls || 0, error: errorType || '', kind: kind || '' });
  const reset = () => { setFingerprint(''); setMinimumMs(null); setMinimumCalls(null); setErrorType(undefined); setKind(undefined); setApplied({ fingerprint: '', ms: 0, calls: 0, error: '', kind: '' }); };
  const collection = resource.data?.collection;
  const filtered = useMemo(() => (resource.data?.items ?? []).filter(item =>
    (!applied.fingerprint || item.fingerprint.includes(applied.fingerprint)) && item.averageElapsedMilliseconds >= applied.ms && item.callCount >= applied.calls &&
    (!applied.kind || (item.kind ?? 'query') === applied.kind) &&
    (!applied.error || (applied.error === '超时' ? item.timeoutCount > 0 : applied.error === '错误' ? item.errorCount > 0 : applied.error === '取消' ? (item.canceledCount ?? 0) > 0 : item.deadlockCount > 0))
  ), [resource.data, applied]);
  return <>
    <PageIntro title="慢查询画像" description="分析 SQL 执行、结果读取、连接与事务等待及请求累计耗时，定位性能瓶颈。" />
    <InfoAlert message={'观测窗口快照，生成时间：' + localTime(resource.data?.generatedAtLocal)}
      description={collection ? `采集${collection.enabled ? '已开启' : '已关闭'} · 阈值 ${formatNumber(collection.thresholdMilliseconds)} ms · 窗口 ${collection.windowMinutes} 分钟 · 归档${!collection.archiveEnabled ? '已关闭' : collection.archiveReady ? '可用' : '初始化中或不可用'} · 已恢复 ${formatNumber(collection.restoredSamples)} 个样本` : '服务端尚未提供分阶段采集状态。'} />
    {collection && (collection.activeOperations > 0 || collection.collectionFailures > 0 || collection.archivePending > 0 || collection.archiveDropped > 0 || collection.capacityEvictions > 0 || collection.expiredSamples > 0) ?
      <InfoAlert type={collection.collectionFailures || collection.archiveDropped ? 'warning' : 'info'} closable={false}
        message={`进行中 ${collection.activeOperations} · 最久 ${formatNumber(collection.oldestActiveMilliseconds)} ms · 容量淘汰 ${collection.capacityEvictions} · 正常过期 ${collection.expiredSamples} · 采集失败 ${collection.collectionFailures} · 归档待写 ${collection.archivePending} / 丢失 ${collection.archiveDropped}`}
        description={collection.oldestActiveTraceId ? '最久操作 TraceId：' + collection.oldestActiveTraceId : '错误和取消也会采集。空列表仅表示当前有效窗口没有匹配样本。'} /> : null}
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    <SectionCard className="filter-card slow-query-filter" title="当前快照内筛选"><div className="filter-grid">
      <Field label="观测指纹"><Input placeholder="请输入观测指纹" value={fingerprint} onChange={event => setFingerprint(event.target.value)} onPressEnter={search} /></Field>
      <Field label="耗时类别"><Select allowClear placeholder="全部类别" value={kind} onChange={setKind} options={[{ value: 'query', label: 'SQL 执行与读取' }, { value: 'connection', label: '连接等待' }, { value: 'transaction', label: '事务等待' }, { value: 'request', label: '请求累计' }]} /></Field>
      <Field label="最小平均耗时（ms）"><InputNumber formatter={formatNumericInput} min={0} placeholder="请输入" value={minimumMs} onChange={setMinimumMs} style={{ width: '100%' }} /></Field>
      <Field label="最小调用次数"><InputNumber min={0} precision={0} placeholder="请输入" value={minimumCalls} onChange={setMinimumCalls} style={{ width: '100%' }} /></Field>
      <Field label="错误类型"><Select allowClear placeholder="请选择错误类型" value={errorType} onChange={setErrorType} options={['超时', '错误', '死锁', '取消'].map(value => ({ value }))} /></Field>
      <FilterActions onSearch={search} onReset={reset} extra={<Button onClick={resource.refresh} loading={resource.loading}>刷新</Button>} />
    </div></SectionCard>
    <SectionCard className="table-card slow-query-table"><DataTable loading={resource.loading} dataSource={filtered}
      locale={{ emptyText: collection && !collection.enabled ? '慢查询采集已关闭' : '当前观测窗口没有匹配样本；请查看采集状态和进行中的操作' }} columns={[
      { title: '类别', dataIndex: 'kind', width: 145, render: value => value === 'request' ? '请求累计' : value === 'connection' ? '连接等待' : value === 'transaction' ? '事务等待' : 'SQL 执行与读取' },
      { title: '指纹', dataIndex: 'fingerprint', width: 183 }, { title: '采集样本数', dataIndex: 'callCount', width: 120 },
      { title: '平均耗时 ms', dataIndex: 'averageElapsedMilliseconds', width: 135 }, { title: 'P95 ms', dataIndex: 'p95Milliseconds', width: 104 }, { title: 'P99 ms', dataIndex: 'p99Milliseconds', width: 108 },
      { title: '执行 / 读取 / 消费 ms', width: 215, render: (_, row) => [row.averageExecuteMilliseconds, row.averageReadMilliseconds, row.averageConsumerMilliseconds].map(value => formatNumber(value)).join(' / ') },
      { title: '最大耗时 ms', dataIndex: 'maxMilliseconds', width: 132 }, { title: '超时 / 错误 / 死锁 / 取消', width: 220, render: (_, row) => [row.timeoutCount, row.errorCount, row.deadlockCount, row.canceledCount ?? 0].join(' / ') },
      { title: '最近 TraceId', dataIndex: 'traceId', width: 280 },
      { title: '最近出现', dataIndex: 'lastOccurredAtLocal', width: 196, render: localTime }, { title: '操作', render: (_, row) => <Button type="link" className="table-link" onClick={() => navigate('/diagnostics/slow-queries/' + encodeURIComponent(row.fingerprint))}>查看</Button> },
    ]} /></SectionCard>
  </>;
}
