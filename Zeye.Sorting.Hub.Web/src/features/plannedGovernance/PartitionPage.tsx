import { App, Button, DatePicker, Descriptions, Drawer, Dropdown, Form, Select, Space, Tag } from 'antd';
import { DownOutlined, EllipsisOutlined, SettingOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useMemo, useState } from 'react';
import { usePartitions } from '../../data/stores/plannedGovernance';
import { DataTable } from '../../components/DataTable';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { VectorIcon } from '../../components/VectorIcon';
import type { Partition } from '../../data/mock/plannedGovernance';

export function PartitionPage() {
  const { message, modal } = App.useApp();
  const [partitions, setPartitions] = usePartitions();
  const [source, setSource] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>([dayjs('2026-01'), dayjs('2026-09')]);
  const [applied, setApplied] = useState({ source: '', status: '', from: '', to: '' });
  const [drawer, setDrawer] = useState<'create' | 'detail' | null>(null);
  const [selected, setSelected] = useState<Partition | null>(null);
  const [form] = Form.useForm();
  const filtered = useMemo(() => partitions.filter(item => (!applied.source || item.source === applied.source) && (!applied.status || item.status === applied.status) && (!applied.from || item.month >= applied.from) && (!applied.to || item.month <= applied.to)), [applied, partitions]);
  const create = (values: { month: Dayjs; source: 'MySQL' | 'SQL Server' }) => {
    const month = values.month.format('YYYY-MM');
    if (partitions.some(item => item.month === month && item.source === values.source)) { message.error('该月分区已存在'); return; }
    const id = Math.max(...partitions.map(item => item.id), 6) + 1;
    setPartitions(current => [{ id, month, source: values.source, table: `parcel_${month.replace('-', '')}`, rows: 0, size: '0 GB', status: '待审核', retention: dayjs(month).add(2, 'year').endOf('month').format('YYYY-MM-DD') }, ...current]);
    setDrawer(null); form.resetFields(); message.success('分区规划已添加到本地演示列表');
  };
  const preview = (item: Partition) => modal.info({ title: `${item.table} 分区预览`, content: <Descriptions column={1} items={[{ key: '1', label: '数据库', children: item.source }, { key: '2', label: '月份', children: item.month }, { key: '3', label: '预计状态', children: item.status }, { key: '4', label: '保留边界', children: item.retention }]} /> });
  return <>
    <PageIntro title="分区管理" planned description="管理包裹记录的按月分区（支持 MySQL 与 SQL Server），查看历史分区与下月规划。" action={<Button type="primary" onClick={() => setDrawer('create')}>新建分区</Button>} />
    <InfoAlert message={<b>请操作人员审核</b>} description="下月分区为系统根据规则生成的规划内容，请在执行前完成业务与存储评估，并由负责人审核确认。" />
    <div className="two-cols partition-top">
      <SectionCard title={<><VectorIcon name="calendar" className="partition-calendar-title" size={24} color="#1677ff" />下月分区规划预览</>} extra={<Tag className="planned-tag">规划稿</Tag>}>
        <div className="partition-plan-banner"><VectorIcon name="calendar" className="partition-calendar-banner" size={30} color="#1677ff" /><div><b>2026-10</b><div className="text-muted">预计创建以下分区（待审核）</div></div></div>
        <DataTable className="partition-plan-table" tableLayout="fixed" scroll={{ x: undefined }} dataSource={[{ id: 1, source: 'MySQL', table: 'parcel_202610', range: '2026-10-01 ~ 2026-10-31', estimated: <>约 1,200 万行<br />约 18 GB</> }, { id: 2, source: 'SQL Server', table: 'parcel_202610', range: '2026-10-01 ~ 2026-10-31', estimated: <>约 1,200 万行<br />约 18 GB</> }]} pagination={false} columns={[
          { title: '数据库类型', dataIndex: 'source', width: 118 }, { title: '分区表名', dataIndex: 'table', width: 138 }, { title: '时间范围', dataIndex: 'range', width: 235 }, { title: '预估数据量', dataIndex: 'estimated', width: 140 }, { title: '状态', render: () => <StatusTag value="待审核" /> },
        ]} />
      </SectionCard>
      <SectionCard title={<><SettingOutlined style={{ color: '#1677ff', marginRight: 10 }} />分区规则</>}>
        <Descriptions className="drawer-meta" column={1} size="small" colon={false} items={[
          { key: '1', label: '分区粒度', children: '按月（YYYYMM）' }, { key: '2', label: '支持的数据库', children: 'MySQL、SQL Server' },
          { key: '3', label: '保留策略', children: '保留最近 24 个月，超出后按业务策略归档或删除' },
          { key: '4', label: '分区命名规则', children: 'parcel_YYYYMM' }, { key: '5', label: '创建时间', children: '每月 1 日 00:05（系统任务）' },
        ]} />
      </SectionCard>
    </div>
    <SectionCard title="历史分区列表" className="partition-history-card" extra={<Space size={12}><Select allowClear placeholder="全部数据库类型" value={source || 'all'} onChange={value => setSource(value === 'all' ? undefined : value)} style={{ width: 145 }} options={[{ value: 'all', label: '全部数据库类型' }, ...['MySQL', 'SQL Server'].map(value => ({ value }))]} /><DatePicker.RangePicker picker="month" separator="~" value={range} onChange={setRange} style={{ width: 220 }} /><Select allowClear placeholder="全部状态" value={status || 'all'} onChange={value => setStatus(value === 'all' ? undefined : value)} style={{ width: 112 }} options={[{ value: 'all', label: '全部状态' }, ...['正常', '待审核'].map(value => ({ value }))]} /><FilterActions onSearch={() => setApplied({ source: source || '', status: status || '', from: range?.[0]?.format('YYYY-MM') || '', to: range?.[1]?.format('YYYY-MM') || '' })} onReset={() => { setSource(undefined); setStatus(undefined); setRange([dayjs('2026-01'), dayjs('2026-09')]); setApplied({ source: '', status: '', from: '', to: '' }); }} extra={<Button onClick={() => message.info('所有筛选条件已显示')}>更多筛选 <DownOutlined /></Button>} /></Space>}>
      <DataTable tableLayout="fixed" scroll={{ x: undefined }} dataSource={filtered} columns={[
        { title: '月份', dataIndex: 'month', width: 109 }, { title: '数据库类型', dataIndex: 'source', width: 136 }, { title: '分区表名', dataIndex: 'table', width: 158 },
        { title: '行数', dataIndex: 'rows', width: 146, render: (value: number) => value.toLocaleString() }, { title: '数据大小', dataIndex: 'size', width: 128 },
        { title: '状态', dataIndex: 'status', width: 139, render: (value: string) => <StatusTag value={value} /> }, { title: '保留边界', dataIndex: 'retention', width: 205 },
        { title: '操作', width: 220, render: (_, item: Partition) => <Space size={30}><Button type="link" className="table-link" onClick={() => { setSelected(item); setDrawer('detail'); }}>查看</Button><Button type="link" className="table-link" onClick={() => preview(item)}>预览</Button><Dropdown menu={{ items: [{ key: 'plan', label: '查看规划说明', onClick: () => message.info(`${item.table} 的保留边界：${item.retention}`) }] }} trigger={['click']}><Button type="link" className="table-link table-more" aria-label={`${item.table} 更多操作`} icon={<EllipsisOutlined />} /></Dropdown></Space> },
      ]} />
    </SectionCard>
    <Drawer title={drawer === 'create' ? '新建分区规划' : '分区详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={420} footer={drawer === 'create' ? <Space><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" onClick={() => form.submit()}>保存规划</Button></Space> : null}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={create} initialValues={{ month: dayjs('2026-10') }}><InfoAlert message="此操作仅保存本地规划记录。" closable={false} /><Form.Item name="month" label="分区月份" rules={[{ required: true }]}><DatePicker picker="month" style={{ width: '100%' }} /></Form.Item><Form.Item name="source" label="数据库类型" rules={[{ required: true }]}><Select options={['MySQL', 'SQL Server'].map(value => ({ value }))} /></Form.Item></Form>
        : selected && <Descriptions className="drawer-meta" column={1} items={[{ key: '1', label: '月份', children: selected.month }, { key: '2', label: '数据库类型', children: selected.source }, { key: '3', label: '物理表名', children: selected.table }, { key: '4', label: '行数', children: selected.rows.toLocaleString() }, { key: '5', label: '数据大小', children: selected.size }, { key: '6', label: '状态', children: <StatusTag value={selected.status} /> }, { key: '7', label: '保留边界', children: selected.retention }]} />}
    </Drawer>
  </>;
}
