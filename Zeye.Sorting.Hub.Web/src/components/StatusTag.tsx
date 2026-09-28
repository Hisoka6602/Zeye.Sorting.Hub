import { Tag } from 'antd';

export function StatusTag({ value, tone }: { value: string | number; tone?: 'green' | 'red' | 'blue' | 'orange' }) {
  const label = String(value);
  const green = ['已完成', '成功', '正常', '就绪', 'Healthy', '已发布', '已处理', '已连接', '设备恢复', '200', '校验通过', '分拣完成'].includes(label);
  const red = ['失败', '发送失败', '死信', '分拣异常', '500', '400', '异常', '包裹卡滞', '设备离线', '识别异常', '传感器异常'].includes(label);
  const orange = ['待处理', '待审核', '警告', '包裹异常'].includes(label);
  return <Tag className={`status-tag status-${tone || (green ? 'green' : red ? 'red' : orange ? 'orange' : 'blue')}`}>{label}</Tag>;
}
