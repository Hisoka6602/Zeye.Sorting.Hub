import { Alert, Button, Drawer, Empty, Spin, Typography } from 'antd';
import { useState } from 'react';
import { useApiResource } from '../../data/api/useApiResource';
import type { ParcelImages, ParcelSummary } from '../../data/api/parcelTypes';
import { ParcelImageGallery } from './ParcelImageGallery';
import './parcelImages.css';

/** 不预先逐行读取详情；关闭或切换包裹时取消旧图片查询。 */
export function ParcelImagesDrawer({ parcel, onClose }: { parcel: ParcelSummary; onClose: () => void }) {
  const resource = useApiResource<ParcelImages>(`/api/parcels/${encodeURIComponent(parcel.id)}/images`);
  const [selectedSource, setSelectedSource] = useState<string>();
  const images = resource.data?.images ?? [];
  return <Drawer title="包裹图片" open onClose={onClose} width="min(640px, 100vw)" className="parcel-images-drawer"
    footer={<Button onClick={resource.refresh} loading={resource.loading}>重新加载</Button>}>
    <div className="parcel-images-heading"><Typography.Text strong>{parcel.barCodes || '未识别条码'}</Typography.Text><Typography.Text type="secondary">包裹 ID：{parcel.id}</Typography.Text></div>
    {resource.loading ? <div className="parcel-images-loading"><Spin aria-label="正在读取包裹图片" /></div>
      : resource.error ? <Alert showIcon type="error" message="图片信息加载失败" description={resource.error.message} action={<Button onClick={resource.refresh}>重试</Button>} />
        : images.length ? <ParcelImageGallery images={images} initialSourcePath={selectedSource} onSelect={image => setSelectedSource(image.sourcePath)} />
          : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={resource.data?.hasImages ? '包裹已标记有图片，尚未收到图片记录。' : '该包裹暂无图片。'} />}
  </Drawer>;
}
