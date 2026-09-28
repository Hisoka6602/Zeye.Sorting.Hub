import { App, Button, Descriptions, Drawer, Form, Input, Select, Space } from 'antd';
import { useMemo, useState } from 'react';
import { useArchiveTasks } from '../../data/stores/governance';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { NumberUnit } from '../../components/NumberUnit';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { ArchiveTask } from '../../data/mock/governance';

export function ArchiveTasksPage() {
  const { message } = App.useApp();
  const [archiveTasks, setArchiveTasks] = useArchiveTasks();
  const [kind, setKind] = useState('WebRequestAuditLogHistory');
  const [status, setStatus] = useState<string>();
  const [applied, setApplied] = useState({ kind: '', status: '' });
  const [drawer, setDrawer] = useState<'create' | 'detail' | null>('create');
  const [selected, setSelected] = useState<ArchiveTask | null>(null);
  const [form] = Form.useForm();
  const filtered = useMemo(() => archiveTasks.filter(item => (!applied.kind || item.type === applied.kind) && (!applied.status || item.status === applied.status)), [archiveTasks, applied]);
  const create = (values: { retention: number; initiator: string; note?: string }) => {
    const id = Math.max(...archiveTasks.map(item => item.id), 12031) + 1;
    setArchiveTasks(current => [{ id, type: 'WebRequestAuditLogHistory', status: '待生成', retention: values.retention, candidates: null, initiator: values.initiator, created: '2026-09-25 16:28:40', note: values.note }, ...current]);
    setDrawer(null); form.resetFields(); message.success('已创建本地演示 dry-run 任务');
  };
  const retry = () => {
    if (!selected) return;
    setArchiveTasks(current => current.map(item => item.id === selected.id ? { ...item, status: '待生成', candidates: null } : item));
    setDrawer(null); message.success('任务已进入本地演示待生成状态');
  };
  return <>
    <PageIntro title="归档任务" />
    <InfoAlert message="仅生成归档 dry-run 计划，不执行迁移或删除。" />
    <SectionCard className="filter-card archive-filter"><div className="filter-grid">
      <Field label="任务类型"><Select value={kind} onChange={setKind} options={[{ value: 'WebRequestAuditLogHistory' }]} /></Field>
      <Field label="状态"><Select allowClear value={status} onChange={setStatus} placeholder="请选择状态" options={['待生成', '已完成', '失败'].map(value => ({ value }))} /></Field>
      <FilterActions onSearch={() => setApplied({ kind, status: status || '' })} onReset={() => { setKind('WebRequestAuditLogHistory'); setStatus(undefined); setApplied({ kind: '', status: '' }); }} extra={<Button type="primary" onClick={() => setDrawer('create')}>新建 dry-run 任务</Button>} />
    </div></SectionCard>
    <SectionCard className="table-card archive-table"><DataTable dataSource={filtered} tableLayout="fixed" scroll={{ x: '100%' }} columns={[
      { title: '任务 ID', dataIndex: 'id', width: 78 }, { title: '类型', dataIndex: 'type', width: 212 },
      { title: '状态', dataIndex: 'status', width: 82, render: (value: string) => <StatusTag value={value} /> },
      { title: '保留天数', dataIndex: 'retention', width: 88 }, { title: '计划候选数', dataIndex: 'candidates', width: 109, render: (value: number | null) => value === null ? '-' : value.toLocaleString() },
      { title: '发起人', dataIndex: 'initiator', width: 85 }, { title: '创建时间', dataIndex: 'created', width: 167 },
      { title: '操作', render: (_, item: ArchiveTask) => <Button type="link" className="table-link" onClick={() => { setSelected(item); setDrawer('detail'); }}>查看</Button> },
    ]} /></SectionCard>
    <Drawer title={drawer === 'create' ? '新建 dry-run 任务' : '归档任务详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={drawer === 'create' ? 362 : 440} mask={false} rootClassName="reference-drawer archive-drawer"
      footer={drawer === 'create' ? <Space><Button autoInsertSpace={false} onClick={() => setDrawer(null)}>取消</Button><Button type="primary" onClick={() => form.submit()}>创建 dry-run 任务</Button></Space> : <Space><Button onClick={() => setDrawer(null)}>关闭</Button>{selected && selected.status !== '待生成' && <Button type="primary" onClick={retry}>重试任务</Button>}</Space>}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={create} initialValues={{ type: 'WebRequestAuditLogHistory', retention: 180, initiator: '张三' }}>
        <Form.Item name="type" label="任务类型" rules={[{ required: true }]}><Select disabled options={[{ value: 'WebRequestAuditLogHistory' }]} /></Form.Item>
        <Form.Item name="retention" label="保留天数" rules={[{ required: true }]} extra="取值范围：1 - 3650"><NumberUnit min={1} max={3650} unit="天" style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="initiator" label="发起人" rules={[{ required: true, whitespace: true, message: '请输入发起人' }]}><Input placeholder="请输入发起人" /></Form.Item>
        <Form.Item name="note" label="备注"><Input.TextArea rows={4} maxLength={500} showCount placeholder="请输入备注（可选）" /></Form.Item>
      </Form> : selected ? <div className="stack">
        <InfoAlert message="仅生成 dry-run 计划，不执行真实迁移或删除。" closable={false} />
        <Descriptions className="drawer-meta" column={1} size="small" items={[
          { key: '1', label: '任务 ID', children: selected.id }, { key: '2', label: '类型', children: selected.type }, { key: '3', label: '状态', children: <StatusTag value={selected.status} /> },
          { key: '4', label: '保留天数', children: `${selected.retention} 天` }, { key: '5', label: '计划候选数', children: selected.candidates?.toLocaleString() || '-' },
          { key: '6', label: '发起人', children: selected.initiator }, { key: '7', label: '创建时间', children: selected.created }, { key: '8', label: '备注', children: selected.note || '-' },
        ]} />
        <SectionCard title="计划摘要"><p>按照保留天数筛选 WebRequestAuditLogHistory 候选记录。</p><p className="text-muted">当前任务仅供预演；实际迁移和删除数量为 0。</p></SectionCard>
      </div> : null}
    </Drawer>
  </>;
}
