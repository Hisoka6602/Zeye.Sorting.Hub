import { formatNumber } from '../../data/formatNumber';
import { PictureOutlined } from '@ant-design/icons';
import { Button, Collapse, Drawer, Empty, Skeleton, Space, Timeline, Typography } from 'antd';
import { useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { ApiError } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import type { ParcelDetail, ParcelProcessingRecord } from '../../data/api/parcelTypes';
import { ParcelFacts, parcelFactValue } from './ParcelFacts';
import { ParcelExceptionPanel } from './ParcelExceptionPanel';
import { ParcelImagesDrawer } from './ParcelImagesDrawer';
import { buildParcelProcessingTimeline, type ParcelProcessingEvent } from './parcelProcessingTimeline';
import './parcelProcessing.css';

/** Every existing value object remains available below the reference-sized summary. */
const detailGroups = [
  ['barCodeInfos', '条码明细'], ['weightInfos', '称重明细'], ['volumeInfo', '体积信息'],
  ['chuteInfo', '格口信息'], ['apiRequests', '外部接口'], ['commandInfos', '通信指令'],
  ['imageInfos', '图片信息'], ['videoInfos', '视频信息'], ['sorterCarrierInfo', '小车信息'],
  ['bagInfo', '集包信息'], ['deviceInfo', '设备信息'], ['grayDetectorInfo', '灰度检测'],
  ['stickingParcelInfo', '叠包检测'], ['parcelPositionInfo', '坐标信息'],
] as const;

const display = (key: string, value: unknown, unavailable: boolean) => unavailable ? '—' : parcelFactValue(key, value);
const dimensions = (parcel: ParcelDetail | undefined) => parcel && [parcel.length, parcel.width, parcel.height].every(value => value != null)
  ? `${parcel.length} × ${parcel.width} × ${parcel.height} mm（毫米）` : parcel ? '未提供' : '—';

/** Render the same page structure for data, loading, and API failure states. */
export function ParcelDetailPage() {
  const designPreview = import.meta.env.MODE === 'design-preview';
  const { id } = useParams();
  const { data: parcel, loading, error, refresh } = useApiResource<ParcelDetail>(id ? `/api/parcels/${encodeURIComponent(id)}` : null);
  const [selectedEvent, setSelectedEvent] = useState<ParcelProcessingEvent | null>(null);
  const [imagesOpen, setImagesOpen] = useState(false);
  const history = useMemo(() => buildParcelProcessingTimeline(parcel?.processingRecords ?? []), [parcel?.processingRecords]);
  const notFound = !loading && ((error instanceof ApiError && error.status === 404) || (!error && !parcel));
  const unavailable = !parcel;
  const status = parcel ? parcelFactValue('status', parcel.status) : undefined;
  const previewText = (record: ParcelProcessingRecord, key: string) => designPreview ? (record as unknown as Record<string, string>)[key] : undefined;
  const copyable = (value: unknown) => parcel && value != null && value !== '' ? { text: String(value) } : false;
  const summary = <div className="detail-grid">
    <div>
      <div className="detail-pair"><span className="detail-label">包裹 ID</span><span className="detail-copy"><Typography.Text copyable={copyable(parcel?.id)}>{display('id', parcel?.id, unavailable)}</Typography.Text></span></div>
      <div className="detail-pair"><span className="detail-label">主条码</span><span className="detail-copy"><Typography.Text copyable={copyable(parcel?.barCodes)}>{display('barCodes', parcel?.barCodes, unavailable)}</Typography.Text></span></div>
      <div className="detail-pair"><span className="detail-label">袋号</span><span className="detail-copy"><Typography.Text copyable={copyable(parcel?.bagCode)}>{display('bagCode', parcel?.bagCode, unavailable)}</Typography.Text></span></div>
      <div className="detail-pair"><span className="detail-label">状态</span>{status ? <StatusTag value={status} /> : <span className="detail-value">—</span>}</div>
    </div>
    <div>
      <div className="detail-pair"><span className="detail-label">目标格口</span><span className="detail-value">{display('targetChuteCode', parcel?.targetChuteCode ?? parcel?.targetChuteId, unavailable)}</span></div>
      <div className="detail-pair"><span className="detail-label">来源工作台</span><span className="detail-value">{display('workstationName', parcel?.workstationName, unavailable)}</span></div>
      <div className="detail-pair"><span className="detail-label">重量</span><span className="detail-value">{parcel?.weight != null ? `${formatNumber(parcel.weight)} kg` : parcel ? '未提供' : '—'}</span></div>
      <div className="detail-pair"><span className="detail-label">物理尺寸</span><span className="detail-value">{dimensions(parcel)}</span></div>
      <div className="detail-pair"><span className="detail-label">创建时间</span><span className="detail-value">{display('createdTime', parcel?.createdTime, unavailable)}</span></div>
    </div>
  </div>;

  return <div className="parcel-detail-page">
    <PageIntro title="包裹详情" description="查看包裹的详细信息、处理轨迹及相关记录。" action={parcel?.hasImages && <Button icon={<PictureOutlined />} onClick={() => setImagesOpen(true)}>查看图片</Button>} />
    <SectionCard title="基本信息" className="parcel-detail-basic" extra={error && !notFound ? <Space><Typography.Text type="danger">详情加载失败：{error.message}</Typography.Text><Button onClick={refresh}>重试</Button></Space> : status && <StatusTag value={status} />}>
      {notFound ? <Empty description="未找到该包裹" /> : loading ? <Skeleton active paragraph={{ rows: 5 }} /> : summary}
    </SectionCard>
    {!notFound && <div className="two-cols parcel-detail-panels">
      <SectionCard title="处理轨迹">
        {loading ? <Skeleton active paragraph={{ rows: 6 }} /> : history.items.length ? <Timeline className="parcel-processing-timeline" items={history.items.map(item => ({
          color: item.color,
          children: <>
            <div className="parcel-processing-heading"><b>{previewText(item.events[0].record, 'previewTimelineTitle') ?? item.title}</b><time className="text-muted">{parcelFactValue('occurredAt', item.occurredAt)}</time></div>
            {(item.state || item.attemptNumber != null) && <div className="parcel-processing-summary">{item.state && <StatusTag value={item.state} tone={item.color} />}{item.attemptNumber != null && <span className="text-muted">第 {item.attemptNumber} 次尝试</span>}</div>}
            {(designPreview || item.description !== item.state) && <div className="text-muted parcel-processing-description">{previewText(item.events[0].record, 'previewDescription') ?? item.description}</div>}
            {item.events.length > 1 && <Collapse ghost size="small" items={[{ key: item.key, label: `查看调用记录（${item.events.length}）`, children: item.events.map(event => <div className="parcel-processing-event" key={event.record.recordId}>
              <time>{parcelFactValue('occurredAt', event.record.occurredAt)}</time><StatusTag value={event.state || event.title} tone={event.color} /><Button type="link" className="table-link" onClick={() => setSelectedEvent(event)}>详情</Button>
            </div>) }]} />}
          </>,
        }))} /> : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={error ? '重试后显示处理轨迹' : '暂无处理事实记录'} />}
      </SectionCard>
      <SectionCard title="相关记录">
        <DataTable<ParcelProcessingEvent> className="parcel-related-records" rowKey={event => event.record.recordId} dataSource={history.events} loading={loading} tableLayout="fixed" scroll={{ x: 900 }} columns={[
          { title: '时间', width: 230, render: (_, event) => parcelFactValue('occurredAt', event.record.occurredAt) },
          { title: '类型', width: 126, render: (_, event) => <StatusTag value={previewText(event.record, 'previewType') ?? event.title} /> },
          { title: '关联编号', width: 140, render: (_, { record }) => previewText(record, 'previewReference') ?? record.actualChuteCode ?? record.targetChuteCode ?? record.sourceParcelId ?? record.recordId },
          { title: '内容', render: (_, event) => previewText(event.record, 'previewContent') ?? event.description },
          { title: '操作', width: 80, render: (_, event) => <Button type="link" className="table-link" onClick={() => setSelectedEvent(event)}>查看</Button> },
        ]} locale={{ emptyText: error ? '重试后显示相关记录' : '暂无处理记录' }} />
      </SectionCard>
    </div>}
    <ParcelExceptionPanel parcel={parcel} events={history.events} />
    {parcel && <SectionCard title="完整合同字段" className="parcel-detail-complete">
      <ParcelFacts facts={parcel} keys={Object.keys(parcel).filter(key => key !== 'processingRecords' && !detailGroups.some(([group]) => group === key))} />
      <Collapse items={detailGroups.map(([key, title]) => {
        const value = parcel[key];
        const rows = Array.isArray(value) ? value : value ? [value] : [];
        return { key, label: `${title}（${rows.length}）`, children: rows.length ? rows.map((row, index) => <ParcelFacts key={index} facts={row} />) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={`暂无${title}`} /> };
      })} />
    </SectionCard>}
    <Drawer title="处理记录详情" width="min(960px, 100vw)" open={!!selectedEvent} onClose={() => setSelectedEvent(null)}>
      {/* 仅修正展示副本的业务名称与真实尝试数，原始报文及其他合同字段保持完整。 */}
      {selectedEvent && <ParcelFacts facts={{ ...selectedEvent.record, stage: selectedEvent.title, attemptNumber: selectedEvent.attemptNumber }} />}
    </Drawer>
    {parcel && imagesOpen && <ParcelImagesDrawer key={parcel.id} parcel={parcel} onClose={() => setImagesOpen(false)} />}
  </div>;
}
