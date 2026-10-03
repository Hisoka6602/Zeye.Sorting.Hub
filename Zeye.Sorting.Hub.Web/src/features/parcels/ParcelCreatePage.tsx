import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Select, Tag } from 'antd';
import type { Dayjs } from 'dayjs';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { NumberUnit } from '../../components/NumberUnit';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';

/** Full creation contract; keep every 64-bit integer as a string in JavaScript. */
interface ParcelFormValues {
  id: string;
  timestamp: string;
  kind: number;
  barcode: string;
  bag?: string;
  workstation: string;
  scanTime: Dayjs;
  landedTime: Dayjs;
  target: string;
  actual: string;
  requestStatus: number;
  weight: number;
}

const maximumInt64 = 9223372036854775807n;
const positiveInt64 = { validator: async (_: unknown, value: string | null | undefined) => {
  if (value == null || value === '') return;
  if (!/^[1-9]\d*$/.test(value) || BigInt(value) > maximumInt64) throw new Error('请输入有效的正整数');
} };

/** Create a complete parcel through the synchronous admin API. */
export function ParcelCreatePage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();
  const create = async (values: ParcelFormValues) => {
    setSaving(true); setError(undefined);
    try {
      const result = await requestApi<{ id: string }>('/api/admin/parcels', undefined, {
        method: 'POST',
        body: JSON.stringify({
          id: values.id,
          parcelTimestamp: values.timestamp,
          type: values.kind,
          barCodes: values.barcode.trim(),
          bagCode: values.bag?.trim() ?? '',
          workstationName: values.workstation.trim(),
          scannedTime: values.scanTime.format('YYYY-MM-DDTHH:mm:ss'),
          dischargeTime: values.landedTime.format('YYYY-MM-DDTHH:mm:ss'),
          targetChuteId: values.target,
          actualChuteId: values.actual,
          requestStatus: values.requestStatus,
          weight: values.weight,
        }),
      });
      message.success('包裹已创建');
      navigate(`/parcels/${result.id}`);
    } catch (failure) { setError(failure instanceof Error ? failure.message : '创建失败，请重试'); }
    finally { setSaving(false); }
  };
  return <>
    <PageIntro title="新建包裹" description="仅供管理员测试使用。业务包裹由工作台或融合服务自动传入。" />
    <SectionCard className="create-card">
      {error && <Alert showIcon type="error" message="创建失败" description={error} style={{ marginBottom: 20 }} />}
      <Form<ParcelFormValues> layout="vertical" onFinish={create} disabled={saving} requiredMark>
        <div className="section-band">身份与条码</div>
        <div className="three-cols">
          <Form.Item name="id" label="包裹 ID" rules={[{ required: true, message: '请输入包裹 ID' }, positiveInt64]}><InputNumber<string> stringMode precision={0} min="1" placeholder="请输入正整数" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="timestamp" label="包裹时间戳 (Unix Ticks)" rules={[{ required: true, message: '请输入时间戳' }, positiveInt64]}><InputNumber<string> stringMode precision={0} min="1" placeholder="请输入正整数" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="kind" label="包裹类型" rules={[{ required: true, message: '请选择包裹类型' }]}><Select placeholder="请选择包裹类型" options={[{ value: 0, label: '标准包裹' }, { value: 1, label: '大件' }, { value: 2, label: '聚合包裹' }, { value: 3, label: '超薄包裹' }, { value: 4, label: '异形件' }, { value: 5, label: '流体包裹' }, { value: 6, label: '易碎品' }]} /></Form.Item>
          <Form.Item name="barcode" label="主条码" rules={[{ required: true, whitespace: true, message: '请输入主条码' }]}><Input placeholder="请输入主条码" /></Form.Item>
          <Form.Item name="bag" label={<span>集包号 <Tag className="optional-tag" style={{ marginLeft: 5 }}>选填</Tag></span>}><Input placeholder="请输入集包号" /></Form.Item>
          <Form.Item name="workstation" label="工作台名称" rules={[{ required: true, whitespace: true, message: '请输入工作台名称' }]}><Input placeholder="请输入工作台名称" /></Form.Item>
        </div>
        <div className="section-band">分拣信息</div>
        <div className="two-cols">
          <Form.Item name="scanTime" label="扫码时间" rules={[{ required: true, message: '请选择扫码时间' }]} extra="使用本地时间，不含时区偏移"><DatePicker showTime style={{ width: '100%' }} placeholder="请选择日期和时间" /></Form.Item>
          <Form.Item name="landedTime" label="落格时间" rules={[{ required: true, message: '请选择落格时间' }]} extra="使用本地时间，不含时区偏移"><DatePicker showTime style={{ width: '100%' }} placeholder="请选择日期和时间" /></Form.Item>
          <Form.Item name="target" label="目标格口 ID" rules={[{ required: true, message: '请输入目标格口 ID' }, positiveInt64]}><InputNumber<string> stringMode precision={0} min="1" placeholder="请输入正整数" style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="actual" label="实际格口 ID" rules={[{ required: true, message: '请输入实际格口 ID' }, positiveInt64]}><InputNumber<string> stringMode precision={0} min="1" placeholder="请输入正整数" style={{ width: '100%' }} /></Form.Item>
        </div>
        <div className="section-band">测量与扩展</div>
        <div className="two-cols">
          <Form.Item name="requestStatus" label="外部接口状态" rules={[{ required: true, message: '请选择外部接口状态' }]}><Select placeholder="请选择外部接口状态" options={[{ value: 0, label: '待发送' }, { value: 1, label: '成功' }, { value: 2, label: '失败' }]} /></Form.Item>
          <Form.Item name="weight" label="重量 (kg)" rules={[{ required: true, message: '请输入重量' }]}><NumberUnit min={0} precision={2} unit="kg" placeholder="请输入重量" style={{ width: '100%' }} /></Form.Item>
        </div>
        <div className="create-form-footer"><Button onClick={() => navigate('/parcels')}>取消</Button><Button type="primary" htmlType="submit" loading={saving}>创建包裹</Button></div>
      </Form>
    </SectionCard>
  </>;
}
