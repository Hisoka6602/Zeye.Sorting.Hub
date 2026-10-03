import { App, Button, Empty, Form, InputNumber, Skeleton, Switch } from 'antd';
import { ApiOutlined, ClockCircleOutlined, CloudUploadOutlined, DatabaseOutlined, DeploymentUnitOutlined, FileSearchOutlined, InfoCircleOutlined, LockOutlined, ReloadOutlined, SaveOutlined, UndoOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { ApiFeedback } from '../../components/ApiFeedback';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { InfoAlert } from '../../components/InfoAlert';
import { useApiResource } from '../../data/api/useApiResource';
import type { ConfigurationSnapshot, OperationalPolicy } from '../../data/api/operationalTypes';
import { requestApi } from '../../data/api/client';
import { useAccessSession } from '../../data/api/useAccessSession';
import './settings.css';

const categories = [
  { name: '运行', description: '服务运行环境', icon: <DeploymentUnitOutlined /> },
  { name: '数据库', description: '存储与分表策略', icon: <DatabaseOutlined /> },
  { name: '审计', description: '请求审计能力', icon: <FileSearchOutlined /> },
  { name: '保留', description: '备份新鲜度要求', icon: <ClockCircleOutlined /> },
  { name: '诊断', description: '后台治理与预热', icon: <ApiOutlined /> },
] as const;

const parameterHelp: Record<string, string> = {
  '环境': '当前服务使用的运行环境。',
  '数据库提供程序': '当前服务使用的数据库引擎。',
  '分表粒度': '组织包裹物理分表所使用的时间粒度。',
  '允许物理分表创建': '决定后台服务是否可以执行实际建表。',
  '分表创建预演': '启用时仅预演建表计划。',
  '只读审计 API': '用于查询已记录的请求审计信息。',
  '备份文件最长允许年龄': '验证备份新鲜度时使用的时间上限。',
  '备份治理': '检查数据库备份状态的后台服务。',
  '备份轮询间隔': '服务器配置中备份治理的轮询周期。',
  '连接预热': '启动时预热数据库连接。',
};

type PolicyValues = Omit<OperationalPolicy, 'revision'>;

function PolicyForm({ policy, canManage, permissionPending, busy, onSave }: {
  policy: OperationalPolicy; canManage: boolean; permissionPending: boolean; busy: boolean;
  onSave: (values: PolicyValues) => Promise<void>;
}) {
  const [form] = Form.useForm<PolicyValues>();
  const [dirty, setDirty] = useState(false);
  const automaticBackups = Form.useWatch('automaticBackups', form) ?? policy.automaticBackups;
  const disabled = !canManage || busy;
  const reset = () => { form.resetFields(); setDirty(false); };

  return <Form name="operationalPolicy" form={form} layout="vertical" initialValues={policy} disabled={disabled} onFinish={onSave}
    onValuesChange={(_, values: PolicyValues) => setDirty(values.automaticBackups !== policy.automaticBackups || values.backupIntervalMinutes !== policy.backupIntervalMinutes || values.prebuildAheadHours !== policy.prebuildAheadHours)}>
    <div className="settings-policy-fields">
      <div className="settings-policy-field settings-policy-toggle">
        <Form.Item name="automaticBackups" label="自动创建备份" valuePropName="checked"><Switch checkedChildren="启用" unCheckedChildren="关闭" /></Form.Item>
        <p className="settings-field-help">{automaticBackups ? '按设定间隔自动创建数据库备份。' : '开启后，按设定间隔自动创建数据库备份。'}</p>
      </div>
      <div className="settings-policy-field">
        <Form.Item name="backupIntervalMinutes" label="备份间隔" rules={[{ required: true, type: 'number', min: 10, max: 1440, message: '请输入 10～1440 分钟' }]}>
          <InputNumber min={10} max={1440} precision={0} addonAfter="分钟" />
        </Form.Item>
        <p className="settings-field-help">10～1440 分钟，控制自动备份频率。</p>
      </div>
      <div className="settings-policy-field">
        <Form.Item name="prebuildAheadHours" label="预建窗口" rules={[{ required: true, type: 'number', min: 1, max: 168, message: '请输入 1～168 小时' }]}>
          <InputNumber min={1} max={168} precision={0} addonAfter="小时" />
        </Form.Item>
        <p className="settings-field-help">1～168 小时，供手动预建分表使用。</p>
      </div>
    </div>
    <div className="settings-policy-footer">
      <div className={`settings-form-status${dirty ? ' pending' : ''}`} role="status" aria-live="polite">
        {permissionPending ? '正在确认修改权限…' : !canManage ? <><LockOutlined />当前账号仅可查看策略</> : dirty ? <><span className="settings-status-dot" />有未保存的修改</> : '当前没有未保存的修改'}
      </div>
      <div className="settings-policy-actions">
        <Button icon={<UndoOutlined />} onClick={reset} disabled={disabled || !dirty}>撤销修改</Button>
        <Button type="primary" htmlType="submit" icon={<SaveOutlined />} loading={busy} disabled={disabled || !dirty}>保存策略</Button>
      </div>
    </div>
  </Form>;
}

function ParameterValue({ value }: { value: string }) {
  const enabled = value === '启用' || value === '是';
  const disabled = value === '关闭' || value === '否';
  return enabled || disabled
    ? <span className={`settings-parameter-state${enabled ? ' enabled' : ''}`}><i aria-hidden="true" />{value}</span>
    : <span className="settings-parameter-value">{value}</span>;
}
export function SettingsPage() {
  const { message } = App.useApp();
  const [category, setCategory] = useState<(typeof categories)[number]['name']>('运行');
  const [busy, setBusy] = useState(false);
  const resource = useApiResource<ConfigurationSnapshot>('/api/operations/configuration');
  const policy = useApiResource<OperationalPolicy>('/api/operations/configuration/policy');
  const session = useAccessSession();
  const canManage = session.data?.permissions.includes('access.manage') ?? false;
  const activeCategory = categories.find(item => item.name === category)!;
  const parameters = resource.data?.settings.filter(item => item.category === category) ?? [];
  const refresh = () => { resource.refresh(); policy.refresh(); };
  const save = async (values: PolicyValues) => {
    if (!policy.data || !canManage || busy) return;
    setBusy(true);
    try { await requestApi('/api/operations/configuration/policy', undefined, { method: 'PUT', body: JSON.stringify({ ...values, revision: policy.data.revision }) }); policy.refresh(); message.success('运维策略已保存，后台将在一分钟内加载'); }
    catch (error) { message.error(error instanceof Error ? error.message : '保存失败'); } finally { setBusy(false); }
  };
  return <div className="system-settings" aria-busy={resource.loading || policy.loading}>
    <PageIntro title="系统配置" description="管理在线运维策略，查看服务器运行、数据库与治理参数。" action={<div className="settings-header-actions">
      <span className="settings-environment" title={resource.data?.environment}><DeploymentUnitOutlined /><span className="settings-environment-label">{resource.data?.environment ?? (resource.loading ? '读取环境中…' : '环境未知')}</span></span>
      <Button icon={<ReloadOutlined />} onClick={refresh} loading={resource.loading || policy.loading} disabled={busy}>刷新配置</Button>
    </div>} />
    <InfoAlert message="在线策略保存后跨重启保留" description="自动备份策略将在一分钟内加载；手动预建使用已保存的窗口。其余参数通过服务器配置维护。" closable={false} />
    <div className="settings-layout">
      <SectionCard className="settings-navigation" title={<span className="settings-section-title">配置分类</span>}>
        <nav className="settings-category-list" aria-label="配置分类">{categories.map(item => <button type="button" className={`settings-category${category === item.name ? ' selected' : ''}`} aria-pressed={category === item.name}
          key={item.name} onClick={() => setCategory(item.name)}>
          <span className="settings-category-icon" aria-hidden="true">{item.icon}</span>
          <span className="settings-category-copy"><span>{item.name}</span><small>{item.description}</small></span>
          <span className="settings-category-count" title="参数数量">{resource.data ? resource.data.settings.filter(setting => setting.category === item.name).length : '—'}</span>
        </button>)}</nav>
        <div className="settings-navigation-note"><InfoCircleOutlined />参数值来自当前服务器配置。</div>
      </SectionCard>
      <div className="settings-main">
        <SectionCard className="settings-policy-card" title={<div className="settings-card-heading"><span className="settings-heading-icon"><CloudUploadOutlined /></span><div>
          <div className="settings-section-title">运维策略</div><div className="settings-section-caption">在线维护自动备份与分表预建窗口</div>
        </div></div>} extra={<span className={`settings-access-badge${canManage ? ' editable' : ''}`}>{session.loading ? '确认权限中' : canManage ? '可在线修改' : '只读查看'}</span>}>
          {policy.loading ? <Skeleton active title={false} paragraph={{ rows: 5 }} /> : policy.error ? <ApiFeedback error={policy.error} retry={policy.refresh} /> : policy.data
            ? <PolicyForm key={policy.data.revision} policy={policy.data} canManage={canManage} permissionPending={session.loading} busy={busy} onSave={save} />
            : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚未读取运维策略" />}
          {session.error && <ApiFeedback error={session.error} retry={session.refresh} />}
        </SectionCard>
        <SectionCard className="settings-parameters-card" title={<div className="settings-card-heading"><span className="settings-heading-icon">{activeCategory.icon}</span><div>
          <div className="settings-section-title">{category}配置</div><div className="settings-section-caption">{activeCategory.description}{resource.data && ` · ${parameters.length} 项参数`}</div>
        </div></div>} extra={<span className="settings-access-badge"><LockOutlined />服务器维护</span>}>
          {resource.loading ? <Skeleton active title={false} paragraph={{ rows: 4 }} /> : resource.error ? <ApiFeedback error={resource.error} retry={resource.refresh} />
            : parameters.length ? <dl className="settings-parameter-grid">{parameters.map(item => <div className="settings-parameter" key={item.name}>
              <dt>{item.name}</dt><dd><ParameterValue value={item.value} />{parameterHelp[item.name] && <p>{parameterHelp[item.name]}</p>}</dd>
            </div>)}</dl> : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="该分类暂无服务器参数" />}
          <div className="settings-config-source">
            <div><span>配置来源</span><p>服务器 appsettings 与部署环境变量</p></div>
            <div><span>生效方式</span><p>按服务器实际配置与后台服务加载方式生效</p></div>
          </div>
        </SectionCard>
      </div>
    </div>
  </div>;
}
