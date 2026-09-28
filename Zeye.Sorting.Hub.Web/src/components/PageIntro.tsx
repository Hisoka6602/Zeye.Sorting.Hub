import { Button, Space, Tag } from 'antd';
import { LeftOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router';
import type { CSSProperties, ReactNode } from 'react';
import { titleTypographyForPath } from '../app/titleTypography';
import { contentTypographyForPath } from '../app/contentTypography';

export function PageIntro({ title, description, planned, action, back }: { title: string; description?: string; planned?: boolean; action?: ReactNode; back?: string }) {
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const { fontSize, letterSpacing, lineHeight, ...captionTypeface } = contentTypographyForPath(pathname, 'intro') ?? {};
  const captionStyle = {
    ...captionTypeface,
    ...(fontSize !== undefined && { '--caption-font-size': typeof fontSize === 'number' ? `${fontSize}px` : fontSize }),
    ...(letterSpacing !== undefined && { '--caption-tracking': typeof letterSpacing === 'number' ? `${letterSpacing}px` : letterSpacing }),
    ...(lineHeight !== undefined && { '--caption-line-height': typeof lineHeight === 'number' ? `${lineHeight}px` : lineHeight }),
  } as CSSProperties;
  return <div className="page-intro">
    <div className="page-intro-copy">
      <div className="page-title-line"><h1 style={titleTypographyForPath(pathname)}>{title}</h1>{planned && <Tag className="planned-tag">规划稿</Tag>}</div>
      {description && <p className={fontSize !== undefined ? 'calibrated-caption' : undefined} style={captionStyle}>{description}</p>}
    </div>
    <Space>{back && <Button icon={<LeftOutlined />} onClick={() => navigate(back)}>返回</Button>}{action}</Space>
  </div>;
}
