import { App, Button, Descriptions, Drawer, Form, Input, Select, Space, Tabs, Typography } from 'antd';
import { DownOutlined } from '@ant-design/icons';
import { useMemo, useState, type ReactNode } from 'react';
import { useOutboxMessages } from '../../data/stores/governance';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { OutboxMessage } from '../../data/mock/governance';

export function OutboxPage() {
  const { message } = App.useApp();
  const [outboxMessages, setOutboxMessages] = useOutboxMessages();
  const [status, setStatus] = useState<string>();
  const [applied, setApplied] = useState('');
  const [drawer, setDrawer] = useState<'detail' | 'create' | null>('detail');
  const [selectedId, setSelectedId] = useState<number>(1000013490);
  const [form] = Form.useForm();
  const selected = outboxMessages.find(item => item.id === selectedId);
  const filtered = useMemo(() => outboxMessages.filter(item => !applied || item.status === applied), [outboxMessages, applied]);
  const append = (values: { event: string; payload: string }) => {
    try {
      const parsed: unknown = JSON.parse(values.payload);
      if (!parsed || typeof parsed !== 'object') throw new Error('消息内容必须是 JSON 对象');
      const id = Math.max(...outboxMessages.map(item => item.id), 1000013493) + 1;
      const item: OutboxMessage = { id, event: values.event, status: '待处理', retries: 0, created: '2026-09-25 16:28:40', attempted: '-', error: '-', payload: parsed as Record<string, unknown> };
      setOutboxMessages(current => [item, ...current]); setDrawer(null); form.resetFields(); message.success('消息已追加到本地演示 Outbox');
    } catch (error) { message.error(error instanceof Error ? error.message : '消息内容格式错误'); }
  };
  return <>
    <PageIntro title="Outbox 消息" description="查看系统 Outbox 事件消息的处理状态与重试情况。" action={<Button type="primary" onClick={() => setDrawer('create')}>追加消息</Button>} />
    <SectionCard className="filter-card outbox-filter"><div className="filter-grid"><Field label="状态"><Select allowClear value={status} onChange={setStatus} placeholder="请选择状态" options={['成功', '待处理', '派发中', '失败', '死信'].map(value => ({ value }))} /></Field><FilterActions onSearch={() => setApplied(status || '')} onReset={() => { setStatus(undefined); setApplied(''); }} /></div></SectionCard>
    <SectionCard className="table-card outbox-table"><DataTable dataSource={filtered} tableLayout="fixed" scroll={{ x: undefined }} rowClassName={item => item.id === selectedId && drawer === 'detail' ? 'selected-table-row' : ''} columns={[
      { title: '消息 ID', dataIndex: 'id', width: 92 }, { title: '事件类型', dataIndex: 'event', width: 135 },
      { title: '状态', dataIndex: 'status', width: 73, render: (value: string) => <StatusTag value={value} /> },
      { title: '重试次数', dataIndex: 'retries', width: 69, align: 'center' }, { title: '创建时间', dataIndex: 'created', width: 135 },
      { title: '最近尝试时间', dataIndex: 'attempted', width: 136 }, { title: '失败摘要', dataIndex: 'error', width: 107 },
      { title: '操作', render: (_, item: OutboxMessage) => <Button type="link" className="table-link" onClick={() => { setSelectedId(item.id); setDrawer('detail'); }}>查看</Button> },
    ]} /></SectionCard>
    <Drawer title={drawer === 'create' ? '追加 Outbox 消息' : '消息详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={drawer === 'detail' ? 368 : 430} mask={false} rootClassName="reference-drawer outbox-drawer" footer={drawer === 'create' ? <Space><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" onClick={() => form.submit()}>追加消息</Button></Space> : null}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={append} initialValues={{ payload: '{\n  "eventType": "PACKAGE_CREATED",\n  "parcelId": 2509250001\n}' }}>
        <InfoAlert message="仅写入本地演示消息列表，不触发真实派发。" closable={false} />
        <Form.Item name="event" label="事件类型" rules={[{ required: true, message: '请输入事件类型' }]}><Input placeholder="例如 PACKAGE_CREATED" /></Form.Item>
        <Form.Item name="payload" label="消息载荷（JSON）" rules={[{ required: true }]}><Input.TextArea rows={12} placeholder="请输入 JSON 对象" /></Form.Item>
      </Form> : selected ? <>
        <Descriptions className="drawer-meta" column={1} size="small" colon={false} items={[
          { key: '1', label: '消息 ID', children: selected.id }, { key: '2', label: '事件类型', children: selected.event },
          { key: '3', label: '状态', children: <StatusTag value={selected.status} /> }, { key: '4', label: '重试次数', children: selected.retries },
          { key: '5', label: '创建时间', children: selected.created }, { key: '6', label: '最近尝试时间', children: selected.attempted },
          { key: '7', label: '失败摘要', children: selected.error }, { key: '8', label: '最后更新时间', children: selected.attempted },
        ]} />
        <Tabs style={{ marginTop: 20 }} items={[
          { key: 'payload', label: '消息内容', children: <><div className="outbox-json-heading"><b>消息载荷（JSON）</b></div><JsonPreview value={selected.payload} /></> },
          { key: 'error', label: '失败信息', children: selected.error === '-' ? <Typography.Text type="secondary">没有失败记录</Typography.Text> : <InfoAlert type="warning" message={selected.error} closable={false} /> },
        ]} />
      </> : null}
    </Drawer>
  </>;
}

function JsonPreview({ value }: { value: Record<string, unknown> }) {
  const source = JSON.stringify(value, null, 3);
  const parts: ReactNode[] = [];
  const pattern = /"(?:\\.|[^"\\])*"(?=\s*:)|"(?:\\.|[^"\\])*"|\b\d+(?:\.\d+)?\b/g;
  let previous = 0;
  for (const match of source.matchAll(pattern)) {
    const start = match.index;
    if (start > previous) parts.push(source.slice(previous, start));
    const token = match[0];
    const suffix = source.slice(start + token.length);
    const kind = /^\s*:/.test(suffix) ? 'json-key' : token.startsWith('"') ? 'json-string' : 'json-number';
    parts.push(<span className={kind} key={start}>{token}</span>);
    previous = start + token.length;
  }
  if (previous < source.length) parts.push(source.slice(previous));
  return <div className="outbox-json-view"><DownOutlined className="json-expander" /><pre className="json-block">{parts}</pre></div>;
}
