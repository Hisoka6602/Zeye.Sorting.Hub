import { formatNumber } from '../../data/formatNumber';
import { Alert, Button, DatePicker, Drawer, Form, Input, Select, Space, Tabs } from 'antd';
import { DownOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { Field } from '../../components/Field';
import { FilterActions } from '../../components/FilterActions';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { useApiResource } from '../../data/api/useApiResource';
import { processingStages, type ParcelList, type ParcelProcessingRecord, type ParcelSummary } from '../../data/api/parcelTypes';
import { ParcelFacts, parcelFactValue } from './ParcelFacts';
import { ParcelImagesDrawer } from './ParcelImagesDrawer';

/** 包裹台账使用后端分页；展开行显示完整摘要，未关联DWS单独检索。 */
export function ParcelListPage() {
  const designPreview = import.meta.env.MODE === 'design-preview';
  const defaultDay = designPreview ? dayjs('2026-09-25') : dayjs();
  const navigate = useNavigate();
  const [barcode, setBarcode] = useState('');
  const [status, setStatus] = useState<number>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>([defaultDay.startOf('day'), defaultDay.endOf('day')]);
  const [moreOpen, setMoreOpen] = useState(false);
  const [bag, setBag] = useState('');
  const [workstation, setWorkstation] = useState('');
  const [applied, setApplied] = useState<Record<string, string>>({ scannedTimeStart: defaultDay.startOf('day').format('YYYY-MM-DDTHH:mm:ss'), scannedTimeEnd: defaultDay.endOf('day').format('YYYY-MM-DDTHH:mm:ss') });
  const [page, setPage] = useState({ number: 1, size: 10 });
  const [tab, setTab] = useState('parcels');
  const [imageParcel, setImageParcel] = useState<ParcelSummary>();
  const query = new URLSearchParams({ ...applied, pageNumber: String(page.number), pageSize: String(page.size), includeTotalCount: 'true' });
  const parcels = useApiResource<ParcelList>(`/api/parcels?${query}`);
  const unbound = useApiResource<ParcelProcessingRecord[]>(tab === 'unbound' ? '/api/parcels/processing-records/unbound?limit=200' : null);
  const search = () => {
    const filters: Record<string, string> = {};
    if (barcode.trim()) filters.barCodeKeyword = barcode.trim();
    if (status !== undefined) filters.status = String(status);
    if (bag.trim()) filters.bagCode = bag.trim();
    if (workstation.trim()) filters.workstationName = workstation.trim();
    if (range?.[0]) filters.scannedTimeStart = range[0].startOf('day').format('YYYY-MM-DDTHH:mm:ss');
    if (range?.[1]) filters.scannedTimeEnd = range[1].endOf('day').format('YYYY-MM-DDTHH:mm:ss');
    setApplied(filters); setPage(current => ({ ...current, number: 1 })); parcels.refresh();
  };
  const reset = () => {
    setBarcode(''); setStatus(undefined); setBag(''); setWorkstation('');
    setRange([defaultDay.startOf('day'), defaultDay.endOf('day')]);
    setApplied({ scannedTimeStart: defaultDay.startOf('day').format('YYYY-MM-DDTHH:mm:ss'), scannedTimeEnd: defaultDay.endOf('day').format('YYYY-MM-DDTHH:mm:ss') });
    setPage(current => ({ ...current, number: 1 })); parcels.refresh();
  };
  return <>
    <PageIntro title="包裹台账" description="按条码、时间和状态定位包裹，业务数据由工作台或融合服务自动传入。" />
    <SectionCard className="filter-card parcel-list-filter">
      <div className="filter-grid">
        <Field label="条码"><Input value={barcode} onChange={event => setBarcode(event.target.value)} onPressEnter={search} placeholder="请输入条码" /></Field>
        <Field label="扫码时间" wide><DatePicker.RangePicker separator="~" value={range} onChange={value => setRange(value)} style={{ width: '100%' }} /></Field>
        <Field label="状态"><Select allowClear value={status} onChange={setStatus} placeholder="请选择状态" options={['待分拣', '已完成', '分拣异常'].map((label, value) => ({ label, value }))} /></Field>
        <FilterActions onSearch={search} onReset={reset} extra={<Button onClick={() => setMoreOpen(true)}>更多筛选 <DownOutlined /></Button>} />
      </div>
    </SectionCard>
    <SectionCard className="table-card parcel-list-table">
      {!designPreview && <Tabs activeKey={tab} onChange={setTab} items={[{ key: 'parcels', label: '包裹记录' }, { key: 'unbound', label: '未关联 DWS' }]} />}
      {tab === 'parcels' ? <>
        {parcels.error && <Alert showIcon type="error" message="包裹台账加载失败" description={parcels.error.message} action={<Button onClick={parcels.refresh}>重试</Button>} />}
        <DataTable<ParcelSummary> className="parcel-records-table" dataSource={parcels.data?.items ?? []} loading={parcels.loading} rowKey="id" tableLayout="fixed" scroll={{ x: 1320 }} columns={[
          { title: '扫码时间', dataIndex: 'scannedTime', render: value => parcelFactValue('scannedTime', value), width: 180, ellipsis: true },
          { title: '包裹 ID', dataIndex: 'id', width: 195 },
          { title: '主条码', dataIndex: 'barCodes', render: value => parcelFactValue('barCodes', value), width: 186 },
          { title: '图片', dataIndex: 'hasImages', width: 116, render: (value, record) => value ? <Button className="table-link" type="link" aria-label={`查看包裹 ${record.id} 的图片`} onClick={event => { event.stopPropagation(); setImageParcel(record); }} onDoubleClick={event => event.stopPropagation()}>查看图片</Button> : <span className="parcel-image-empty">暂无图片</span> },
          { title: '状态', dataIndex: 'status', render: value => <StatusTag value={parcelFactValue('status', value)} />, width: 117 },
          { title: '目标 / 实际格口', render: (_, record) => `${record.targetChuteCode ?? record.targetChuteId ?? (designPreview ? '-' : '未提供')} / ${record.actualChuteCode ?? record.actualChuteId ?? (designPreview ? '-' : '未提供')}`, width: 185 },
          { title: '工作台', dataIndex: 'workstationName', width: 126 },
          { title: '重量', dataIndex: 'weight', render: value => value == null ? '未提供' : `${formatNumber(Number(value))} kg`, width: 118 },
          { title: '操作', width: 96, render: (_, record) => <Button className="table-link" type="link" onClick={() => navigate(`/parcels/${record.id}`)}>查看</Button> },
        ]} pagination={{ current: page.number, pageSize: page.size, total: parcels.data?.totalCount ?? 0, onChange: (number, size) => setPage({ number, size }), pageSizeOptions: [10, 20, 50, 100, 200] }} onRow={record => ({ onDoubleClick: () => navigate(`/parcels/${record.id}`) })} locale={{ emptyText: parcels.error ? '数据未加载，请重试' : '当前筛选下没有包裹' }} />
      </> : <>
        {unbound.error && <Alert type="error" showIcon message="未关联 DWS 加载失败" description={unbound.error.message} />}
        <DataTable<ParcelProcessingRecord> rowKey={record => `${record.sourceInstanceId}/${record.sourceRunId}/${record.recordId}`} loading={unbound.loading} dataSource={unbound.data ?? []} columns={[
          { title: '入库时间', dataIndex: 'recordedAt', render: value => parcelFactValue('recordedAt', value), width: 210 },
          { title: '来源实例', dataIndex: 'sourceInstanceId', width: 180 },
          { title: '消息标识', dataIndex: 'messageIdentity', render: value => parcelFactValue('messageIdentity', value) },
          { title: '处理阶段', dataIndex: 'stage', render: value => processingStages[value], width: 140 },
          { title: '拒绝 / 未关联原因', render: (_, record) => record.decisionReason ?? record.errorMessage ?? '未提供' },
        ]} expandable={{ expandedRowRender: record => <ParcelFacts facts={record} /> }} locale={{ emptyText: '暂无未关联 DWS 记录' }} />
      </>}
    </SectionCard>
    <Drawer title="更多筛选" open={moreOpen} onClose={() => setMoreOpen(false)} width={380} footer={<Space><Button onClick={() => { setBag(''); setWorkstation(''); }}>清空</Button><Button type="primary" onClick={() => { search(); setMoreOpen(false); }}>应用筛选</Button></Space>}>
      <Form layout="vertical"><Form.Item label="集包号"><Input value={bag} onChange={event => setBag(event.target.value)} placeholder="请输入集包号" /></Form.Item><Form.Item label="工作台"><Input value={workstation} onChange={event => setWorkstation(event.target.value)} placeholder="请输入工作台名称" /></Form.Item></Form>
    </Drawer>
    {imageParcel && <ParcelImagesDrawer key={imageParcel.id} parcel={imageParcel} onClose={() => setImageParcel(undefined)} />}
  </>;
}
