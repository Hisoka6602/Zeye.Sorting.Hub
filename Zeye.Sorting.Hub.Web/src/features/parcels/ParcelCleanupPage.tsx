import { App, Button, Checkbox, DatePicker } from 'antd';
import { InfoCircleOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';

export function ParcelCleanupPage() {
  const { modal, message } = App.useApp();
  const [saving, setSaving] = useState(false);
  const [before, setBefore] = useState<Dayjs | null>(null);
  const [accepted, setAccepted] = useState(false);
  const [result, setResult] = useState<{ decision: string; planned: number; executed: number; boundary: string } | null>(null);
  const execute = () => {
    if (!before) { message.warning('请选择清理日期'); return; }
    if (!accepted) { message.warning('请先确认影响范围'); return; }
    modal.confirm({ title: '确认提交清理请求', content: <p>创建时间早于 {before.format('YYYY-MM-DD')} 的包裹将进入服务端决策流程。如果服务端已允许执行，符合条件的包裹将被删除。</p>, okText: '确认提交', onOk: async () => {
      setSaving(true);
      try {
        const response = await requestApi<{ decision: string; plannedCount: number; executedCount: number; compensationBoundary: string }>('/api/admin/parcels/cleanup-expired', undefined, { method: 'POST', body: JSON.stringify({ createdBefore: before.startOf('day').format('YYYY-MM-DDTHH:mm:ss') }) });
        setResult({ decision: response.decision, planned: response.plannedCount, executed: response.executedCount, boundary: response.compensationBoundary });
      } catch (failure) { message.error(failure instanceof Error ? failure.message : '清理请求失败'); throw failure; }
      finally { setSaving(false); }
    } });
  };
  return <div className="cleanup-page">
    <PageIntro title="过期包裹清理" description="按创建时间清理符合过期策略的包裹数据，释放存储空间，保持系统健康。" />
    <InfoAlert type="warning" message="清理决策由服务端隔离器控制，结果可能为阻断、演练或真实执行" />
    <SectionCard title="清理条件" className="cleanup-condition"><p className="text-muted" style={{ marginTop: -8 }}>选择时间范围并确认影响范围后，提交清理请求。</p>
      <div className="cleanup-form">
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}><b style={{ minWidth: 126 }}><span style={{ color: '#e74c4c' }}>* </span>创建时间早于</b><DatePicker value={before} onChange={setBefore} style={{ width: 405 }} placeholder="请选择日期" /></div>
        <div className="cleanup-impact"><b><InfoCircleOutlined />影响范围说明</b><ul style={{ color: '#5f708c', marginBottom: 0 }}><li>将清理创建时间早于所选日期的包裹数据。</li><li>包括包裹主数据及聚合附属数据；来源身份和处理事实保留用于追溯。</li></ul></div>
        <div className="cleanup-confirm" style={{ marginLeft: 147, marginTop: 22 }}><span className="cleanup-required">*</span><Checkbox checked={accepted} onChange={event => setAccepted(event.target.checked)}>我已阅读并理解上述影响范围，确认提交清理请求</Checkbox></div>
        <Button style={{ marginLeft: 147, marginTop: 33 }} type="primary" loading={saving} onClick={execute}>提交清理请求</Button>
      </div>
    </SectionCard>
    <SectionCard title="结果预览" className="cleanup-result"><p className="text-muted" style={{ marginTop: -8 }}>提交后将显示服务端的实际决策与执行结果。</p>
      <div className="four-stats">{[['决策', result?.decision || '未提交'], ['计划处理数', result?.planned ?? '未提交'], ['实际执行数', result?.executed ?? '未提交'], ['补偿边界', result?.boundary || '未提交']].map(([label, value]) => <div className="stat-divider" key={label}><div className="text-muted">{label}</div><b>{value}</b><div className="text-muted">-</div></div>)}</div>
    </SectionCard>
  </div>;
}
