import { App, Button, Descriptions, Drawer, Form, Input, Select, Space, Tabs, Timeline } from 'antd';
import { useMemo, useState } from 'react';
import { useRules } from '../../data/stores/operations';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { Rule } from '../../data/mock/operations';

export function RulesPage() {
  const { message, modal } = App.useApp();
  const [rules, setRules] = useRules();
  const [status, setStatus] = useState<string>();
  const [type, setType] = useState<string>();
  const [version, setVersion] = useState('');
  const [applied, setApplied] = useState({ status: '', type: '', version: '' });
  const [selectedId, setSelectedId] = useState(1);
  const [drawer, setDrawer] = useState<'detail' | 'create' | null>('detail');
  const [form] = Form.useForm();
  const selected = rules.find(item => item.id === selectedId);
  const filtered = useMemo(() => rules.filter(item => (!applied.status || item.status === applied.status) && (!applied.type || item.name.includes(applied.type)) && (!applied.version || item.version.includes(applied.version))), [rules, applied]);
  const create = (values: { name: string; scope: string; description: string }) => {
    const id = Math.max(...rules.map(item => item.id), 10) + 1;
    setRules(current => [{ ...values, id, version: 'v0.1.0', status: '草稿', modified: '2026-09-25 16:28:40', editor: '张三' }, ...current]);
    form.resetFields(); setDrawer(null); message.success('新规则草稿已加入本地演示列表');
  };
  return <>
    <PageIntro title="规则管理" planned description="管理包裹分拣规则，配置匹配条件与处理动作，提升分拣效率。" action={<Button type="primary" onClick={() => setDrawer('create')}>新建规则</Button>} />
    <SectionCard className="filter-card rules-filter"><div className="filter-grid">
      <Field label="状态"><Select allowClear value={status} onChange={setStatus} placeholder="请选择状态" options={['已发布', '草稿'].map(value => ({ value }))} /></Field>
      <Field label="规则类型"><Select allowClear value={type} onChange={setType} placeholder="请选择规则类型" options={['大件', '易碎', '目的地', '异常', '生鲜'].map(value => ({ value }))} /></Field>
      <Field label="版本"><Input value={version} onChange={event => setVersion(event.target.value)} onPressEnter={() => setApplied({ status: status || '', type: type || '', version })} placeholder="请输入版本号" /></Field>
      <FilterActions onSearch={() => setApplied({ status: status || '', type: type || '', version })} onReset={() => { setStatus(undefined); setType(undefined); setVersion(''); setApplied({ status: '', type: '', version: '' }); }} />
    </div></SectionCard>
    <SectionCard className="table-card rules-table"><DataTable tableLayout="fixed" scroll={{ x: undefined }} dataSource={filtered} columns={[
      { title: '规则名称', dataIndex: 'name', width: 142 }, { title: '版本', dataIndex: 'version', width: 67 },
      { title: '状态', dataIndex: 'status', width: 90, render: (value: string) => <StatusTag value={value} /> }, { title: '适用范围', dataIndex: 'scope', width: 124 },
      { title: '最近修改', dataIndex: 'modified', width: 158 }, { title: '修改人', dataIndex: 'editor', width: 83 },
      { title: '操作', render: (_, item: Rule) => <Button type="link" className="table-link" onClick={() => { setSelectedId(item.id); setDrawer('detail'); }}>查看</Button> },
    ]} /></SectionCard>
    <Drawer title={drawer === 'create' ? '新建规则' : '规则详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={392} mask={false} rootClassName="reference-drawer rules-drawer" footer={drawer === 'create' ? <Space><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" onClick={() => form.submit()}>保存草稿</Button></Space> : <Space><Button onClick={() => modal.info({ title: '规则仿真', content: `规则「${selected?.name || ''}」已执行本地演示仿真，结果：匹配 12 件。` })}>仿真</Button><Button type="primary" onClick={() => message.success('规则草稿已保存到本地演示数据')}>保存草稿</Button></Space>}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={create}><Form.Item name="name" label="规则名称" rules={[{ required: true }]}><Input placeholder="请输入规则名称" /></Form.Item><Form.Item name="scope" label="适用范围" rules={[{ required: true }]}><Input placeholder="例如 B01-06" /></Form.Item><Form.Item name="description" label="规则说明" rules={[{ required: true }]}><Input.TextArea rows={5} /></Form.Item></Form>
        : selected && <><h3 style={{ marginTop: 0 }}>{selected.name} <StatusTag value="草稿" /></h3><p className="text-muted" style={{ marginTop: -4 }}>{selected.description}</p>
          <div className="rule-meta-panel"><Descriptions className="drawer-meta" column={1} size="small" colon={false} items={[{ key: '1', label: '规则类型', children: '包裹属性规则' }, { key: '2', label: '适用范围', children: selected.scope }, { key: '3', label: '当前版本', children: selected.version }, { key: '4', label: '最后修改', children: selected.modified }, { key: '5', label: '修改人', children: selected.editor }, { key: '6', label: '备注', children: selected.note || selected.description }]} /></div>
          <Tabs style={{ marginTop: 18 }} items={[
            { key: 'conditions', label: '条件', children: <><h4 className="rule-condition-heading">触发条件</h4><div className="stack">{[['包裹重量', '大于', '20 kg'], ['包裹体积（长×宽×高）', '大于', '100000 cm³'], ['包裹类型', '包含', '大件']].map(([field, operator, threshold], index) => <div className="rule-condition" key={field}><b>条件 {index + 1}</b><Descriptions size="small" bordered column={1} items={[{ key: '1', label: '匹配字段', children: field }, { key: '2', label: '运算符', children: operator }, { key: '3', label: '阈值', children: threshold }]} /></div>)}</div></> },
            { key: 'actions', label: '命中动作', children: <Descriptions bordered column={1} items={[{ key: '1', label: '处理动作', children: '分流至专用分拣口' }, { key: '2', label: '目标范围', children: selected.scope }]} /> },
            { key: 'history', label: '变更记录', children: <Timeline items={[{ children: `${selected.modified} ${selected.editor} 更新规则` }, { children: '2026-09-19 10:20:00 创建草稿' }]} /> },
          ]} />
        </>}
    </Drawer>
  </>;
}
