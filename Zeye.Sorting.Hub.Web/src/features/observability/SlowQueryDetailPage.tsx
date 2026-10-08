import { formatNumber } from '../../data/formatNumber';
import { App, Button, Empty, Spin, Tabs } from 'antd';
import { FileOutlined } from '@ant-design/icons';
import { useParams } from 'react-router';
import { ApiFeedback } from '../../components/ApiFeedback';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { useApiResource } from '../../data/api/useApiResource';
import { localTime, type SlowQuery } from '../../data/api/operationalTypes';
export function SlowQueryDetailPage() {
  const { fingerprint } = useParams();
  const resource = useApiResource<SlowQuery>(fingerprint ? '/api/diagnostics/slow-queries/' + encodeURIComponent(fingerprint) : null);
  const query = resource.data;
  const isQuery = !query?.kind || query.kind === 'query';
  const { message } = App.useApp();
  const copy = async (sql: string) => { try { await navigator.clipboard.writeText(sql); message.success((isQuery ? 'SQL' : '观测动作') + ' 已复制'); } catch { message.error('复制失败，请检查浏览器权限'); } };
  const sqlPanel = (sql: string) => <><div className="sql-heading"><b>{isQuery ? 'SQL 语句' : '观测动作'}</b><Button icon={<FileOutlined />} onClick={() => copy(sql)}>复制</Button></div><div className="sql-code">{sql.split('\n').map((line, index) => <div className="sql-line" key={index}><span className="sql-line-no">{index + 1}</span><code>{line}</code></div>)}</div></>;
  return <div className="slow-query-detail-page">
    <PageIntro title="慢查询详情" description={'指纹：' + (fingerprint ?? '')} back="/diagnostics/slow-queries" />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    {resource.loading ? <Spin /> : !query ? !resource.error && <Empty description="当前快照已过期或指纹不存在" /> : <>
      <SectionCard className="slow-detail-metrics"><div className="query-metrics">{[
        ['采集样本数', query.callCount, ''], ['平均耗时', query.averageElapsedMilliseconds, 'ms'], ['P95', query.p95Milliseconds, 'ms'],
        ['P99', query.p99Milliseconds, 'ms'], ['最大耗时', query.maxMilliseconds, 'ms'], ['超时', query.timeoutCount, ''], ['错误', query.errorCount, ''], ['死锁', query.deadlockCount, ''],
        [query.kind === 'request' ? '数据库累计耗时' : '执行耗时', query.averageExecuteMilliseconds, 'ms'], ['读取调用耗时', query.averageReadMilliseconds, 'ms'],
        ['物化与消费耗时', query.averageConsumerMilliseconds, 'ms'], ['连接等待', query.averageConnectionMilliseconds, 'ms'],
        ['取消', query.canceledCount, ''], ['提前结束读取', query.partialReadCount, ''], ['累计读取行数', query.totalRowsRead, ''], ['最近命令数', query.latestCommandCount, ''],
      ].map(([label, value, unit]) => <div key={label}><span className="text-muted">{label}</span><strong>{typeof value === 'number' ? formatNumber(value) : value}<small>{unit}</small></strong></div>)}</div></SectionCard>
      <InfoAlert closable={false} message={`类别：${query.kind === 'request' ? '请求累计' : query.kind === 'connection' ? '连接等待' : query.kind === 'transaction' ? '事务等待' : 'SQL 执行与读取'} · 提供器：${query.provider || '未提供'} · 用途：${query.databaseRole === 'configuration' ? '配置历史' : query.databaseRole === 'maintenance' ? '自动调优' : '业务数据'}`}
        description={<><div>TraceId：{query.traceId || '未提供'} · SpanId：{query.spanId || '未提供'} · 命令标识：{query.commandId || '未提供'}</div>
          <div>{query.exceptionType ? '最近异常：' + query.exceptionType + ' · ' : ''}{query.statusCode ? '响应状态：' + query.statusCode + ' · ' : ''}执行和读取是提供器调用耗时；消费包含读取器存续期间的物化、应用处理与等待。请求的数据库累计耗时可能包含并行操作，不能与请求总时长直接相减。</div></>} />
      <SectionCard className="slow-detail-sql"><Tabs items={isQuery ? [{ key: 'standard', label: '标准 SQL', children: sqlPanel(query.normalizedSql) }, { key: 'sample', label: '样例 SQL', children: sqlPanel(query.sampleSql) }] : [{ key: 'action', label: '观测动作', children: sqlPanel(query.sampleSql) }]} /></SectionCard>
      <SectionCard title="有效观测窗口" className="slow-detail-window"><div className="two-cols"><div><div className="text-muted">开始时间</div><p>{localTime(query.windowStartedAtLocal)}</p></div><div><div className="text-muted">结束时间</div><p>{localTime(query.windowEndedAtLocal)}</p></div></div><InfoAlert message={'统计仅覆盖被采集并仍有效的样本，包含后台归档恢复的样本。最近出现：' + localTime(query.lastOccurredAtLocal)} closable={false} /></SectionCard>
    </>}
  </div>;
}
