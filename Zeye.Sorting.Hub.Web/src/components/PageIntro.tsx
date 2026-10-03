import { Button, Space, Tag } from 'antd';
import { LeftOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router';
import type { ReactNode } from 'react';

export function PageIntro({ title, description, planned, action, back }: { title: string; description?: string; planned?: boolean; action?: ReactNode; back?: string }) {
  const navigate = useNavigate();
  return <div className="page-intro">
    <div className="page-intro-copy">
      <div className="page-title-line"><h1>{title}</h1>{planned && <Tag className="planned-tag">规划稿</Tag>}</div>
      {description && <p>{description}</p>}
    </div>
    <Space>{back && <Button icon={<LeftOutlined />} onClick={() => navigate(back)}>返回</Button>}{action}</Space>
  </div>;
}
