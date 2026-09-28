import { Alert, App, Button, DatePicker, Descriptions, Drawer, Dropdown, Form, Input, InputNumber, Select, Space } from 'antd';
import { CheckCircleFilled, DownOutlined, EllipsisOutlined, SafetyCertificateFilled, SettingOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useMemo, useState } from 'react';
import { useBackupJobs } from '../../data/stores/plannedGovernance';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { BackupJob } from '../../data/mock/plannedGovernance';

export function BackupPage() {
  const { message, modal } = App.useApp();
  const [backupJobs, setBackupJobs] = useBackupJobs();
  const [source, setSource] = useState<string>();
  const [kind, setKind] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>([dayjs('2026-09-01'), dayjs('2026-09-25')]);
  const [applied, setApplied] = useState({ source: '', kind: '', status: '', from: '', to: '' });
  const [selected, setSelected] = useState<BackupJob | null>(null);
  const [drawer, setDrawer] = useState<'create' | 'detail' | 'policy' | null>(null);
  const [policy, setPolicy] = useState({ full: '每周 1 次（周日 02:00）', incremental: '每天 1 次（02:00）', retention: 30 });
  const [form] = Form.useForm();
  const filtered = useMemo(() => backupJobs.filter(item =>
    (!applied.source || item.source === applied.source) && (!applied.kind || item.kind === applied.kind) && (!applied.status || item.status === applied.status) &&
    (!applied.from || item.lastRun.slice(0, 10) >= applied.from) && (!applied.to || item.lastRun.slice(0, 10) <= applied.to)
  ), [applied, backupJobs]);
  const search = () => setApplied({ source: source || '', kind: kind || '', status: status || '', from: range?.[0]?.format('YYYY-MM-DD') || '', to: range?.[1]?.format('YYYY-MM-DD') || '' });
  const reset = () => { setSource(undefined); setKind(undefined); setStatus(undefined); setRange([dayjs('2026-09-01'), dayjs('2026-09-25')]); setApplied({ source: '', kind: '', status: '', from: '', to: '' }); };
  const create = (values: { name: string; source: 'MySQL' | 'SQL Server'; kind: '全量备份' | '增量备份'; retention: number }) => {
    const id = Math.max(...backupJobs.map(item => item.id), 8) + 1;
    setBackupJobs(current => [{ ...values, id, status: '执行中', lastRun: '2026-09-25 16:28:40' }, ...current]);
    setDrawer(null); form.resetFields(); message.success('已创建本地演示备份任务');
  };
  const verify = () => modal.success({ title: '恢复验证已发起', content: '本地演示已记录一次恢复验证。此操作不会连接数据库或恢复文件。' });
  return <>
    <PageIntro title="备份与恢复" planned description="管理数据库备份策略与执行任务，保障数据安全，并支持恢复验证。" action={<Button type="primary" onClick={() => { form.resetFields(); form.setFieldsValue({ retention: 30 }); setDrawer('create'); }}>新建备份任务</Button>} />
    <div className="two-cols backup-top">
      <SectionCard title={<><SafetyCertificateFilled style={{ color: '#1677ff', marginRight: 10 }} />备份策略</>} extra={<Button icon={<SettingOutlined />} onClick={() => { form.resetFields(); form.setFieldsValue({ retention: policy.retention }); setDrawer('policy'); }}>编辑策略</Button>}>
        <Descriptions className="drawer-meta" column={1} size="small" colon={false} items={[{ key: '1', label: '全量备份', children: policy.full }, { key: '2', label: '增量备份', children: policy.incremental }, { key: '3', label: '备份保留期限', children: `${policy.retention} 天` }, { key: '4', label: '备份数据源', children: 'MySQL、SQL Server' }]} />
      </SectionCard>
      <SectionCard title={<><CheckCircleFilled style={{ color: '#13a66d', marginRight: 10 }} />恢复验证</>}>
        <div className="backup-verify-row"><Alert className="page-alert backup-verify-alert" type="info" showIcon message="建议定期进行恢复验证，确保备份文件可用且数据完整。" description="可通过在隔离环境中还原数据进行验证，验证完成后记录结果。" action={<Button onClick={verify}>发起恢复验证</Button>} /></div>
      </SectionCard>
    </div>
    <SectionCard className="table-card backup-table backup-combined"><div className="filter-grid backup-filter">
      <Field label="数据源"><Select allowClear placeholder="请选择数据源" value={source} onChange={setSource} options={['MySQL', 'SQL Server'].map(value => ({ value }))} /></Field>
      <Field label="备份类型"><Select allowClear placeholder="请选择备份类型" value={kind} onChange={setKind} options={['全量备份', '增量备份'].map(value => ({ value }))} /></Field>
      <Field label="状态"><Select allowClear placeholder="请选择状态" value={status} onChange={setStatus} options={['已完成', '执行中', '失败'].map(value => ({ value }))} /></Field>
      <Field label="时间范围" wide><DatePicker.RangePicker separator="~" value={range} onChange={setRange} style={{ width: '100%' }} /></Field>
      <FilterActions onSearch={search} onReset={reset} extra={<Button onClick={() => message.info('所有筛选条件已显示')}>更多筛选 <DownOutlined /></Button>} />
    </div><DataTable dataSource={filtered} columns={[
      { title: '任务名称', dataIndex: 'name', width: 257 }, { title: '数据源', dataIndex: 'source', width: 139 }, { title: '备份类型', dataIndex: 'kind', width: 132 },
      { title: '最近执行', dataIndex: 'lastRun', width: 204 }, { title: '状态', dataIndex: 'status', width: 146, render: (value: string) => <StatusTag value={value} /> },
      { title: '保留期限', dataIndex: 'retention', width: 141, render: (value: number) => `${value} 天` },
      { title: '操作', render: (_, item: BackupJob) => <Space size={30}><Button type="link" className="table-link" onClick={() => { setSelected(item); setDrawer('detail'); }}>查看</Button><Button type="link" className="table-link" onClick={() => modal.confirm({ title: '恢复任务演示', content: `演示恢复 ${item.name}。不会连接数据库或修改真实数据。`, okText: '演示恢复', onOk: () => message.success('演示恢复流程已记录') })}>恢复</Button><Dropdown menu={{ items: [{ key: 'history', label: '查看执行历史', onClick: () => message.info(`${item.name} 的执行历史为本地演示数据`) }] }} trigger={['click']}><Button type="link" className="table-link table-more" aria-label={`${item.name} 更多操作`} icon={<EllipsisOutlined />} /></Dropdown></Space> },
    ]} tableLayout="fixed" /></SectionCard>
    <Drawer title={drawer === 'create' ? '新建备份任务' : drawer === 'policy' ? '编辑备份策略' : '备份任务详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={440} footer={drawer === 'detail' ? <Button onClick={() => setDrawer(null)}>关闭</Button> : <Space><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" onClick={() => drawer === 'create' ? form.submit() : (setPolicy({ full: '每周 1 次（周日 02:00）', incremental: '每天 1 次（02:00）', retention: Number(form.getFieldValue('retention') || 30) }), setDrawer(null), message.success('演示策略已保存'))}>保存</Button></Space>}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={create} initialValues={{ retention: 30 }}><Form.Item name="name" label="任务名称" rules={[{ required: true }]}><Input placeholder="例如 daily-mysql-prod" /></Form.Item><Form.Item name="source" label="数据源" rules={[{ required: true }]}><Select options={['MySQL', 'SQL Server'].map(value => ({ value }))} /></Form.Item><Form.Item name="kind" label="备份类型" rules={[{ required: true }]}><Select options={['全量备份', '增量备份'].map(value => ({ value }))} /></Form.Item><Form.Item name="retention" label="保留期限（天）" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Form>
        : drawer === 'policy' ? <Form form={form} layout="vertical" initialValues={{ retention: policy.retention }}><Form.Item name="retention" label="备份保留期限（天）"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item><InfoAlert message="本地演示策略，不会修改服务器计划。" closable={false} /></Form>
          : selected && <Descriptions className="drawer-meta" column={1} items={[{ key: '1', label: '任务名称', children: selected.name }, { key: '2', label: '数据源', children: selected.source }, { key: '3', label: '备份类型', children: selected.kind }, { key: '4', label: '状态', children: <StatusTag value={selected.status} /> }, { key: '5', label: '最近执行', children: selected.lastRun }, { key: '6', label: '保留期限', children: `${selected.retention} 天` }]} />}
    </Drawer>
  </>;
}
