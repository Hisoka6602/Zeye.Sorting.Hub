import { Alert, App, Button, DatePicker, Form, Input } from 'antd';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';
import { createDetectionRecordId } from '../../data/api/detectionIdentity';

/** 在测量及格口结果未知时登记来源检测，重载页面后仍生成同一检测记录身份。 */
interface DetectionForm { sourceInstanceId: string; sourceRunId: string; sourceParcelId: string; occurredAt: Dayjs; barcode?: string; workstationName?: string }

export function ParcelDetectionPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();
  const create = async (values: DetectionForm) => {
    const fact = { ...values, sourceInstanceId: values.sourceInstanceId.trim(), sourceRunId: values.sourceRunId.trim(), sourceParcelId: values.sourceParcelId.trim(), occurredAt: values.occurredAt.format('YYYY-MM-DDTHH:mm:ss.SSS'), stage: 0 };
    setSaving(true); setError(undefined);
    try {
      const recordId = await createDetectionRecordId(fact);
      const result = await requestApi<{ parcelId: string; isDuplicate: boolean }>('/api/admin/parcels/processing-records', undefined, { method: 'POST', body: JSON.stringify({ ...fact, recordId }) });
      message.success(result.isDuplicate ? '该检测记录已保存' : '包裹检测记录已保存');
      navigate(`/parcels/${result.parcelId}`);
    } catch (failure) { setError(failure instanceof Error ? failure.message : '保存失败，请重试'); }
    finally { setSaving(false); }
  };
  return <>
    <PageIntro title="来源检测登记" description="仅供管理员测试来源检测。业务检测记录由工作台或融合服务自动传入。" />
    <SectionCard className="create-card">
      {error && <Alert showIcon type="error" message="保存失败" description={error} style={{ marginBottom: 20 }} />}
      <Form<DetectionForm> layout="vertical" onFinish={create} disabled={saving} requiredMark>
        <div className="section-band">来源身份</div>
        <div className="two-cols">
          <Form.Item name="sourceInstanceId" label="来源实例" rules={[{ required: true, whitespace: true, max: 96, message: '请输入来源实例，最多96个字符' }]}><Input placeholder="例如 sorter-01" /></Form.Item>
          <Form.Item name="sourceRunId" label="设备编号会话" rules={[{ required: true, whitespace: true, max: 96, message: '请输入编号有效会话，最多96个字符' }]} extra="设备计数重置时更换；服务重启后保持原会话"><Input placeholder="例如 counter-session-01" /></Form.Item>
          <Form.Item name="sourceParcelId" label="来源包裹编号" rules={[{ required: true, message: '请输入正整数编号' }, { validator: async (_, value: string) => { if (!value || !/^[1-9]\d*$/.test(value.trim()) || BigInt(value.trim()) > 9223372036854775807n) throw new Error('编号须为1至9223372036854775807之间的整数'); } }]}><Input inputMode="numeric" placeholder="请输入设备包裹编号" /></Form.Item>
          <Form.Item name="workstationName" label="工作台名称" rules={[{ max: 128 }]}><Input placeholder="选填" /></Form.Item>
        </div>
        <div className="section-band">检测信息</div>
        <div className="two-cols">
          <Form.Item name="occurredAt" label="检测时间" rules={[{ required: true, message: '请选择检测时间' }]} extra="使用设备所在站点的本地时间"><DatePicker showTime placeholder="请选择检测日期和时间" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="barcode" label="主条码" rules={[{ max: 1024 }]}><Input placeholder="尚未识别时可留空" /></Form.Item>
        </div>
        <div className="create-form-footer"><Button onClick={() => navigate('/parcels')}>取消</Button><Button type="primary" htmlType="submit" loading={saving}>保存检测</Button></div>
      </Form>
    </SectionCard>
  </>;
}
