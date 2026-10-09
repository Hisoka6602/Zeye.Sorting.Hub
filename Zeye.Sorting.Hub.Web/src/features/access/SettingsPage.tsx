import { Alert, App, Button, Empty, Form, Input, Modal, Skeleton, Switch, Tabs } from 'antd';
import { ApiOutlined, ClockCircleOutlined, CloudUploadOutlined, DatabaseOutlined, DeploymentUnitOutlined, FileSearchOutlined, InfoCircleOutlined, LockOutlined, ReloadOutlined, SaveOutlined, UndoOutlined } from '@ant-design/icons';
import { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router';
import type { DatabaseStartupStatus } from '../../data/api/configurationTypes';
import { ApiFeedback } from '../../components/ApiFeedback';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { InfoAlert } from '../../components/InfoAlert';
import { useApiResource } from '../../data/api/useApiResource';
import type { ConfigurationSnapshot, OperationalPolicy } from '../../data/api/operationalTypes';
import { requestApi, requestHttpApi } from '../../data/api/client';
import { useAccessSession } from '../../data/api/useAccessSession';
import { RuntimeConfigurationPanel } from './RuntimeConfigurationPanel';
import { ConfigurationNumberPicker } from '../../components/ConfigurationNumberPicker';
import { RuntimeConfigurationHistory } from './RuntimeConfigurationHistory';
import { useConfigurationNavigationGuard } from './useConfigurationNavigationGuard';
import './settings.css';

const categories = [
  { name: '运行', description: '服务运行环境', icon: <DeploymentUnitOutlined /> },
  { name: '数据库', description: '存储与分表策略', icon: <DatabaseOutlined /> },
  { name: '审计', description: '请求审计能力', icon: <FileSearchOutlined /> },
  { name: '保留', description: '备份新鲜度要求', icon: <ClockCircleOutlined /> },
  { name: '诊断', description: '后台治理与预热', icon: <ApiOutlined /> },
] as const;

/** 数据库未就绪时复用现有配置表单，不请求业务账号、策略或变更历史。 */
export function DatabaseSetupPage({ status, onRefresh }: { status: DatabaseStartupStatus; onRefresh: () => void }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [key, setKey] = useState(() => new URLSearchParams(location.hash.slice(1)).get('setup') ?? '');
  const [acceptedKey, setAcceptedKey] = useState(key);
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const onDraftStateChange = useCallback((changed: boolean, saving: boolean) => { setDirty(changed); setBusy(saving); }, []);
  // 访问码只从片段读取，立即移除，防止后续导航和复制地址携带访问码。
  useEffect(() => {
    if (new URLSearchParams(location.hash.slice(1)).has('setup')) navigate({ pathname: location.pathname, search: location.search, hash: '' }, { replace: true });
  }, [location.hash, location.pathname, location.search, navigate]);
  useEffect(() => {
    const guard = (event: BeforeUnloadEvent) => { if (dirty || busy) { event.preventDefault(); event.returnValue = ''; } };
    window.addEventListener('beforeunload', guard); return () => window.removeEventListener('beforeunload', guard);
  }, [dirty, busy]);
  return <main className="system-settings" style={{ maxWidth: 1400, margin: '0 auto', padding: 24 }}>
    <PageIntro title="数据库配置" description="网页已启动。完成数据库连接配置后，重启服务即可进入登录和业务页面。" />
    <Alert type="warning" showIcon message="业务数据库尚未就绪" description={<><p>{status.failureSummary ?? '当前仅提供本机数据库配置，业务读写暂不可用。'}</p><p>保存后点击配置表单中的“重启 Host”；已保存的配置无需重复修改，也可直接重启重试连接。</p></>} action={<Button onClick={onRefresh} disabled={dirty || busy}>检查启动状态</Button>} />
    {status.localSetupAllowed ? <>
      <SectionCard title="本机配置访问码">
        <p>安装程序会自动打开带访问码的配置入口。手动打开时，请从服务器本机读取访问码文件：{status.setupKeyPath}</p>
        <Form layout="inline" onFinish={() => setAcceptedKey(key.trim())}>
          <Form.Item label="访问码"><Input.Password autoComplete="off" aria-label="本机配置访问码" value={key} onChange={event => setKey(event.target.value)} disabled={busy} /></Form.Item>
          <Button type="primary" htmlType="submit" disabled={!key.trim() || busy}>打开配置</Button>
        </Form>
      </SectionCard>
      {acceptedKey && <RuntimeConfigurationPanel key={acceptedKey} allowed authenticated active refreshToken={0} setupKey={acceptedKey} onDraftStateChange={onDraftStateChange} />}
    </> : <Alert type="info" showIcon message="请在服务器本机打开此页面" description="数据库配置入口不接受远程访问。数据库就绪后会恢复正常登录和业务入口。" />}
  </main>;
}

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

function PolicyForm({ policy, canManage, permissionPending, busy, onSave, onDirtyChange }: {
  policy: OperationalPolicy; canManage: boolean; permissionPending: boolean; busy: boolean;
  onSave: (values: PolicyValues) => Promise<void>;
  onDirtyChange: (dirty: boolean) => void;
}) {
  const [form] = Form.useForm<PolicyValues>();
  const [dirty, setDirty] = useState(false);
  const automaticBackups = Form.useWatch('automaticBackups', form) ?? policy.automaticBackups;
  const disabled = !canManage || busy;
  const reset = () => { form.resetFields(); setDirty(false); onDirtyChange(false); };

  return <Form name="operationalPolicy" form={form} layout="vertical" initialValues={policy} disabled={disabled} onFinish={onSave}
    onValuesChange={(_, values: PolicyValues) => { const changed = values.automaticBackups !== policy.automaticBackups || values.backupIntervalMinutes !== policy.backupIntervalMinutes || values.prebuildAheadHours !== policy.prebuildAheadHours; setDirty(changed); onDirtyChange(changed); }}>
    <div className="settings-policy-fields">
      <div className="settings-policy-field settings-policy-toggle">
        <Form.Item name="automaticBackups" label="自动创建备份" valuePropName="checked"><Switch checkedChildren="启用" unCheckedChildren="关闭" /></Form.Item>
        <p className="settings-field-help">{automaticBackups ? '按设定间隔自动创建数据库备份。' : '开启后，按设定间隔自动创建数据库备份。'}</p>
      </div>
      <div className="settings-policy-field">
        <Form.Item name="backupIntervalMinutes" label="备份间隔（分钟）" rules={[{ required: true, type: 'number', min: 10, max: 1440, message: '请选择或设置 10～1440 分钟' }]}>
          <ConfigurationNumberPicker min={10} max={1440} precision={0} presets={[15, 30, 60, 120, 360, 720, 1440]} unit="分钟" />
        </Form.Item>
        <p className="settings-field-help">10～1440 分钟，控制自动备份频率。</p>
      </div>
      <div className="settings-policy-field">
        <Form.Item name="prebuildAheadHours" label="预建窗口（小时）" rules={[{ required: true, type: 'number', min: 1, max: 168, message: '请选择或设置 1～168 小时' }]}>
          <ConfigurationNumberPicker min={1} max={168} precision={0} presets={[12, 24, 48, 72, 96, 120, 168]} unit="小时" />
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
  const [policyDirty, setPolicyDirty] = useState(false);
  const [runtimeDirty, setRuntimeDirty] = useState(false);
  const [runtimeBusy, setRuntimeBusy] = useState(false);
  const [tab, setTab] = useState('runtime');
  const [refreshToken, setRefreshToken] = useState(0);
  const resource = useApiResource<ConfigurationSnapshot>('/api/operations/configuration', requestHttpApi, false);
  const policy = useApiResource<OperationalPolicy>('/api/operations/configuration/policy', requestHttpApi, false);
  const session = useAccessSession();
  const canManage = session.data?.permissions.includes('access.manage') ?? false;
  const canManageRuntime = session.data?.authenticated === true && session.data.isSuperAdministrator === true;
  const savingConfiguration = busy || runtimeBusy;
  const blocker = useConfigurationNavigationGuard(canManageRuntime && runtimeDirty || canManage && policyDirty, savingConfiguration);
  const runtimeDraftChanged = useCallback((dirty: boolean, saving: boolean) => { setRuntimeDirty(dirty); setRuntimeBusy(saving); }, []);
  const activeCategory = categories.find(item => item.name === category)!;
  const parameters = resource.data?.settings.filter(item => item.category === category) ?? [];
  const refresh = () => { resource.refresh(); if (!policyDirty) policy.refresh(); setRefreshToken(value => value + 1); };
  const save = async (values: PolicyValues) => {
    if (!policy.data || !canManage || busy) return;
    setBusy(true);
    try { await requestApi('/api/operations/configuration/policy', undefined, { method: 'PUT', body: JSON.stringify({ ...values, revision: policy.data.revision }) }); setPolicyDirty(false); policy.refresh(); message.success('运维策略已保存，后台将在一分钟内加载'); }
    catch (error) { message.error(error instanceof Error ? error.message : '保存失败'); } finally { setBusy(false); }
  };
  return <div className="system-settings" aria-busy={resource.loading || policy.loading}>
    <PageIntro title="系统配置" description="在线维护运行参数与运维策略，查看配置的生效状态和变更历史。" action={<div className="settings-header-actions">
      <span className="settings-environment" title={resource.data?.environment}><DeploymentUnitOutlined /><span className="settings-environment-label">{resource.data?.environment ?? (resource.loading ? '读取环境中…' : '环境未知')}</span></span>
      <Button icon={<ReloadOutlined />} onClick={refresh} loading={resource.loading || policy.loading} disabled={savingConfiguration}>刷新配置</Button>
    </div>} />
    <InfoAlert message="配置保存后，重启仍保留" description="在线参数保存后发布热更新；数据库连接、监听地址等启动参数需要重启。环境覆盖值和待重启字段会单独显示。" closable={false} />
    <Tabs className="settings-tabs" activeKey={tab} onChange={next => { if (!savingConfiguration) setTab(next); }} items={[
      { key: 'runtime', label: '运行配置', children: <RuntimeConfigurationPanel allowed={canManageRuntime} authenticated={session.data?.authenticated ?? false} active={tab === 'runtime'} refreshToken={refreshToken} onDraftStateChange={runtimeDraftChanged} restartBlocked={policyDirty || busy} /> },
      { key: 'policy', label: '运维策略', children: <div className="settings-layout">
      <SectionCard className="settings-navigation" title={<span className="settings-section-title">配置分类</span>}>
        <nav className="settings-category-list" aria-label="配置分类">{categories.map(item => <button type="button" className={`settings-category${category === item.name ? ' selected' : ''}`} aria-pressed={category === item.name}
          key={item.name} onClick={() => setCategory(item.name)}>
          <span className="settings-category-icon" aria-hidden="true">{item.icon}</span>
          <span className="settings-category-copy"><span>{item.name}</span><small>{item.description}</small></span>
          <span className="settings-category-count" title="参数数量">{resource.data ? resource.data.settings.filter(setting => setting.category === item.name).length : '—'}</span>
        </button>)}</nav>
        <div className="settings-navigation-note"><InfoCircleOutlined />参数值来自当前已生效的配置。</div>
      </SectionCard>
      <div className="settings-main">
        <SectionCard className="settings-policy-card" title={<div className="settings-card-heading"><span className="settings-heading-icon"><CloudUploadOutlined /></span><div>
          <div className="settings-section-title">运维策略</div><div className="settings-section-caption">在线维护自动备份与分表预建窗口</div>
        </div></div>} extra={<span className={`settings-access-badge${canManage ? ' editable' : ''}`}>{session.loading ? '确认权限中' : canManage ? '可在线修改' : '只读查看'}</span>}>
          {policy.loading ? <Skeleton active title={false} paragraph={{ rows: 5 }} /> : policy.error ? <ApiFeedback error={policy.error} retry={policy.refresh} /> : policy.data
            ? <PolicyForm key={policy.data.revision} policy={policy.data} canManage={canManage} permissionPending={session.loading} busy={busy} onSave={save} onDirtyChange={setPolicyDirty} />
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
            <div><span>配置来源</span><p>配置库与部署环境覆盖</p></div>
            <div><span>生效方式</span><p>运行配置页可修改参数并查看在线或重启生效状态</p></div>
          </div>
        </SectionCard>
      </div>
    </div> },
      { key: 'history', label: '变更历史', children: <RuntimeConfigurationHistory allowed={canManageRuntime} active={tab === 'history'} refreshToken={refreshToken} /> },
    ]} />
    <Modal open={blocker.state === 'blocked'} title={savingConfiguration ? '配置正在保存' : '有未保存的配置修改'}
      okText="放弃修改并离开" cancelText="继续编辑" maskClosable={false} okButtonProps={{ danger: true, disabled: savingConfiguration }}
      onCancel={() => { if (blocker.state === 'blocked') blocker.reset(); }}
      onOk={() => { if (!savingConfiguration && blocker.state === 'blocked') blocker.proceed(); }}>
      <p>{savingConfiguration ? '请等待保存结果后再离开页面。' : '离开将丢失尚未保存的运行配置或运维策略修改。可以继续编辑，或明确放弃修改后离开。'}</p>
    </Modal>
  </div>;
}
