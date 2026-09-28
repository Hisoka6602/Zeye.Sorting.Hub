import { App, Button, Empty, Tabs } from 'antd';
import { FileOutlined } from '@ant-design/icons';
import { useParams } from 'react-router';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { slowQueries } from '../../data/mock/observability';

export function SlowQueryDetailPage() {
  const { fingerprint } = useParams();
  const query = fingerprint === '8a3f2c1d'
    ? { ...slowQueries[0], fingerprint, calls: 1248, average: 412, p95: 980, p99: 1562, max: 2134, lastSeen: '2026-09-25 16:28:40' }
    : slowQueries.find(item => item.fingerprint === fingerprint);
  const { message } = App.useApp();
  if (!query) return <><PageIntro title="慢查询详情" back="/diagnostics/slow-queries" /><SectionCard><Empty description="当前快照已过期或指纹不存在" /></SectionCard></>;
  const sqlPanel = (sql: string) => <><div className="sql-heading"><b>SQL 语句</b><Button icon={<FileOutlined />} onClick={() => navigator.clipboard.writeText(sql).then(() => message.success('SQL 已复制'))}>复制</Button></div><div className="sql-code">{sql.split('\n').map((line, index) => <div className="sql-line" key={index}><span className="sql-line-no">{index + 1}</span><code>{line.split(/(LEFT JOIN|ORDER BY|SELECT|FROM|JOIN|WHERE|AND|ON|IN|DESC|LIMIT|'[^']*'|\b\d+\b)/g).map((part, partIndex) => <span key={partIndex} className={/^(LEFT JOIN|ORDER BY|SELECT|FROM|JOIN|WHERE|AND|ON|IN|DESC|LIMIT)$/.test(part) ? 'sql-keyword' : /^'/.test(part) ? 'sql-string' : /^\d+$/.test(part) ? 'sql-number' : ''}>{part}</span>)}</code></div>)}</div></>;
  return <div className="slow-query-detail-page">
    <PageIntro title="慢查询详情" description={`指纹：${query.fingerprint}　│　最后出现时间：${query.lastSeen}`} />
    <SectionCard className="slow-detail-metrics"><div className="query-metrics">{[['调用次数', query.calls, ''], ['平均耗时', query.average, 'ms'], ['P95', query.p95, 'ms'], ['P99', query.p99, 'ms'], ['最大耗时', query.max, 'ms'], ['超时', query.timeout, ''], ['错误', query.errors, ''], ['死锁', query.deadlocks, '']].map(([label, value, unit]) => <div key={label}><span className="text-muted">{label}</span><strong>{value}<small>{unit}</small></strong></div>)}</div></SectionCard>
    <SectionCard className="slow-detail-sql"><Tabs items={[{ key: 'standard', label: '标准 SQL', children: sqlPanel(query.sql) }, { key: 'sample', label: '样例 SQL', children: sqlPanel(query.sample) }]} /></SectionCard>
    <SectionCard title="当前内存观测窗口" className="slow-detail-window"><div className="two-cols"><div><div className="text-muted">开始时间</div><p>2026-09-25 15:28:40</p></div><div><div className="text-muted">结束时间</div><p>2026-09-25 16:28:40</p></div></div><InfoAlert message="仅反映当前进程的观测窗口。" closable={false} /></SectionCard>
  </div>;
}
