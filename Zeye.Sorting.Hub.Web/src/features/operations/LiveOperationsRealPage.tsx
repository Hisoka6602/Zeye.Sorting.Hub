import { Badge, Button, Select, Space, Typography } from 'antd';
import { BellOutlined, ClockCircleOutlined, DesktopOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { useApiResource } from '../../data/api/useApiResource';
import type { ParcelList, ParcelSummary } from '../../data/api/parcelTypes';

interface HealthResponse { status: string; generatedAt: string }
const statusName = (status: number) => ['待分拣', '已完成', '分拣异常'][status] ?? '未知';

/** 最近包裹和健康探针来自 Host；页面不会凭本地样本声称设备在线。 */
export function LiveOperationsPage() {
  const navigate = useNavigate();
  const parcels = useApiResource<ParcelList>('/api/parcels?pageNumber=1&pageSize=50&includeTotalCount=true');
  const live = useApiResource<HealthResponse>('/health/live');
  const ready = useApiResource<HealthResponse>('/health/ready');
  const [workstation, setWorkstation] = useState<string>();
  const [lastUpdated, setLastUpdated] = useState<string>();

  useEffect(() => {
    if (parcels.data && live.data && ready.data) setLastUpdated(dayjs().format('YYYY-MM-DD HH:mm:ss'));
  }, [parcels.data, live.data, ready.data]);

  const items = parcels.data?.items ?? [];
  const workstations = useMemo(() => [...new Set(items.map(item => item.workstationName).filter(Boolean))].sort(), [items]);
  const filtered = useMemo(() => items.filter(item => !workstation || item.workstationName === workstation), [items, workstation]);
  const abnormalCount = items.filter(item => item.status === 2).length;
  const refresh = () => { parcels.refresh(); live.refresh(); ready.refresh(); };

  return <div className="live-operations-page">
    <PageIntro title="实时运行态势" description="查看服务状态与最近入库包裹，业务变化通过实时通道持续更新。" action={<Space>
      <Typography.Text type="secondary">最后更新：{lastUpdated ?? '等待数据'}</Typography.Text>
      <Button icon={<ReloadOutlined />} onClick={refresh} loading={parcels.loading || live.loading || ready.loading}>立即刷新</Button>
    </Space>} />
    {(parcels.error || live.error || ready.error) && <InfoAlert type="error" closable={false} message="运行数据读取失败" description={[parcels.error?.message, live.error?.message, ready.error?.message].filter(Boolean).join('；')} />}
    <InfoAlert closable={false} message="数据来源：Host 健康探针与最近 50 条包裹记录" description="服务状态来自真实探针；包裹件数只反映当前列表，不代表全站设备总量。" />
    <div className="three-cols live-metrics">
      <div className="metric-card live-metric"><DesktopOutlined className="metric-side-icon" /><div><div className="metric-title">应用存活</div><div className="metric-number"><Badge status={live.data?.status === 'Healthy' ? 'success' : 'error'} text={live.data?.status ?? '未知'} /></div><div className="metric-caption">/health/live</div></div></div>
      <div className="metric-card live-metric"><BellOutlined className="metric-side-icon warning" /><div><div className="metric-title">服务就绪</div><div className="metric-number"><Badge status={ready.data?.status === 'Healthy' ? 'success' : 'error'} text={ready.data?.status ?? '未知'} /></div><div className="metric-caption">/health/ready</div></div></div>
      <div className="metric-card live-metric"><ClockCircleOutlined className="metric-side-icon purple" /><div><div className="metric-title">最近 50 条中的异常包裹</div><div className="metric-number">{parcels.data ? abnormalCount : '—'} <small>件</small></div><div className="metric-caption">当前查询窗口，不作全量统计</div></div></div>
    </div>
    <SectionCard title="最新包裹动态" className="live-table" extra={<Field label="工作台"><Select allowClear placeholder="全部工作台" value={workstation} onChange={setWorkstation} style={{ width: 180 }} options={workstations.map(value => ({ value, label: value }))} /></Field>}>
      <DataTable<ParcelSummary> rowKey="id" loading={parcels.loading} dataSource={filtered} pagination={false} locale={{ emptyText: '暂无来源包裹' }} columns={[
        { title: '入库时间', dataIndex: 'createdTime', width: 180 },
        { title: '条码', dataIndex: 'barCodes', width: 180, render: (value: string, item) => <Button type="link" className="table-link" onClick={() => navigate(`/parcels/${item.id}`)}>{value || item.id}</Button> },
        { title: '工作台', dataIndex: 'workstationName', width: 150, render: (value: string) => value || '未提供' },
        { title: '状态', dataIndex: 'status', width: 120, render: (value: number) => <StatusTag value={statusName(value)} /> },
        { title: '目标格口', dataIndex: 'targetChuteCode', width: 130, render: (value: string | null) => value || '—' },
        { title: '实际格口', dataIndex: 'actualChuteCode', width: 130, render: (value: string | null) => value || '—' },
      ]} />
    </SectionCard>
  </div>;
}
