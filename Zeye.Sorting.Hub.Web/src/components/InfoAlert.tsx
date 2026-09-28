import { Alert } from 'antd';
import type { ReactNode } from 'react';

export function InfoAlert({ message, description, type = 'info', closable = true }: { message: ReactNode; description?: ReactNode; type?: 'info' | 'warning' | 'success' | 'error'; closable?: boolean }) {
  return <Alert className="page-alert" message={message} description={description} type={type} showIcon closable={closable} />;
}
