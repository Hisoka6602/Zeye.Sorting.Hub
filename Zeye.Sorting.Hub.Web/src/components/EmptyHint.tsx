import { Typography } from 'antd';

export function EmptyHint({ text = '暂无数据' }: { text?: string }) {
  return <Typography.Text type="secondary">{text}</Typography.Text>;
}
