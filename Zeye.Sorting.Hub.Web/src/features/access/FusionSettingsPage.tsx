import { useEffect, useState } from 'react';
import { App, Alert, Badge, Button, Collapse, Drawer, Empty, Form, Input, InputNumber, Modal, Select, Skeleton, Space, Switch, Tag } from 'antd';
import { ApiOutlined, CopyOutlined, KeyOutlined, PlusOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { DataTable } from '../../components/DataTable';
import { ApiFeedback } from '../../components/ApiFeedback';
import { useApiResource } from '../../data/api/useApiResource';
import { useAccessSession } from '../../data/api/useAccessSession';
import { requestApi } from '../../data/api/client';
import type { FusionConfiguration, FusionPairing, FusionPairingResult, FusionPresence, FusionSettings, FusionSource } from '../../data/api/fusionTypes';
import './fusion-settings.css';

const api = '/api/operations/configuration/fusion';
const label = (name: string, key: string) => <span className="fusion-field-label"><strong>{name}</strong><small>{key}</small></span>;
const identityRules = [{ required: true, pattern: /^[A-Za-z0-9._-]{1,96}$/, message: '填写 1～96 位字母、数字、点、下划线或连字符' }];
const zoneOptions = [...new Set(['Asia/Shanghai', 'UTC', ...Intl.supportedValuesOf('timeZone')])].map(value => ({ value, label: value === 'Asia/Shanghai' ? 'Asia/Shanghai · 上海 / 北京时间' : value === 'UTC' ? 'UTC · 协调世界时' : value }));

function SettingsForm({ config, busy, onSave }: { config: FusionConfiguration; busy: boolean; onSave: (settings: FusionSettings, revision: number) => Promise<boolean> }) {
  const [form] = Form.useForm<FusionSettings>();
  const [dirty, setDirty] = useState(false);
  const [baseRevision, setBaseRevision] = useState(config.revision);
  useEffect(() => { if (!dirty) { form.setFieldsValue(config.settings); setBaseRevision(config.revision); } }, [config.revision, config.settings, dirty, form]);
  const numeric: [keyof FusionSettings, string, number, number][] = [
    ['maxBatchRecords', '事实批次条数上限', 1, 100], ['maxBatchBytes', '事实批次字节上限', 16384, 524288],
    ['maxImageChunkBytes', '图片分块字节上限', 1024, 65536], ['maxImageBytes', '单张图片字节上限', 1, 134217728],
    ['maxPendingImagesPerSource', '单来源未完成图片上限', 1, 1000], ['leaseSeconds', '心跳租约期限（秒）', 30, 600],
    ['uploadRetentionHours', '临时上传保留期（小时）', 1, 168],
  ];
  return <Form form={form} layout="vertical" initialValues={config.settings} onValuesChange={() => setDirty(true)} onFinish={async values => { if (await onSave(values, baseRevision)) setDirty(false); }} disabled={busy}>
    {dirty && baseRevision !== config.revision && <Alert type="warning" message="配置已被其他页面更新；当前草稿保留，请撤销修改以读取最新版本后再编辑。" />}
    <div className="fusion-settings-grid">
      <Form.Item name="isEnabled" label={label('启用 Fusion 接入', 'FusionIngestion.IsEnabled')} valuePropName="checked"><Switch /></Form.Item>
      <Form.Item label={label('Hub 标识', 'FusionIngestion.HubId')}><Input value={config.hubId} readOnly /><span className="fusion-help">由部署设置固定，已有包裹的归属保持稳定。</span></Form.Item>
      <Form.Item name="advertisedEndpoint" label={label('对外 SignalR 地址', 'FusionIngestion.AdvertisedEndpoint')}><Input placeholder="https://hub.example/hubs/fusion-ingestion" /></Form.Item>
      <Form.Item name="allowInsecureHttp" label={label('允许开发 HTTP 地址', 'FusionIngestion.AllowInsecureHttp')} valuePropName="checked" extra="本机 Docker 测试开启；生产使用 HTTPS。"><Switch /></Form.Item>
      <Form.Item name="discoveryEnabled" label={label('启用 UDP 自动发现', 'FusionIngestion.DiscoveryEnabled')} valuePropName="checked"><Switch /></Form.Item>
      <Form.Item name="discoveryPort" label={label('UDP 发现端口', 'FusionIngestion.DiscoveryPort')} rules={[{ required: true, type: 'number', min: 1024, max: 65535, message: '填写 1024–65535 范围内的整数端口' }, { validator: (_, value) => value === 5089 ? Promise.reject(new Error('5089 保留给 NarrowBeltSorter')) : Promise.resolve() }]}><InputNumber min={1024} max={65535} precision={0} /></Form.Item>
    </div>
    <Alert type="info" showIcon message="对外地址必须能从 Fusion 所在机器访问" description="本机 Docker Fusion 可使用 http://host.docker.internal:5087/hubs/fusion-ingestion；其他机器填写 Hub 的实际域名或局域网 IP。启用发现还需发布对应 UDP 端口，发现不会自动授予接入权限。" />
    <Collapse className="fusion-advanced" items={[{ key: 'limits', label: '传输限额与临时上传保留', children: <div className="fusion-settings-grid">{numeric.map(([key, title, min, max]) => <Form.Item key={key} name={key} label={label(title, `FusionIngestion.${key[0].toUpperCase()}${key.slice(1)}`)} rules={[{ required: true, type: 'number', min, max, message: `${title}必须为 ${min}–${max} 范围内的整数` }]}><InputNumber min={min} max={max} precision={0} /></Form.Item>)}<Form.Item label={label('图片持久化目录', 'FusionIngestion.ImageDirectory')}><Input value={config.imageDirectory} readOnly /><span className="fusion-help">由部署目录和持久化卷管理。</span></Form.Item></div> }]} />
    <div className="fusion-form-footer"><span>{dirty ? '有未保存的修改' : `已保存版本 ${config.revision} · 保存后在线生效`}</span><Space><Button disabled={!dirty || busy} onClick={() => { form.setFieldsValue(config.settings); setBaseRevision(config.revision); setDirty(false); }}>撤销修改</Button><Button type="primary" htmlType="submit" icon={<SaveOutlined />} disabled={!dirty || baseRevision !== config.revision} loading={busy}>保存接入设置</Button></Space></div>
  </Form>;
}

export function FusionSettingsPage() {
  const { message, modal } = App.useApp();
  const config = useApiResource<FusionConfiguration>(api);
  const presence = useApiResource<FusionPresence[]>('/api/parcels/fusion/sources');
  const session = useAccessSession();
  const canManage = session.data?.permissions.includes('access.manage') ?? false;
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<FusionSource | 'new' | null>(null);
  const [sourceRevision, setSourceRevision] = useState(0);
  const [pairing, setPairing] = useState<FusionPairing | null>(null);
  const [sourceForm] = Form.useForm<FusionSource>();
  const [search, setSearch] = useState('');
  const refresh = () => { config.refresh(); presence.refresh(); };
  const perform = async (action: () => Promise<unknown>) => {
    if (busy || !canManage) return false;
    setBusy(true);
    try { await action(); refresh(); return true; } catch (error) { message.error(error instanceof Error ? error.message : '保存失败'); return false; } finally { setBusy(false); }
  };
  const saveSettings = (settings: FusionSettings, revision: number) => perform(async () => {
    await requestApi(api, undefined, { method: 'PUT', body: JSON.stringify({ revision, settings }) });
    message.success('接入设置已保存并在线生效');
  });
  const openSource = (source: FusionSource | 'new') => {
    setSourceRevision(config.data!.revision);
    sourceForm.resetFields();
    sourceForm.setFieldsValue(source === 'new' ? { sourceInstanceId: '', workstationName: '', enabled: true, tenantId: 'default', storagePartitionId: 'default', lineId: 'line-01', siteCode: '', deviceCode: '', timeZoneId: 'Asia/Shanghai' } : source);
    setEditing(source);
  };
  const saveSource = (source: FusionSource) => perform(async () => {
    const create = editing === 'new';
    const result = await requestApi<FusionPairingResult>(create ? `${api}/sources` : `${api}/sources/${encodeURIComponent(source.sourceInstanceId)}`, undefined,
      { method: create ? 'POST' : 'PUT', body: JSON.stringify({ revision: sourceRevision, source }) });
    setEditing(null); if (create) setPairing(result.pairing);
    message.success(create ? '工作台已登记，请将配对信息导入 Fusion' : '工作台配置已在线生效');
  });
  const rotate = (source: FusionSource) => modal.confirm({ title: `重置 ${source.workstationName || source.sourceInstanceId} 的机器密钥？`, content: '旧密钥和既有连接租约立即失效。重置后将新配对信息导入该 Fusion，其他工作台不受影响。', okText: '重置并生成配对信息', cancelText: '取消', onOk: () => perform(async () => {
    const result = await requestApi<FusionPairingResult>(`${api}/sources/${encodeURIComponent(source.sourceInstanceId)}/rotate-key`, undefined, { method: 'POST', body: JSON.stringify({ revision: config.data!.revision }) });
    setPairing(result.pairing);
  }) });
  const copy = async (value: string) => { try { await navigator.clipboard.writeText(value); message.success('已复制'); } catch { message.error('无法访问剪贴板，请手动复制。'); } };
  const rows = (config.data?.sources ?? []).filter(source => `${source.sourceInstanceId} ${source.workstationName} ${source.lineId}`.toLowerCase().includes(search.toLowerCase()));
  const states = new Map((presence.data ?? []).map(source => [source.sourceInstanceId, source]));
  const locked = editing !== null && editing !== 'new' && editing.identityLocked;
  return <div className="fusion-management">
    <PageIntro title="Fusion 接入" description="在线登记分拣工作台，管理独立配对凭据与 SignalR / UDP 接入。" action={<Button icon={<ReloadOutlined />} onClick={refresh} loading={config.loading || presence.loading}>刷新状态</Button>} />
    <div className="fusion-summary"><div><span>已登记工作台</span><strong>{config.data?.sources.length ?? '—'}</strong></div><div><span>当前在线</span><strong>{presence.data?.filter(source => source.isOnline).length ?? '—'}</strong></div><div><span>接入服务</span><strong className="fusion-summary-state"><Badge status={config.data?.settings.isEnabled ? 'success' : 'default'} />{config.data ? config.data.settings.isEnabled ? '已启用' : '已关闭' : '读取中'}</strong></div></div>
    {config.error ? <ApiFeedback error={config.error} retry={config.refresh} /> : config.loading && !config.data ? <Skeleton active /> : config.data && <>
      <SectionCard title={<span><ApiOutlined /> 接入设置</span>} extra={<Tag color={canManage ? 'blue' : undefined}>{canManage ? '在线管理' : '需要管理员权限'}</Tag>}>
        {canManage ? <SettingsForm config={config.data} busy={busy} onSave={saveSettings} /> : <Alert type="info" message="请使用具有接入配置管理权限的账号登录。" />}
      </SectionCard>
      <SectionCard title="已登记工作台" extra={<Button type="primary" icon={<PlusOutlined />} disabled={!canManage || busy} onClick={() => openSource('new')}>新增工作台</Button>}>
        <div className="fusion-source-toolbar"><Input.Search allowClear placeholder="搜索工作台名称、来源标识或产线" aria-label="搜索已登记工作台" value={search} onChange={event => setSearch(event.target.value)} /><span>每个 Fusion 使用独立来源标识和机器密钥</span></div>
        {presence.error && <ApiFeedback error={presence.error} retry={presence.refresh} />}
        <DataTable<FusionSource> rowKey="sourceInstanceId" dataSource={rows} locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚未登记 Fusion。点击新增工作台，生成配对信息后导入 Fusion。" /> }} columns={[
          { title: '工作台 / 来源', key: 'name', width: 245, render: (_, source) => <div className="fusion-source-name"><strong>{source.workstationName || source.sourceInstanceId}</strong><small>{source.sourceInstanceId}</small></div> },
          { title: '产线 / 时区', key: 'line', width: 210, render: (_, source) => <div className="fusion-source-name"><span>{source.lineId}</span><small>{source.timeZoneId}</small></div> },
          { title: '连接状态', key: 'status', width: 110, render: (_, source) => <Badge status={!source.enabled ? 'default' : states.get(source.sourceInstanceId)?.isOnline ? 'success' : 'warning'} text={!source.enabled ? '已停用' : states.get(source.sourceInstanceId)?.isOnline ? '在线' : '离线'} /> },
          { title: '最近心跳', key: 'seen', width: 190, render: (_, source) => states.get(source.sourceInstanceId)?.lastSeenAt?.replace('T', ' ') ?? '尚未接入' },
          { title: '待确认 / 图片', key: 'pending', width: 145, render: (_, source) => `${states.get(source.sourceInstanceId)?.pendingFacts ?? 0} / ${states.get(source.sourceInstanceId)?.pendingImages ?? 0}` },
          { title: '操作', key: 'action', width: 175, render: (_, source) => <Space wrap><Button type="link" size="small" disabled={!canManage || busy} onClick={() => openSource(source)}>编辑 / 停用</Button><Button type="link" size="small" disabled={!canManage || busy} onClick={() => rotate(source)}>重置密钥</Button></Space> },
        ]} />
      </SectionCard>
    </>}
    <Drawer title={editing === 'new' ? '新增分拣工作台' : '编辑分拣工作台'} open={editing !== null} onClose={() => !busy && setEditing(null)} width={580} destroyOnClose>
      <Alert showIcon type="info" message={locked ? '已接入工作台的来源及业务归属固定；名称、启停和密钥可在线管理。' : '来源标识、产线与时区必须和 Fusion 一致，保存后立即允许配对。'} />
      <Form form={sourceForm} layout="vertical" onFinish={saveSource} disabled={busy} className="fusion-source-form">
        <div className="fusion-settings-grid">
          <Form.Item name="sourceInstanceId" label={label('Fusion 来源标识', 'SourceInstanceId')} rules={identityRules}><Input disabled={editing !== 'new'} placeholder="fusion-line-01" /></Form.Item>
          <Form.Item name="workstationName" label={label('工作台名称', 'WorkstationName')} rules={[{ required: true, max: 128, message: '填写工作台名称，最多 128 个字符' }]}><Input placeholder="分拣工作台 1" /></Form.Item>
          <Form.Item name="lineId" label={label('产线标识', 'LineId')} rules={identityRules}><Input disabled={locked} /></Form.Item>
          <Form.Item name="timeZoneId" label={label('业务时区', 'TimeZoneId')} rules={[{ required: true, message: '请选择业务时区' }]}><Select disabled={locked} showSearch optionFilterProp="label" options={zoneOptions} /></Form.Item>
          <Form.Item name="siteCode" label={label('站点编码（可留空）', 'SiteCode')} rules={[{ pattern: /^[A-Za-z0-9._-]{0,96}$/, message: '最多 96 位字母、数字、点、下划线或连字符' }]}><Input disabled={locked} /></Form.Item>
          <Form.Item name="deviceCode" label={label('设备编码（可留空）', 'DeviceCode')} rules={[{ pattern: /^[A-Za-z0-9._-]{0,96}$/, message: '最多 96 位字母、数字、点、下划线或连字符' }]}><Input disabled={locked} /></Form.Item>
          <Form.Item name="tenantId" label={label('租户标识', 'TenantId')} rules={identityRules}><Input disabled={locked} /></Form.Item>
          <Form.Item name="storagePartitionId" label={label('存储分区', 'StoragePartitionId')} rules={identityRules}><Input disabled={locked} /></Form.Item>
          <Form.Item name="enabled" label={label('允许工作台接入', 'Enabled')} valuePropName="checked"><Switch /></Form.Item>
        </div>
        <div className="fusion-form-footer"><Button onClick={() => setEditing(null)} disabled={busy}>取消</Button><Button type="primary" htmlType="submit" loading={busy}>{editing === 'new' ? '登记并生成配对信息' : '保存工作台'}</Button></div>
      </Form>
    </Drawer>
    <Modal title={<span><KeyOutlined /> Fusion 配对信息</span>} open={Boolean(pairing)} onCancel={() => setPairing(null)} footer={<Button type="primary" onClick={() => setPairing(null)}>完成</Button>} width={650} destroyOnClose>
      {pairing && <div className="fusion-pairing"><Alert type="warning" showIcon message="完整密钥仅本次显示，请及时复制配对信息" description="在 Fusion → 参数配置 → Hub 点击“导入配对信息”。关闭后需要重置密钥才能重新获取，正常配置查询不会返回密钥。" />
        <dl><dt>来源标识</dt><dd>{pairing.sourceInstanceId}</dd><dt>目标 Hub</dt><dd>{pairing.hubId}</dd><dt>连接地址</dt><dd>{pairing.endpoint || '尚未填写；请先配置 Hub 的对外 SignalR 地址'}</dd><dt>产线 / 时区</dt><dd>{pairing.lineId} / {pairing.timeZoneId}</dd></dl>
        <Input.Password value={pairing.machineApiKey} readOnly aria-label="本次生成的机器密钥" />
        <Space wrap><Button type="primary" icon={<CopyOutlined />} onClick={() => copy(JSON.stringify(pairing, null, 2))}>复制配对信息 JSON</Button><Button onClick={() => copy(pairing.machineApiKey)}>仅复制机器密钥</Button></Space>
      </div>}
    </Modal>
  </div>;
}
