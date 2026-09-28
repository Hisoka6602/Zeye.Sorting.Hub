import { Card } from 'antd';
import type { ReactNode } from 'react';
import { useLocation } from 'react-router';
import { contentTypographyForPath } from '../app/contentTypography';

export function SectionCard({ title, extra, children, className = '' }: { title?: ReactNode; extra?: ReactNode; children: ReactNode; className?: string }) {
  const { pathname } = useLocation();
  const typography = typeof title === 'string' ? contentTypographyForPath(pathname, 'section', title) : undefined;
  return <Card className={`surface-card ${className}`} title={title} extra={extra} styles={{ title: typography }}>{children}</Card>;
}
