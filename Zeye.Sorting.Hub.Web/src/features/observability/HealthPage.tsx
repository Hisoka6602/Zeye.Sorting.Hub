import { formatNumber } from '../../data/formatNumber';
import { Badge, Button, Descriptions, Spin, Tag } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { useApiResource } from '../../data/api/useApiResource';
import { readHealthReport } from '../../data/api/client';
import { type HealthReport } from '../../data/api/operationalTypes';
const probes = [{ path: '/health/live', title: '应用存活检查' }, { path: '/health/ready', title: '就绪性检查（基础依赖）' }, { path: '/health/deep', title: '深度检查（扩展依赖）' }];
const badgeStatus = (status?: string) => status === 'Healthy' ? 'success' : status === 'Degraded' ? 'warning' : status === 'Unhealthy' ? 'error' : 'default';
function HealthProbe({ path, title, resource }: { path: string; title: string; resource: ReturnType<typeof useApiResource<HealthReport>> }) {
  const report = resource.data;
  const entries = Object.entries(report?.entries ?? {}).map(([key, value]) => ({ key, ...value }));
  return <SectionCard className="health-card">
    <div className="health-head"><span className="health-name">{path}</span><Tag><Badge status={badgeStatus(report?.status)} /> {report?.status ?? 'Unknown'}</Tag></div>
    <div className="health-caption">{title}</div><ApiFeedback error={resource.error} retry={resource.refresh} />
    {resource.loading ? <Spin /> : report && (entries.length ? <DataTable className="health-check-table" rowKey="key" size="small" tableLayout="fixed" scroll={{ x: 680 }} pagination={false} dataSource={entries} columns={[
      { title: '检查项', dataIndex: 'key', width: 170 }, { title: '状态', dataIndex: 'status', width: 140, render: (status: string) => <Badge status={badgeStatus(status)} text={status} /> },
      { title: '响应时间', dataIndex: 'durationMs', width: 110, render: (value?: number) => value == null ? '-' : formatNumber(value) + ' ms' },
      { title: '说明', dataIndex: 'description' },
    ]} /> : <p>{report.status === 'Healthy' ? '应用进程存活。此探针不检查外部依赖。' : '应用存活检查异常。'}</p>)}
    <div className="health-divider" style={{ borderTop: '1px solid #e2e9f3', margin: '18px 0' }} />
    <Descriptions column={1} size="small" items={[
      { key: 'time', label: '检查时间', children: report?.generatedAt ?? '-' }, { key: 'total', label: '总项数', children: report ? entries.length : '-' },
      { key: 'success', label: '正常数', children: report ? entries.filter(item => item.status === 'Healthy').length : '-' },
      { key: 'failed', label: '异常 / 降级数', children: report ? entries.filter(item => item.status !== 'Healthy').length : '-' },
    ]} />
  </SectionCard>;
}
export function HealthPage() {
  const live = useApiResource<HealthReport>('/health/live', readHealthReport);
  const ready = useApiResource<HealthReport>('/health/ready', readHealthReport);
  const deep = useApiResource<HealthReport>('/health/deep', readHealthReport);
  const resources = [live, ready, deep];
  const loading = resources.some(item => item.loading);
  const refresh = () => resources.forEach(item => item.refresh());
  return <div className="health-page">
    <PageIntro title="健康检查" description="读取应用和依赖服务的实际运行状态。" action={<Button type="primary" icon={<ReloadOutlined spin={loading} />} loading={loading} onClick={refresh}>刷新检查</Button>} />
    <InfoAlert message={<div className="health-alert-head"><b>健康状态说明</b><span><Badge status="success" text="Healthy 正常" /><Badge status="warning" text="Degraded 降级" /><Badge status="error" text="Unhealthy 异常" /><Badge status="default" text="Unknown 未知" /></span></div>} description="存活检查用于判断进程是否运行；就绪检查验证流量关键依赖；深度检查包含后台治理状态。" closable={false} />
    <div className="three-cols">{probes.map((probe, index) => <HealthProbe key={probe.path} {...probe} resource={resources[index]} />)}</div>
  </div>;
}
