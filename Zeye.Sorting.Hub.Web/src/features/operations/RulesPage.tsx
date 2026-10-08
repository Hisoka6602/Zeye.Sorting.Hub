import { Alert, App, Button, Descriptions, Drawer, Empty, Form, Input, Popconfirm, Select, Space, Tabs, Tag, Timeline, Tooltip } from 'antd';
import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { useServerRules } from '../../data/api/useServerRules';
import { ApiFeedback } from '../../components/ApiFeedback';
import { exceptionTypeOptions, type Rule, type RuleCondition } from '../../data/mock/operations';
import { createExceptionCondition, editableExceptionCondition, formatConditionValue, saveExceptionCondition } from '../../data/exceptionConditions';
import { ExceptionConditionEditor } from './ExceptionConditionEditor';
import { ExceptionVerification } from './ExceptionVerification';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { localDateTimeFormat, localTime } from '../../data/api/operationalTypes';
import './rules.css';

type RuleCategory = 'parcel' | 'exception';
const categoryNames: Record<RuleCategory, string> = { parcel: '包裹分类规则', exception: '异常分类规则' };
const parcelTypes = ['普通包裹', '大型包裹', '聚合包裹', '超薄包裹', '异形件', '流体包裹', '易碎品'];
const parcelActions = ['标记包裹类型'];
const exceptionActions = ['标记分拣异常', '记录异常原因'];
const emptyFilters = { status: '', type: '', version: '' };

interface RuleFormValues {
  name: string;
  scope: string;
  description: string;
  targetType?: string;
  exceptionType?: number;
  parcelType?: number;
  matchMode: 'all' | 'any';
  conditions: RuleCondition[];
  actions: string[];
}

function RuleEditor({ category, values, onSave }: { category: RuleCategory; values: RuleFormValues; onSave: (values: RuleFormValues) => void }) {
  return <Form id="rule-editor-form" layout="vertical" initialValues={values} onFinish={onSave}>
    <Form.Item name="name" label="规则名称" rules={[{ required: true, whitespace: true, message: '请输入规则名称' }]}><Input placeholder="请输入规则名称" /></Form.Item>
    {category === 'exception'
      ? <Form.Item name="exceptionType" label="异常类型" rules={[{ required: true, message: '请选择分类后的异常类型' }]}><Select placeholder="请选择分类后的异常类型" options={exceptionTypeOptions.filter(item => item.value !== 0)} /></Form.Item>
      : <Form.Item name="parcelType" label="包裹分类" rules={[{ required: true, message: '请选择分类结果' }]}><Select placeholder="请选择分类结果" options={parcelTypes.map((label, value) => ({ label, value }))} /></Form.Item>}
    <Form.Item name="scope" label="适用范围" rules={[{ required: true, whitespace: true, message: '请输入适用范围' }]}><Input placeholder={category === 'exception' ? '例如 全部产线、产线 1' : '例如 B01-06'} /></Form.Item>
    <Form.Item name="description" label="规则说明" rules={[{ required: true, whitespace: true, message: '请输入规则说明' }]}><Input.TextArea rows={3} /></Form.Item>
    <Form.Item name="matchMode" label="条件关系"><Select options={[{ value: 'all', label: '满足全部条件（且）' }, { value: 'any', label: '满足任意条件（或）' }]} /></Form.Item>
    <Form.List name="conditions" rules={[{ validator: async (_, conditions: RuleCondition[]) => { if (!conditions?.length) throw new Error('请至少配置一个匹配条件'); } }]}>
      {(fields, { add, remove }, { errors }) => <div className="rule-editor-conditions">
        {fields.map((field, index) => <ExceptionConditionEditor key={field.key} name={field.name} index={index} canRemove={fields.length > 1} onRemove={() => remove(field.name)} />)}
        <Button block onClick={() => add(createExceptionCondition())}>添加条件</Button>
        <Form.ErrorList errors={errors} />
      </div>}
    </Form.List>
    <Form.Item name="actions" label="命中动作" rules={[{ required: true, type: 'array', min: 1, message: '请选择至少一个命中动作' }]}><Select mode="multiple" placeholder="请选择命中动作" options={(category === 'exception' ? exceptionActions : parcelActions).map(value => ({ value, label: value }))} /></Form.Item>
  </Form>;
}

export function RulesPage() {
  const { message } = App.useApp();
  const parcelResource = useServerRules('parcel');
  const exceptionResource = useServerRules('exception');
  const parcelRules = parcelResource.rules;
  const exceptionRules = exceptionResource.rules;
  const setParcelRules = parcelResource.commit;
  const setExceptionRules = exceptionResource.commit;
  const [category, setCategory] = useState<RuleCategory>('parcel');
  const [status, setStatus] = useState<string>();
  const [type, setType] = useState<string>();
  const [version, setVersion] = useState('');
  const [applied, setApplied] = useState(emptyFilters);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedId, setSelectedId] = useState<number | undefined>(1);
  const [drawer, setDrawer] = useState<'detail' | 'create' | null>(null);
  const [detailTab, setDetailTab] = useState('conditions');
  const [editingId, setEditingId] = useState<number>();
  const [editorKey, setEditorKey] = useState(0);
  const [editorValues, setEditorValues] = useState<RuleFormValues>();
  const [verificationOpen, setVerificationOpen] = useState(false);
  const [previewRule, setPreviewRule] = useState<Rule>();
  const rules = category === 'parcel' ? parcelRules : exceptionRules;
  const selected = rules.find(item => item.id === selectedId);
  const filtered = useMemo(() => rules.filter(item => (!applied.status || item.status === applied.status) && (!applied.type || item.targetType === applied.type) && (!applied.version || item.version.includes(applied.version.trim()))), [rules, applied]);
  const typeOptions = category === 'exception' ? exceptionTypeOptions.map(item => ({ value: item.label, label: item.label })) : [...new Set(parcelRules.map(item => item.targetType).filter((value): value is string => Boolean(value)))].map(value => ({ value, label: value }));

  const resetFilters = () => { setStatus(undefined); setType(undefined); setVersion(''); setApplied(emptyFilters); setPage(1); };
  const changeCategory = (value: string) => {
    const next = value as RuleCategory;
    setCategory(next); resetFilters(); setDetailTab('conditions');
    const first = (next === 'parcel' ? parcelRules : exceptionRules)[0];
    setSelectedId(first?.id);
    if (drawer === 'create' || !first) setDrawer(null);
  };
  const search = () => {
    const next = { status: status || '', type: type || '', version: version.trim() };
    setApplied(next); setPage(1);
    if (selected && ((next.status && selected.status !== next.status) || (next.type && selected.targetType !== next.type) || (next.version && !selected.version.includes(next.version)))) setDrawer(null);
  };
  const openEditor = (rule?: Rule) => {
    if (rule?.systemRule === 'unknown-fallback') return;
    setEditingId(rule?.status === '草稿' ? rule.id : undefined);
    setEditorKey(current => current + 1);
    setEditorValues({
      name: rule ? `${rule.name}${rule.status === '已发布' ? '（副本）' : ''}` : '',
      scope: rule?.scope ?? '全部产线', description: rule?.description ?? '',
      targetType: rule?.targetType, exceptionType: rule?.exceptionType, parcelType: rule?.parcelType,
      matchMode: rule?.matchMode ?? 'all',
      conditions: rule?.conditions?.length ? rule.conditions.map(editableExceptionCondition) : [createExceptionCondition()],
      actions: rule?.actions?.length ? rule.actions : category === 'exception' ? ['标记分拣异常', '记录异常原因'] : ['标记包裹类型'],
    });
    setDrawer('create');
  };
  const save = async (values: RuleFormValues) => {
    if (category === 'exception' && values.exceptionType === 0) return;
    const id = editingId ?? Math.max(0, ...rules.map(item => item.id)) + 1;
    const previous = rules.find(item => item.id === editingId);
    const rule: Rule = {
      id, name: values.name.trim(), scope: values.scope.trim(), description: values.description.trim(),
      version: previous?.version ?? 'v0.1.0', status: '草稿', modified: dayjs().format(localDateTimeFormat), editor: '张三',
      targetType: category === 'exception' ? exceptionTypeOptions.find(item => item.value === values.exceptionType)?.label : parcelTypes[values.parcelType!],
      exceptionType: category === 'exception' ? values.exceptionType : undefined,
      parcelType: category === 'parcel' ? values.parcelType : undefined,
      matchMode: values.matchMode,
      conditions: values.conditions.map(saveExceptionCondition),
      actions: values.actions,
    };
    const update = category === 'exception' ? setExceptionRules : setParcelRules;
    try { await update(current => editingId === undefined ? [...current, rule] : current.map(item => item.id === editingId ? { ...item, ...rule } : item)); }
    catch (error) { message.error(error instanceof Error ? error.message : '保存失败'); return; }
    resetFilters(); setSelectedId(id); setDetailTab('conditions'); setDrawer('detail');
    message.success(`${categoryNames[category]}草稿已保存到服务器`);
  };
  const removeRule = async (rule: Rule) => {
    if (rule.systemRule || rule.status !== '草稿') return;
    const update = category === 'exception' ? setExceptionRules : setParcelRules;
    try { await update(current => current.filter(item => item.id !== rule.id)); }
    catch (error) { message.error(error instanceof Error ? error.message : '删除失败'); return; }
    if (selectedId === rule.id) setDrawer(null);
    setPage(1);
    message.success('草稿已删除');
  };
  const publish = async (rule: Rule) => {
    const update = category === 'exception' ? setExceptionRules : setParcelRules;
    try { await update(current => current.map(item => item.id === rule.id ? { ...item, status: '已发布' } : item)); message.success('规则已发布，后续处理记录将按规则分类'); }
    catch (error) { message.error(error instanceof Error ? error.message : '发布失败'); }
  };
  const deactivate = async (rule: Rule) => {
    if (rule.systemRule) return;
    const update = category === 'exception' ? setExceptionRules : setParcelRules;
    try { await update(current => current.map(item => item.id === rule.id ? { ...item, status: '草稿' } : item)); message.success('规则已停用，可以继续编辑草稿'); }
    catch (error) { message.error(error instanceof Error ? error.message : '停用失败'); }
  };

  const list = <>
    {category === 'exception' && <Alert className="rules-protocol-note" type="info" showIcon message="默认包含 Fusion 分拣机已对接的异常编码。未匹配的异常归入“未知异常”，该系统兜底规则不可修改或删除。" />}
    <SectionCard className="filter-card rules-filter"><div className="filter-grid">
      <Field label="状态"><Select aria-label="状态" allowClear value={status} onChange={setStatus} placeholder="请选择状态" options={['已发布', '草稿'].map(value => ({ value, label: value }))} /></Field>
      <Field label={category === 'exception' ? '异常类型' : '包裹分类'}><Select aria-label={category === 'exception' ? '异常类型' : '包裹分类'} allowClear value={type} onChange={setType} placeholder={category === 'exception' ? '请选择异常类型' : '请选择包裹分类'} options={typeOptions} /></Field>
      <Field label="版本"><Input aria-label="版本" value={version} onChange={event => setVersion(event.target.value)} onPressEnter={search} placeholder="请输入版本号" /></Field>
      <FilterActions onSearch={search} onReset={resetFilters} />
    </div></SectionCard>
    <SectionCard className="table-card rules-table"><DataTable<Rule> tableLayout="fixed" scroll={{ x: category === 'exception' ? 1260 : 850 }} dataSource={filtered} locale={{ emptyText: `暂无符合条件的${categoryNames[category]}` }} pagination={{ current: page, pageSize, onChange: (next, size) => { setPage(next); setPageSize(size); } }} columns={[
      { title: '规则名称', dataIndex: 'name', width: category === 'exception' ? 198 : 178, render: (name: string, rule: Rule) => <span>{name}{rule.systemRule === 'unknown-fallback' && <Tag className="rule-system-tag">系统兜底</Tag>}</span> },
      ...(category === 'exception' ? [{ title: '匹配条件', width: 220, render: (_: unknown, rule: Rule) => rule.systemRule === 'unknown-fallback' ? '其他规则均未匹配' : <div className="rule-condition-summary">{rule.conditions?.map((condition, index) => <span key={index}>{condition.field} {condition.operator} {formatConditionValue(condition)}</span>)}</div> }, { title: '异常类型', dataIndex: 'targetType', width: 158 }] : []),
      { title: '版本', dataIndex: 'version', width: 80 },
      { title: '状态', dataIndex: 'status', width: 86, render: (value: string) => <StatusTag value={value} /> },
      { title: '适用范围', dataIndex: 'scope', width: 124 }, { title: '最近修改', dataIndex: 'modified', width: 210, render: localTime }, { title: '修改人', dataIndex: 'editor', width: 75 },
      { title: '操作', width: category === 'exception' ? 124 : 70, render: (_, item) => <Space size={4}><Button type="link" className="table-link" onClick={() => { setSelectedId(item.id); setDetailTab('conditions'); setDrawer('detail'); }}>查看</Button>{category === 'exception' && (item.systemRule === 'unknown-fallback' ? <Tooltip title="未知异常是系统兜底规则，不能删除"><Button type="link" disabled>删除</Button></Tooltip> : item.status === '草稿' && <Popconfirm title="删除此草稿？" description="已发布规则和系统规则不会受到影响。" okText="删除" cancelText="取消" onConfirm={() => removeRule(item)}><Button type="link" danger className="table-link">删除</Button></Popconfirm>)}</Space> },
    ]} /></SectionCard>
  </>;

  return <div className="rules-page">
    <PageIntro title="规则管理" description="已发布规则按列表顺序匹配真实包裹数据，分别保存包裹类型与异常分类。" action={<Space wrap>{category === 'exception' && <Button onClick={() => { setPreviewRule(undefined); setVerificationOpen(true); }}>验证异常分类</Button>}<Button onClick={() => { parcelResource.refresh(); exceptionResource.refresh(); }}>刷新</Button><Button type="primary" disabled={!(category === 'exception' ? exceptionResource.loaded : parcelResource.loaded)} onClick={() => openEditor()}>新建{category === 'exception' ? '异常' : '包裹'}规则</Button></Space>} />
    <ApiFeedback error={category === 'exception' ? exceptionResource.error : parcelResource.error} retry={category === 'exception' ? exceptionResource.refresh : parcelResource.refresh} />
    <Tabs className="rules-category-tabs" activeKey={category} onChange={changeCategory} destroyOnHidden items={(['parcel', 'exception'] as const).map(key => ({
      key, label: <span className="rules-category-label">{categoryNames[key]}<span className="rules-category-count">{key === 'parcel' ? parcelRules.length : exceptionRules.length}</span></span>,
      children: category === key ? list : null,
    }))} />
    <ExceptionVerification key={`${verificationOpen}-${previewRule?.id ?? ''}`} rules={exceptionRules} previewRule={previewRule} open={verificationOpen} onClose={() => setVerificationOpen(false)} />
    <Drawer title={drawer === 'create' ? `${editingId === undefined ? '新建' : '编辑'}${categoryNames[category]}` : '规则详情'} open={drawer !== null} onClose={() => setDrawer(null)} width={392} mask={false} rootClassName="reference-drawer rules-drawer" footer={drawer === 'create'
      ? <Space key="rule-editor-footer"><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" loading={parcelResource.busy || exceptionResource.busy} htmlType="submit" form="rule-editor-form">保存草稿</Button></Space>
      : selected && (selected.systemRule === 'unknown-fallback' ? <Space key="rule-fallback-footer"><Button disabled>删除</Button><Button disabled>系统兜底规则</Button></Space> : <Space wrap key="rule-detail-footer">{!selected.systemRule && selected.status === '已发布' && <Popconfirm title="停用此分类规则？" description="停用后不会参与后续处理记录的分类，可继续编辑草稿。" okText="停用" cancelText="取消" onConfirm={() => deactivate(selected)}><Button loading={parcelResource.busy || exceptionResource.busy}>停用规则</Button></Popconfirm>}{!selected.systemRule && selected.status === '草稿' && <Popconfirm title="发布此分类规则？" description="发布后会用于后续包裹处理记录的实际分类。" okText="发布" cancelText="取消" onConfirm={() => publish(selected)}><Button type="primary" loading={parcelResource.busy || exceptionResource.busy}>发布规则</Button></Popconfirm>}{category === 'exception' && <Button onClick={() => { setPreviewRule(selected.status === '草稿' ? selected : undefined); setVerificationOpen(true); }}>{selected.status === '草稿' ? '验证草稿' : '验证分类'}</Button>}<Button type="primary" onClick={() => openEditor(selected)}>{selected.status === '已发布' ? '复制为草稿' : '编辑草稿'}</Button></Space>)}>
      {drawer === 'create' && editorValues ? <RuleEditor key={editorKey} category={category} values={editorValues} onSave={save} /> : selected && <>
        <h3 style={{ marginTop: 0 }}>{selected.name} <StatusTag value={selected.status} /></h3><p className="text-muted" style={{ marginTop: -4 }}>{selected.description}</p>
        <div className="rule-meta-panel"><Descriptions className="drawer-meta" column={1} size="small" colon={false} items={[
          { key: 'category', label: '规则分类', children: categoryNames[category] },
          { key: 'target', label: category === 'exception' ? '异常类型' : '包裹分类', children: selected.targetType || '待配置' },
          { key: 'scope', label: '适用范围', children: selected.scope }, { key: 'version', label: '当前版本', children: selected.version },
          { key: 'modified', label: '最后修改', children: localTime(selected.modified) }, { key: 'editor', label: '修改人', children: selected.editor },
          { key: 'note', label: '备注', children: selected.note || selected.description },
        ]} /></div>
        <Tabs style={{ marginTop: 18 }} activeKey={detailTab} onChange={setDetailTab} items={[
          { key: 'conditions', label: '条件', children: selected.systemRule === 'unknown-fallback' ? <Alert type="info" showIcon message="系统兜底条件" description="仅当所有已发布异常分类规则均未命中时使用；不参与普通条件排序，始终最后执行。此规则不可修改或删除。" /> : <><h4 className="rule-condition-heading">触发条件</h4><p className="rule-match-mode">{selected.matchMode === 'any' ? '满足任意条件（或）' : '满足全部条件（且）'}</p><div className="stack">
            {selected.conditions?.length ? selected.conditions.map((condition, index) => <div className="rule-condition" key={index}><b>条件 {index + 1}</b><Descriptions size="small" bordered column={1} items={[
              { key: 'field', label: '匹配字段', children: condition.field }, { key: 'operator', label: '运算符', children: condition.operator },
              { key: 'value', label: category === 'exception' ? '匹配值' : '阈值', children: formatConditionValue(condition) },
            ]} /></div>) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚未配置匹配条件" />}
          </div></> },
          { key: 'actions', label: '命中动作', children: <Descriptions bordered column={1} items={[
            { key: 'result', label: '分类结果', children: selected.targetType || '待配置' },
            { key: 'actions', label: '处理动作', children: selected.actions?.length ? <ul className="rule-action-list">{selected.actions.map(action => <li key={action}>{action}</li>)}</ul> : '待配置' },
            { key: 'scope', label: '适用范围', children: selected.scope },
          ]} /> },
          { key: 'history', label: '变更记录', children: <Timeline items={[{ children: `${localTime(selected.modified)} ${selected.editor} 更新规则` }]} /> },
        ]} />
      </>}
    </Drawer>
  </div>;
}
