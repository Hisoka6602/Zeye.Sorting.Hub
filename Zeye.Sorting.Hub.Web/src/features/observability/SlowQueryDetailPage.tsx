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
  const { message } = App.useApp();
  const copy = async (sql: string) => { try { await navigator.clipboard.writeText(sql); message.success('SQL 已复制'); } catch { message.error('复制失败，请检查浏览器权限'); } };
  const sqlPanel = (sql: string) => <><div className="sql-heading"><b>SQL 语句</b><Button icon={<FileOutlined />} onClick={() => copy(sql)}>复制</Button></div><div className="sql-code">{sql.split('\n').map((line, index) => <div className="sql-line" key={index}><span className="sql-line-no">{index + 1}</span><code>{line}</code></div>)}</div></>;
  return <div className="slow-query-detail-page">
    <PageIntro title="慢查询详情" description={'指纹：' + (fingerprint ?? '')} back="/diagnostics/slow-queries" />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    {resource.loading ? <Spin /> : !query ? !resource.error && <Empty description="当前快照已过期或指纹不存在" /> : <>
      <SectionCard className="slow-detail-metrics"><div className="query-metrics">{[
        ['调用次数', query.callCount, ''], ['平均耗时', query.averageElapsedMilliseconds, 'ms'], ['P95', query.p95Milliseconds, 'ms'],
        ['P99', query.p99Milliseconds, 'ms'], ['最大耗时', query.maxMilliseconds, 'ms'], ['超时', query.timeoutCount, ''], ['错误', query.errorCount, ''], ['死锁', query.deadlockCount, ''],
      ].map(([label, value, unit]) => <div key={label}><span className="text-muted">{label}</span><strong>{typeof value === 'number' ? formatNumber(value) : value}<small>{unit}</small></strong></div>)}</div></SectionCard>
      <SectionCard className="slow-detail-sql"><Tabs items={[{ key: 'standard', label: '标准 SQL', children: sqlPanel(query.normalizedSql) }, { key: 'sample', label: '样例 SQL', children: sqlPanel(query.sampleSql) }]} /></SectionCard>
      <SectionCard title="当前内存观测窗口" className="slow-detail-window"><div className="two-cols"><div><div className="text-muted">开始时间</div><p>{localTime(query.windowStartedAtLocal)}</p></div><div><div className="text-muted">结束时间</div><p>{localTime(query.windowEndedAtLocal)}</p></div></div><InfoAlert message={'仅反映当前进程的观测窗口。最近出现：' + localTime(query.lastOccurredAtLocal)} closable={false} /></SectionCard>
    </>}
  </div>;
}
