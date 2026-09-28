import { App, Badge, Button, Descriptions, Drawer, Select, Space, Tag, Timeline, Typography } from 'antd';
import { ArrowUpOutlined, BellOutlined, ClockCircleOutlined, DesktopOutlined } from '@ant-design/icons';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { useLiveEvents } from '../../data/stores/operations';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { LiveEvent } from '../../data/mock/operations';

export function LiveOperationsPage() {
  const navigate = useNavigate();
  const { message, modal } = App.useApp();
  const [liveEvents, setLiveEvents] = useLiveEvents();
  const [site, setSite] = useState('全部站点');
  const [line, setLine] = useState('全部产线');
  const [applied, setApplied] = useState({ site: '全部站点', line: '全部产线' });
  const [selectedId, setSelectedId] = useState(2);
  const [drawerOpen, setDrawerOpen] = useState(true);
  const selected = liveEvents.find(item => item.id === selectedId);
  const filtered = useMemo(() => liveEvents.filter(item => applied.line === '全部产线' || item.line === applied.line), [liveEvents, applied]);
  const mark = () => {
    if (!selected) return;
    setLiveEvents(current => current.map(item => item.id === selected.id ? { ...item, status: '已处理' } : item));
    message.success('事件已标记为已处理（本地演示）');
  };
  return <>
    <PageIntro title="实时运行态势" planned description="实时展示分拣系统的设备运行状态与事件动态。" action={<Space><Tag className="live-connection-tag"><Badge status="success" text="已连接" /></Tag><Typography.Text type="secondary">最后更新时间：2026-09-25 16:28:40</Typography.Text></Space>} />
    <SectionCard className="filter-card"><div className="filter-grid">
      <Field label="站点"><Select value={site} onChange={setSite} options={['全部站点', '华东分拨中心', '华南分拨中心'].map(value => ({ value }))} /></Field>
      <Field label="产线"><Select value={line} onChange={setLine} options={['全部产线', '产线 1', '产线 2', '产线 3'].map(value => ({ value }))} /></Field>
      <FilterActions onSearch={() => setApplied({ site, line })} onReset={() => { setSite('全部站点'); setLine('全部产线'); setApplied({ site: '全部站点', line: '全部产线' }); }} />
    </div></SectionCard>
    <div className="three-cols live-metrics">
      <div className="metric-card live-metric"><DesktopOutlined className="metric-side-icon" /><div><div className="metric-title">当前在线设备</div><div className="metric-number">156 <small style={{ fontSize: 16, color: '#7a89a1' }}>/ 168</small> <small style={{ color: '#0aa661', fontSize: 15 }}><ArrowUpOutlined /> 98.2%</small></div><div className="metric-caption">较昨日 +2</div></div></div>
      <div className="metric-card live-metric"><BellOutlined className="metric-side-icon warning" /><div><div className="metric-title">待处理事件</div><div className="metric-number">{5 + filtered.filter(item => item.status === '待处理').length} <small style={{ color: '#e94c4c', fontSize: 15 }}><ArrowUpOutlined /> +3</small></div><div className="metric-caption">较昨日 +3</div></div></div>
      <div className="metric-card live-metric"><ClockCircleOutlined className="metric-side-icon purple" /><div><div className="metric-title">最近更新时间</div><div style={{ fontSize: 16, fontWeight: 700, marginTop: 9, whiteSpace: 'nowrap', letterSpacing: '-.4px' }}>2026-09-25 16:28:40</div><div className="metric-caption">实时数据</div></div></div>
    </div>
    <div style={{ height: 18 }} />
    <SectionCard title="事件动态" className="live-table"><DataTable dataSource={filtered} tableLayout="fixed" scroll={{ x: undefined }} rowClassName={item => drawerOpen && item.id === selectedId ? 'selected-table-row' : ''} columns={[
      { title: '时间', dataIndex: 'time', width: 172 }, { title: '产线', dataIndex: 'line', width: 98 }, { title: '设备', dataIndex: 'device', width: 105 },
      { title: '事件类型', dataIndex: 'type', width: 115, render: (value: string) => <StatusTag value={value} /> }, { title: '关联包裹', dataIndex: 'parcel', width: 156 },
      { title: '状态', dataIndex: 'status', width: 98, render: (value: string) => <StatusTag value={value} /> },
      { title: '操作', render: (_, item: LiveEvent) => <Button type="link" className="table-link" onClick={() => { setSelectedId(item.id); setDrawerOpen(true); }}>查看</Button> },
    ]} /></SectionCard>
    <Drawer title="事件详情" open={drawerOpen} onClose={() => setDrawerOpen(false)} width={347} mask={false} rootClassName="reference-drawer live-drawer" footer={<Space><Button type="primary" onClick={mark} disabled={selected?.status === '已处理'}>标记为已处理</Button><Button onClick={() => modal.info({ title: '演示工单已创建', content: `事件 ${selected?.device || ''} 已生成本地演示工单。` })}>创建工单</Button></Space>}>
      {selected && <><Space style={{ marginBottom: 18 }}><StatusTag value={selected.type} /><StatusTag value={selected.status} /></Space>
        <Descriptions className="drawer-meta" column={1} size="small" colon={false} items={[{ key: '1', label: '事件时间', children: selected.time }, { key: '2', label: '产线', children: selected.line }, { key: '3', label: '设备', children: selected.device }, { key: '4', label: '关联包裹', children: selected.parcel === '-' ? '-' : <Button type="link" className="table-link live-parcel-link" onClick={() => { const id = selected.parcel.includes('YT') ? 2509250003 : 2509250004; navigate(`/parcels/${id}`); }}>{selected.parcel}</Button> }, { key: '5', label: '状态', children: <StatusTag value={selected.status} /> }]} />
        <div className="live-section-divider" style={{ borderTop: '1px solid #e5ebf3', margin: '25px 0' }} />
        <h3>事件描述</h3><div className="live-event-description" style={{ border: '1px solid #e1e8f1', background: '#f8fbff', padding: 12, borderRadius: 5 }}>{selected.description}</div>
        <h3 style={{ marginTop: 32 }}>处理记录</h3><Timeline items={[{ children: <><div className="live-record-time">{selected.time}</div><b>系统告警</b><div className="text-muted">{selected.type === '包裹卡滞' ? '检测到包裹卡滞' : selected.description}</div></> }, { color: 'gray', children: <><b>{selected.status}</b><div className="text-muted">{selected.status === '已处理' ? '处理已完成' : '等待运维人员处理'}</div></> }]} />
      </>}
    </Drawer>
  </>;
}
