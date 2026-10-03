import { App, Button, Checkbox, DatePicker, Form, Input, Modal } from 'antd';
import { InfoCircleOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { useRef, useState } from 'react';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';
import { useAccessSession } from '../../data/api/useAccessSession';
import { cleanupDecisionLabels, type ParcelCleanupResponse } from '../../data/api/parcelCleanupTypes';
import { ParcelCleanupHistory } from './ParcelCleanupHistory';
import './parcelCleanup.css';

export function ParcelCleanupPage() {
  const { message } = App.useApp();
  const session = useAccessSession();
  const [passwordForm] = Form.useForm<{ password: string }>();
  const submitting = useRef(false);
  const [saving, setSaving] = useState(false);
  const [before, setBefore] = useState<Dayjs | null>(null);
  const [accepted, setAccepted] = useState(false);
  const [confirmDate, setConfirmDate] = useState<Dayjs | null>(null);
  const [passwordError, setPasswordError] = useState('');
  const [result, setResult] = useState<ParcelCleanupResponse | null>(null);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const canClean = session.data?.authenticated && session.data.permissions.includes('governance.manage');
  const execute = () => {
    if (!before) { message.warning('请选择清理日期'); return; }
    if (!accepted) { message.warning('请先确认影响范围'); return; }
    passwordForm.resetFields(); setPasswordError(''); setConfirmDate(before.startOf('day'));
  };
  const confirmCleanup = async () => {
    if (!confirmDate || submitting.current) return;
    submitting.current = true;
    let values: { password: string };
    try { values = await passwordForm.validateFields(); } catch { submitting.current = false; return; }
    setSaving(true); setPasswordError('');
    const payload = JSON.stringify({ createdBefore: confirmDate.format('YYYY-MM-DDTHH:mm:ss'), password: values.password });
    passwordForm.resetFields();
    try {
      const response = await requestApi<ParcelCleanupResponse>('/api/admin/parcels/cleanup-expired', undefined, { method: 'POST', body: payload });
      setResult(response); setConfirmDate(null); setAccepted(false);
      message.success(response.decision === 'execute' ? `清理完成，已删除 ${response.executedCount.toLocaleString()} 条，操作记录已保存` : `本次${cleanupDecisionLabels[response.decision] ?? '操作完成'}，结果已保存至清理历史`);
    } catch (failure) { setPasswordError(failure instanceof Error ? failure.message : '清理请求失败，请查看清理历史确认执行结果'); }
    finally { submitting.current = false; setSaving(false); setHistoryRevision(value => value + 1); }
  };
  return <div className="cleanup-page">
    <PageIntro title="过期包裹清理" description="按创建时间清理包裹数据，释放存储空间，并保留完整的操作追溯记录。" />
    <InfoAlert type="warning" message="清理会永久删除符合条件的包裹；提交前需验证当前用户的登录密码" description="每次操作及已删除包裹清单会长期保留在清理历史中。单次最多清理 10,000 条，物理删除不支持撤销。" closable={false} />
    <SectionCard title="清理条件" className="cleanup-condition"><p className="text-muted" style={{ marginTop: -8 }}>选择清理日期并确认影响范围，再验证密码提交。</p>
      <div className="cleanup-form">
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}><b style={{ minWidth: 126 }}><span className="cleanup-required-inline">* </span>创建时间早于</b><DatePicker value={before} onChange={value => { setBefore(value); setAccepted(false); }} style={{ width: 405, maxWidth: '100%' }} placeholder="请选择日期" /></div>
        <div className="cleanup-impact"><b><InfoCircleOutlined />影响范围说明</b><ul><li>将删除创建时间早于所选日期的包裹主数据及聚合附属数据。</li><li>来源身份、处理事实及本次清理记录保留用于后续追溯。</li></ul></div>
        <div className="cleanup-confirm" style={{ marginLeft: 147, marginTop: 22 }}><span className="cleanup-required">*</span><Checkbox checked={accepted} onChange={event => setAccepted(event.target.checked)}>我已阅读并理解上述影响范围，确认提交清理请求</Checkbox></div>
        <Button style={{ marginLeft: 147, marginTop: 33 }} type="primary" loading={saving} disabled={!canClean} onClick={execute}>提交清理请求</Button>
        {session.data && !canClean && <p className="text-muted">需要登录并具有数据治理权限才能提交清理。</p>}
      </div>
    </SectionCard>
    <SectionCard title="最近执行结果" className="cleanup-result">
      <div className="four-stats">{[['执行结果', result ? cleanupDecisionLabels[result.decision] ?? result.decision : '未提交'], ['计划处理数', result?.plannedCount.toLocaleString() ?? '未提交'], ['实际删除数', result?.executedCount.toLocaleString() ?? '未提交']].map(([label, value]) => <div className="stat-divider" key={label}><div className="text-muted">{label}</div><b>{value}</b></div>)}<div className="stat-divider"><div className="text-muted">操作追溯</div>{result?.cleanupRecordId ? <Button type="link" onClick={() => setSelectedId(result.cleanupRecordId!)}>查看本次清理记录</Button> : <b>未提交</b>}</div></div>
    </SectionCard>
    <SectionCard title="清理历史" className="cleanup-history"><ParcelCleanupHistory revision={historyRevision} selectedId={selectedId} onSelect={setSelectedId} /></SectionCard>
    <Modal title="验证密码并确认清理" open={confirmDate !== null} onCancel={() => { if (!saving) { setConfirmDate(null); passwordForm.resetFields(); setPasswordError(''); } }} onOk={confirmCleanup} okText="验证并清理" cancelText="取消" confirmLoading={saving} okButtonProps={{ danger: true }} cancelButtonProps={{ disabled: saving }} closable={!saving} maskClosable={false} keyboard={!saving} destroyOnHidden>
      <p>将删除创建时间早于 <strong>{confirmDate?.format('YYYY-MM-DD')}</strong> 的包裹。此操作无法撤销，执行结果和删除清单将永久记录。</p>
      <Form form={passwordForm} layout="vertical" preserve={false} onFinish={confirmCleanup}>
        <Form.Item name="password" label={`当前用户登录密码${session.data?.name ? `（${session.data.name}）` : ''}`} rules={[{ required: true, message: '请输入当前登录用户的密码' }]}>
          <Input.Password aria-label="当前用户登录密码" autoComplete="current-password" placeholder="请输入您的登录密码" maxLength={128} autoFocus disabled={saving} />
        </Form.Item>
      </Form>
      {passwordError && <InfoAlert type="error" message={passwordError} closable={false} />}
    </Modal>
  </div>;
}
