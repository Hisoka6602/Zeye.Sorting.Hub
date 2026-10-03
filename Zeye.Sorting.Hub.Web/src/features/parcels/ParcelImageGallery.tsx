import { FullscreenOutlined, LeftOutlined, PictureOutlined, RightOutlined } from '@ant-design/icons';
import { Button, Empty, Image, Space, Typography } from 'antd';
import { useRef, useState, type KeyboardEvent } from 'react';
import type { ParcelImage } from '../../data/api/parcelTypes';
import './parcelImages.css';

const imageLabel = (image: ParcelImage, index: number) => image.cameraName.trim() || `图片 ${index + 1}`;

/** Keep source records selectable even when their preview URLs are unavailable. */
export function ParcelImageGallery({ images, initialSourcePath, onSelect }: {
  images: ParcelImage[];
  initialSourcePath?: string;
  onSelect?: (image: ParcelImage) => void;
}) {
  const [selected, setSelected] = useState(() => {
    const remembered = images.findIndex(image => image.sourcePath === initialSourcePath);
    return remembered >= 0 ? remembered : Math.max(0, images.findIndex(image => image.url));
  });
  const [failed, setFailed] = useState<ReadonlySet<number>>(() => new Set());
  const [previewOpen, setPreviewOpen] = useState(false);
  const thumbnails = useRef<(HTMLButtonElement | null)[]>([]);
  const current = Math.min(selected, Math.max(0, images.length - 1));
  const image = images[current];
  const previewImages = images.flatMap((item, index) => item.url && !failed.has(index) ? [{ image: item, index }] : []);
  const previewIndex = previewImages.findIndex(item => item.index === current);
  const canPreview = previewIndex >= 0;

  const select = (index: number) => {
    setSelected(index);
    onSelect?.(images[index]);
  };
  const markFailed = (index: number) => {
    setFailed(previous => previous.has(index) ? previous : new Set([...previous, index]));
    if (index === current) setPreviewOpen(false);
  };
  const retryImage = () => {
    setFailed(previous => {
      const next = new Set(previous);
      next.delete(current);
      return next;
    });
  };
  const navigateThumbnails = (event: KeyboardEvent<HTMLDivElement>) => {
    const next = event.key === 'ArrowRight' ? Math.min(current + 1, images.length - 1)
      : event.key === 'ArrowLeft' ? Math.max(current - 1, 0)
        : event.key === 'Home' ? 0 : event.key === 'End' ? images.length - 1 : null;
    if (next == null) return;
    event.preventDefault();
    select(next);
    thumbnails.current[next]?.focus();
  };

  if (!image) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="该包裹暂无图片。" />;
  const label = imageLabel(image, current);

  return <section className="parcel-image-gallery" aria-label="包裹图片浏览器" onKeyDownCapture={event => {
    // The preview portal must consume Escape before the underlying drawer does.
    if (previewOpen && event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      setPreviewOpen(false);
    }
  }}>
    <div className="parcel-gallery-toolbar">
      <Typography.Text type="secondary">共 {images.length} 张图片{previewImages.length < images.length && ` · ${previewImages.length} 张可预览`}</Typography.Text>
      <Space size={8}>
        {images.length > 1 && <>
          <Button icon={<LeftOutlined />} aria-label="上一张图片" disabled={current === 0} onClick={() => select(current - 1)} />
          <span className="parcel-gallery-position" aria-live="polite" aria-atomic="true">{current + 1} / {images.length}</span>
          <Button icon={<RightOutlined />} aria-label="下一张图片" disabled={current === images.length - 1} onClick={() => select(current + 1)} />
        </>}
        <Button icon={<FullscreenOutlined />} disabled={!canPreview} onClick={() => setPreviewOpen(true)}>放大查看</Button>
      </Space>
    </div>
    <div className="parcel-image-card">
      <div className="parcel-gallery-stage">
        {canPreview ? <button type="button" className="parcel-gallery-open" aria-label={`放大查看第 ${current + 1} 张图片，${label}`} onClick={() => setPreviewOpen(true)}>
          <img key={`${current}:${image.url}`} src={image.url!} alt={label} onError={() => markFailed(current)} />
          <span className="parcel-gallery-zoom"><FullscreenOutlined /> 放大查看</span>
        </button> : <div className="parcel-image-unavailable" role="status">
          <PictureOutlined className="parcel-gallery-unavailable-icon" />
          <Typography.Text strong>{failed.has(current) ? '图片加载失败' : '图片暂不可预览'}</Typography.Text>
          <Typography.Text type="secondary">{failed.has(current) ? '请重试或重新加载图片信息。' : image.unavailableReason || '尚未提供可访问的图片地址。'}</Typography.Text>
          {failed.has(current) && <Button onClick={retryImage}>重试当前图片</Button>}
        </div>}
      </div>
      <div className="parcel-image-caption" aria-live="polite">
        <Typography.Text strong>{label}</Typography.Text>
        <Typography.Text type="secondary" className="parcel-image-source-label">图片来源</Typography.Text>
        <Typography.Paragraph type="secondary" copyable={{ tooltips: ['复制路径', '已复制'] }}>{image.sourcePath}</Typography.Paragraph>
      </div>
    </div>
    {images.length > 1 && <div className="parcel-gallery-thumbnails" role="group" aria-label="选择包裹图片" onKeyDown={navigateThumbnails}>
      {images.map((item, index) => <button type="button" key={item.sourcePath} ref={element => { thumbnails.current[index] = element; }}
        className={`parcel-gallery-thumbnail${index === current ? ' is-selected' : ''}`} aria-pressed={index === current}
        aria-label={`查看第 ${index + 1} 张图片，${imageLabel(item, index)}`} tabIndex={index === current ? 0 : -1} onClick={() => select(index)}>
        <span className="parcel-gallery-thumbnail-media">
          {item.url && !failed.has(index) ? <img src={item.url} alt="" loading="lazy" onError={() => markFailed(index)} />
            : <span className="parcel-gallery-thumbnail-unavailable"><PictureOutlined />{failed.has(index) ? '加载失败' : '暂无预览'}</span>}
          <span className="parcel-gallery-thumbnail-number">{index + 1}</span>
        </span>
        <span className="parcel-gallery-thumbnail-label" title={imageLabel(item, index)}>{imageLabel(item, index)}</span>
      </button>)}
    </div>}
    <Image.PreviewGroup items={previewImages.map(item => ({ src: item.image.url!, alt: imageLabel(item.image, item.index) }))}
      preview={{
        visible: previewOpen && canPreview,
        current: Math.max(0, previewIndex),
        onVisibleChange: visible => setPreviewOpen(visible),
        onChange: index => { if (previewImages[index]) select(previewImages[index].index); },
        countRender: position => {
          const item = previewImages[position - 1];
          return item ? `${item.index + 1} / ${images.length} · ${imageLabel(item.image, item.index)}` : '';
        },
      }} />
  </section>;
}
