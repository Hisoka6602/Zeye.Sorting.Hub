import { App, Button, Input, Space, Tabs, Typography, Upload } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { parseApiJson, requestApi } from '../../data/api/client';
import { initialParcels } from '../../data/mock/parcels';

/** 批量预览保持完整请求内容，编号始终使用字符串。 */
type PreviewRow = { key: number; id: string; barcode: string; workstation: string; weight: unknown; target: string; payload: Record<string, unknown>; error?: string };
/** 真实批量入队结果；入队与数据库完成分别呈现。 */
interface BatchResult { acceptedCount: number; rejectedCount: number; queueDepth: number; isBackpressureTriggered: boolean; message: string }
const requiredFields = ['id', 'parcelTimestamp', 'type', 'barCodes', 'weight', 'workstationName', 'scannedTime', 'dischargeTime', 'targetChuteId', 'actualChuteId', 'requestStatus'];

/** 将完整新增合同提交给有界缓冲入口，不使用本地演示数据。 */
export function ParcelBatchPage() {
  const designPreview = import.meta.env.MODE === 'design-preview';
  const { message, modal } = App.useApp();
  const [tab, setTab] = useState('json');
  const [raw, setRaw] = useState('');
  const [preview, setPreview] = useState<PreviewRow[]>(() => designPreview ? initialParcels.slice(0, 5).map((item, key) => ({ key, id: String(item.id), barcode: item.barcode, workstation: item.workstation, weight: item.weight, target: item.target, payload: {} })) : []);
  const [validated, setValidated] = useState(designPreview);
  const [saving, setSaving] = useState(false);
  const parse = () => {
    try {
      let entries: unknown;
      if (tab === 'csv') {
        if (raw.includes('"')) throw new Error('带引号或字段内含逗号的数据请使用 JSON 导入');
        const [header, ...lines] = raw.trim().split(/\r?\n/);
        const columns = header.split(',').map(value => value.trim());
        entries = lines.filter(Boolean).map(line => Object.fromEntries(line.split(',').map((cell, index) => {
          const key = columns[index]; const value = cell.trim();
          if (['type', 'requestStatus', 'weight', 'length', 'width', 'height', 'volume', 'noReadType'].includes(key)) return [key, Number(value)];
          if (['isSticking', 'hasImages', 'hasVideos'].includes(key)) return [key, value === 'true' ? true : value === 'false' ? false : value];
          return [key, value];
        })));
      } else entries = parseApiJson(raw);
      if (!Array.isArray(entries) || entries.length === 0 || entries.length > 1000) throw new Error('请输入1至1000条完整新增合同');
      const seen = new Set<string>();
      const rows = entries.map((entry, key): PreviewRow => {
        const item = entry && typeof entry === 'object' && !Array.isArray(entry) ? entry as Record<string, unknown> : {};
        const id = String(item.id ?? '');
        const missing = requiredFields.filter(field => item[field] === null || item[field] === undefined || item[field] === '');
        let error = missing.length ? `缺少字段：${missing.join('、')}` : undefined;
        if (!/^[1-9]\d*$/.test(id) || BigInt(id) > 9223372036854775807n) error = '包裹编号超出有效整数范围';
        if (seen.has(id)) error = '批次内包裹编号重复';
        seen.add(id);
        return { key, id, barcode: String(item.barCodes ?? ''), workstation: String(item.workstationName ?? ''), weight: item.weight, target: String(item.targetChuteId ?? ''), payload: item, error };
      });
      setPreview(rows); setValidated(true); message.success(`已预览 ${rows.length} 条数据，最终校验由服务端执行`);
    } catch (error) { message.error(error instanceof Error ? error.message : '数据格式有误'); setValidated(false); }
  };
  const submit = async () => {
    if (designPreview) { message.info('设计预览只展示样本，不能提交到后端'); return; }
    if (!validated || preview.some(item => item.error)) { message.warning('请先校验并修正全部错误'); return; }
    setSaving(true);
    try {
      const result = await requestApi<BatchResult>('/api/admin/parcels/batch-buffer', undefined, { method: 'POST', body: JSON.stringify({ parcels: preview.map(row => row.payload) }) });
      modal.info({ title: '服务端入队结果', content: <div><p>成功入队：{result.acceptedCount} 条</p><p>拒绝：{result.rejectedCount} 条</p><p>当前队列深度：{result.queueDepth} 条</p><p>{result.message}</p><p>入队不等于数据库写入完成，可返回台账查询最终记录。</p></div> });
      // 部分接收后重发整批可能重复入队，要求核对服务端结果后重新准备批次。
      setValidated(false);
    } catch (error) { message.error(error instanceof Error ? error.message : '提交失败'); }
    finally { setSaving(false); }
  };
  return <>
    <PageIntro title="批量入队" />
    <InfoAlert message="最多 1000 条；入队不等于数据库写入完成。" />
    <SectionCard className="batch-input-card">
      <Tabs activeKey={tab} onChange={value => { setTab(value); setValidated(false); }} items={[{ key: 'json', label: '粘贴 JSON' }, { key: 'csv', label: '本地 CSV' }]} />
      {tab === 'csv' && <Upload accept=".csv,text/csv" showUploadList={false} beforeUpload={file => { file.text().then(value => { setRaw(value); setValidated(false); }).catch(() => message.error('文件读取失败')); return false; }}><Button icon={<DownloadOutlined />}>选择 CSV 文件</Button></Upload>}
      <Input.TextArea value={raw} onChange={event => { setRaw(event.target.value); setValidated(false); }} rows={7} placeholder={tab === 'json' ? '粘贴包裹数组 JSON' : '粘贴包裹 CSV 内容'} style={{ marginTop: tab === 'csv' ? 15 : 0 }} />
      <div className="batch-validate-action"><Button type="primary" onClick={parse}>校验数据</Button></div>
    </SectionCard>
    <SectionCard title="预览与校验结果" className="batch-preview-card">
      <DataTable dataSource={preview} rowKey="key" pagination={false} scroll={{ x: undefined }} locale={{ emptyText: '请导入待提交数据' }} columns={[
        { title: '包裹 ID', dataIndex: 'id', width: 200 }, { title: '主条码', dataIndex: 'barcode', width: 240 }, { title: '工作台', dataIndex: 'workstation', width: 160 },
        { title: '重量 (kg)', dataIndex: 'weight', width: 165, render: value => typeof value === 'number' && Number.isFinite(value) ? value.toFixed(2) : String(value ?? '未提供') }, { title: '目标格口', dataIndex: 'target', width: 195 },
        { title: '校验状态', width: 163, render: (_, row) => row.error ? <Typography.Text type="danger">{row.error}</Typography.Text> : <StatusTag value="校验通过" /> },
      ]} />
      <div className="batch-submit-action"><span>待提交 <b className="batch-pending-count">{preview.length}</b> 条，错误 <b className="batch-error-count">{preview.filter(item => item.error).length}</b> 条</span><Space><Button onClick={() => { setRaw(''); setPreview([]); setValidated(false); }}>取消</Button><Button type="primary" loading={saving} disabled={!validated || preview.some(item => !!item.error)} onClick={submit}>提交入队</Button></Space></div>
    </SectionCard>
  </>;
}
