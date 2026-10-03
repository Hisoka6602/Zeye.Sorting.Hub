import { App, Button, Descriptions, Drawer, Form, Input, Select, Space } from 'antd';
import { useState } from 'react';
import { DataTable } from '../../components/DataTable';
import { ApiFeedback } from '../../components/ApiFeedback';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { NumberUnit } from '../../components/NumberUnit';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { requestApi } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import { localTime, archiveStatusLabels, type ArchiveTask, type PagedResult } from '../../data/api/operationalTypes';
export function ArchiveTasksPage() {
  const { message } = App.useApp();
  const [kind, setKind] = useState('WebRequestAuditLogHistory');
  const [status, setStatus] = useState<string>();
  const [applied, setApplied] = useState('');
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(10);
  const [busy, setBusy] = useState(false);
  const resource = useApiResource<PagedResult<ArchiveTask>>('/api/data-governance/archive-tasks?pageNumber=' + page + '&pageSize=' + size + applied);
  const [drawer, setDrawer] = useState<'create' | 'detail' | null>(null);
  const [selected, setSelected] = useState<ArchiveTask | null>(null);
  const [form] = Form.useForm();
  const create = async (values: { retention: number; initiator: string; note?: string }) => {
    setBusy(true);
    try {
      const task = await requestApi<ArchiveTask>('/api/data-governance/archive-tasks', undefined, { method: 'POST', body: JSON.stringify({ taskType: 'WebRequestAuditLogHistory', retentionDays: values.retention, requestedBy: values.initiator, remark: values.note }) });
      setSelected(task); setDrawer('detail'); form.resetFields(); setPage(1); resource.refresh(); message.success('归档 dry-run 任务已保存到服务器');
    } catch (error) { message.error(error instanceof Error ? error.message : '创建失败'); } finally { setBusy(false); }
  };
  const retry = async () => {
    if (!selected) return;
    setBusy(true);
    try { const task = await requestApi<ArchiveTask>('/api/data-governance/archive-tasks/' + selected.id + '/retry', undefined, { method: 'POST' }); setSelected(task); resource.refresh(); message.success('任务已重新提交到服务器'); }
    catch (error) { message.error(error instanceof Error ? error.message : '重试失败'); } finally { setBusy(false); }
  };
  return <>
    <PageIntro title="归档任务" />
    <InfoAlert message="仅生成归档 dry-run 计划，不执行迁移或删除。" />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    <SectionCard className="filter-card archive-filter"><div className="filter-grid">
      <Field label="任务类型"><Select value={kind} onChange={setKind} options={[{ value: 'WebRequestAuditLogHistory' }]} /></Field>
      <Field label="状态"><Select allowClear value={status} onChange={setStatus} placeholder="请选择状态" options={Object.entries(archiveStatusLabels).map(([value, label]) => ({ value, label }))} /></Field>
      <FilterActions onSearch={() => { setPage(1); setApplied('&taskType=' + kind + (status ? '&status=' + status : '')); resource.refresh(); }} onReset={() => { setKind('WebRequestAuditLogHistory'); setStatus(undefined); setPage(1); setApplied(''); resource.refresh(); }} extra={[<Button key="refresh" onClick={resource.refresh}>刷新</Button>, <Button key="create" type="primary" onClick={() => setDrawer('create')}>新建 dry-run 任务</Button>]} />
    </div></SectionCard>
    <SectionCard className="table-card archive-table"><DataTable<ArchiveTask> loading={resource.loading} dataSource={resource.data?.items ?? []} pagination={{ current: page, pageSize: size, total: Number(resource.data?.totalCount ?? 0), onChange: (next, nextSize) => { setPage(next); setSize(nextSize); } }} columns={[
      { title: '任务 ID', dataIndex: 'id', width: 180 }, { title: '类型', dataIndex: 'taskType', width: 212 },
      { title: '状态', dataIndex: 'status', render: (value: string) => <StatusTag value={archiveStatusLabels[value] ?? value} /> },
      { title: '保留天数', dataIndex: 'retentionDays' }, { title: '计划候选数', dataIndex: 'plannedItemCount' },
      { title: '发起人', dataIndex: 'requestedBy' }, { title: '创建时间', dataIndex: 'createdAt', width: 167, render: localTime },
      { title: '操作', render: (_, item) => <Button type="link" className="table-link" onClick={() => { setSelected(item); setDrawer('detail'); }}>查看</Button> },
    ]} /></SectionCard>
    <Drawer title={drawer === 'create' ? '新建 dry-run 任务' : '归档任务详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={440} footer={drawer === 'create'
      ? <Space><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" loading={busy} onClick={() => form.submit()}>创建 dry-run 任务</Button></Space>
      : <Space><Button onClick={() => setDrawer(null)}>关闭</Button>{selected?.status === 'Failed' && <Button type="primary" loading={busy} onClick={retry}>重试任务</Button>}</Space>}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={create} initialValues={{ retention: 180 }}>
        <Form.Item label="任务类型"><Input disabled value="WebRequestAuditLogHistory" /></Form.Item>
        <Form.Item name="retention" label="保留天数" rules={[{ required: true }]} extra="取值范围：1 - 3650"><NumberUnit min={1} max={3650} unit="天" style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="initiator" label="发起人" rules={[{ required: true, whitespace: true, message: '请输入发起人' }]}><Input maxLength={64} placeholder="请输入发起人" /></Form.Item>
        <Form.Item name="note" label="备注"><Input.TextArea rows={4} maxLength={512} showCount /></Form.Item>
      </Form> : selected && <div className="stack">
        <InfoAlert message={selected.isDryRun ? '仅生成 dry-run 计划。' : '以服务器实际执行结果为准。'} closable={false} />
        <Descriptions column={1} items={[
          { key: 'id', label: '任务 ID', children: selected.id }, { key: 'type', label: '类型', children: selected.taskType },
          { key: 'status', label: '状态', children: archiveStatusLabels[selected.status] ?? selected.status },
          { key: 'days', label: '保留天数', children: selected.retentionDays }, { key: 'planned', label: '计划候选数', children: selected.plannedItemCount },
          { key: 'processed', label: '已处理数量', children: selected.processedItemCount }, { key: 'by', label: '发起人', children: selected.requestedBy },
          { key: 'created', label: '创建时间', children: localTime(selected.createdAt) }, { key: 'updated', label: '更新时间', children: localTime(selected.updatedAt) },
          { key: 'remark', label: '备注', children: selected.remark || '-' }, { key: 'summary', label: '计划摘要', children: selected.planSummary || '尚未生成计划' },
          { key: 'error', label: '失败信息', children: selected.failureMessage || '-' },
        ]} />
      </div>}
    </Drawer>
  </>;
}
