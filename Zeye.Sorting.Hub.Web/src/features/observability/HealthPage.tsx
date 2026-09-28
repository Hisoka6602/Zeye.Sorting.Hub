import { Badge, Button, Descriptions, Table, Tag } from 'antd';
import { CheckCircleFilled, ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useState } from 'react';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';

const healthChecks = [
  { key: 'MySQL', status: '正常', latency: '18 ms' }, { key: 'SQL Server', status: '正常', latency: '22 ms' },
  { key: 'Redis', status: '正常', latency: '6 ms' }, { key: 'Hangfire', status: '正常', latency: '24 ms' },
];
export function HealthPage() {
  const [checked, setChecked] = useState('2026-09-25 16:28:40');
  const [loading, setLoading] = useState(false);
  const refresh = () => { setLoading(true); window.setTimeout(() => { setChecked(dayjs().format('YYYY-MM-DD HH:mm:ss')); setLoading(false); }, 500); };
  return <div className="health-page">
    <PageIntro title="健康检查" description="检查系统各项服务与依赖的运行状态，确保系统健康可用。" action={<div style={{ textAlign: 'right' }}><Button type="primary" icon={<ReloadOutlined spin={loading} />} onClick={refresh}>刷新检查</Button><div className="text-muted" style={{ marginTop: 8 }}>上次检查：{checked}</div></div>} />
    <InfoAlert message={<div className="health-alert-head"><b>健康状态说明</b><span><Badge status="success" text="Healthy 正常" /><Badge status="error" text="Unhealthy 异常" /><Badge status="default" text="Unknown 未知" /></span></div>} description="用于检查应用自身及依赖服务的可用性。/health/live 仅用于判断应用是否存活；/health/ready 检查基础依赖；/health/deep 进行更深入的依赖检查。" closable={false} />
    <div className="three-cols">{[
      { path: '/health/live', title: '应用存活检查', live: true }, { path: '/health/ready', title: '就绪性检查（基础依赖）', live: false }, { path: '/health/deep', title: '深度检查（扩展依赖）', live: false },
    ].map(item => <SectionCard key={item.path} className="health-card">
      <div className="health-head"><span className="health-name">{item.path}</span><Tag className="health-status"><Badge status="success" />Healthy</Tag></div><div className="health-caption">{item.title}</div>
      {item.live ? <div className="health-summary"><CheckCircleFilled className="health-success-icon" /><div><b>应用运行正常</b><div className="health-summary-description" style={{ marginTop: 7 }}>服务已启动并正常运行。</div></div></div> : <Table
        className="health-check-table"
        rowKey="key"
        bordered
        size="small"
        tableLayout="fixed"
        pagination={false}
        dataSource={healthChecks.map(check => ({ ...check, latency: item.path === '/health/deep' && check.key === 'SQL Server' ? '21 ms' : item.path === '/health/deep' && check.key === 'Hangfire' ? '25 ms' : check.latency }))}
        columns={[
          { title: '检查项', dataIndex: 'key', width: '38.3%' },
          { title: '状态', dataIndex: 'status', width: '30.6%', render: (status: string) => <StatusTag value={status} /> },
          { title: '响应时间', dataIndex: 'latency', width: '31.1%' },
        ]}
      />}
      <DividerLine />
      <Descriptions size="small" column={1} colon={false} className="drawer-meta" items={item.live ? [
        { key: '1', label: '检查时间', children: checked }, { key: '2', label: '响应时间', children: '12 ms' }, { key: '3', label: '检查说明', children: <span>仅验证应用进程是否存活；<br />不包含任何外部依赖检查。</span> },
      ] : [{ key: '1', label: '检查时间', children: checked }, { key: '2', label: '总项数', children: 4 }, { key: '3', label: '成功数', children: 4 }, { key: '4', label: '失败数', children: 0 }]} />
    </SectionCard>)}</div>
  </div>;
}

function DividerLine() { return <div className="health-divider" style={{ borderTop: '1px solid #e2e9f3', margin: '18px 0' }} />; }
