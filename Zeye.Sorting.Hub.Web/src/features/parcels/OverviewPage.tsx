import { Button } from 'antd';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { Parcel } from '../../data/mock/parcels';
import { useParcels } from '../../data/stores/parcels';
import { parcelColumns } from './parcelColumns';

export function OverviewPage() {
  const navigate = useNavigate();
  const [parcels] = useParcels();
  const recent = [
    { id: 2509251001, scanTime: '2026-09-25 16:12:03', barcode: 'SF3124567890CN', status: '已完成', target: 'A01-01', actual: 'A01-01', workstation: '工作台 1', weight: 1.20 },
    { id: 2509251002, scanTime: '2026-09-25 15:47:21', barcode: 'JD7331982746CN', status: '待分拣', target: 'B02-03', actual: '-', workstation: '工作台 2', weight: 0.56 },
    { id: 2509251003, scanTime: '2026-09-25 14:33:18', barcode: 'YT9988776655CN', status: '已完成', target: 'C03-12', actual: 'C03-12', workstation: '工作台 1', weight: 2.35 },
    { id: 2509251004, scanTime: '2026-09-25 11:20:09', barcode: 'ZTO5566778899CN', status: '分拣异常', target: 'A02-07', actual: '-', workstation: '工作台 3', weight: 0.80 },
  ].map((item, index) => ({ ...parcels[index], ...item } as Parcel));
  const failed = [
    { id: 310001, created: '2026-09-25 15:28:17', event: 'package.status', retries: 3, status: '发送失败' },
    { id: 310002, created: '2026-09-25 13:11:05', event: 'archive.trigger', retries: 5, status: '发送失败' },
    { id: 310003, created: '2026-09-25 10:02:41', event: 'notify.exception', retries: 2, status: '发送失败' },
  ];
  const recentArchives = [
    { id: 420001, created: '2026-09-25 14:20:33', range: '2026-09-24', status: '成功', initiator: '张三' },
    { id: 420002, created: '2026-09-25 10:15:08', range: '2026-09-23', status: '失败', initiator: '李四' },
    { id: 420003, created: '2026-09-25 08:40:12', range: '2026-09-22', status: '成功', initiator: '王五' },
  ];
  return <div className="overview-page">
    <PageIntro title="工作台" description="掌握系统运行状态，快速处理关键事项。" />
    <div className="three-cols overview-metrics">
      {[['存活', '正常', '系统进程运行正常，服务可用。'], ['就绪', '就绪', '依赖服务已就绪，可正常处理业务。'], ['深度诊断', '正常', '系统关键组件健康，未发现异常。']].map(([name, status, detail]) => <div className="metric-card" key={name}>
        <div className="metric-title" style={{ display: 'flex', alignItems: 'center', gap: 24, fontSize: 24 }}>{name}<span className={`overview-state-dot ${name === '深度诊断' ? 'blue' : ''}`} /><StatusTag value={status} tone={name === '深度诊断' ? 'blue' : undefined} /></div>
        <div className="overview-metric-description">{detail}</div>
        <div className="metric-footer">最后检查时间 <strong>2026-09-25 16:28:40</strong></div>
      </div>)}
    </div>
    <div style={{ height: 22 }} />
    <SectionCard className="overview-recent" title="最近包裹记录" extra={<Button type="link" onClick={() => navigate('/parcels')}>查看全部 ›</Button>}>
      <DataTable dataSource={recent} tableLayout="fixed" rowClassName={(_, index) => `overview-parcel-row-${index + 1}`} columns={parcelColumns(navigate).filter(column => column.key !== 'actions').map((column, index) => ({ ...column, title: index === 0 ? '扫描时间' : column.title, sorter: undefined, width: [197, 148, 198, 137, 188, 147, 130][index] }))} pagination={false} />
    </SectionCard>
    <div className="two-cols overview-bottom">
      <SectionCard title="失败的 Outbox 消息" extra={<Button type="link" onClick={() => navigate('/governance/outbox')}>查看全部 ›</Button>}>
        <DataTable dataSource={failed} tableLayout="fixed" scroll={{ x: undefined }} pagination={false} columns={[
          { title: '创建时间', dataIndex: 'created', width: 159 }, { title: '消息 ID', dataIndex: 'id', width: 77 }, { title: '主题', dataIndex: 'event', width: 126 },
          { title: '重试次数', dataIndex: 'retries', width: 81 }, { title: '状态', dataIndex: 'status', render: (value: string) => <StatusTag value={value} /> },
        ]} />
      </SectionCard>
      <SectionCard title="最近归档试运行任务" extra={<Button type="link" onClick={() => navigate('/governance/archive-tasks')}>查看全部 ›</Button>}>
        <DataTable dataSource={recentArchives} tableLayout="fixed" scroll={{ x: undefined }} pagination={false} columns={[
          { title: '创建时间', dataIndex: 'created', width: 170 }, { title: '任务 ID', dataIndex: 'id', width: 88 }, { title: '范围', dataIndex: 'range', width: 114 },
          { title: '状态', dataIndex: 'status', width: 104, render: (value: string) => <StatusTag value={value} /> }, { title: '操作人', dataIndex: 'initiator' },
        ]} />
      </SectionCard>
    </div>
  </div>;
}
