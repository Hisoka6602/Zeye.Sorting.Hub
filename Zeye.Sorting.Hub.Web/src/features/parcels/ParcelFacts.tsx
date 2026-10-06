import { Typography } from 'antd';
import { parcelFieldLabels, processingStages } from '../../data/api/parcelTypes';
import { formatNumber } from '../../data/formatNumber';

/** 按后端字段语义格式化真实值，未知值与零保持区别。 */
export function parcelFactValue(key: string, value: unknown): string {
  if (value === null || value === undefined || value === '') return '未提供';
  if (typeof value === 'boolean') return value ? '是' : '否';
  if (key === 'stage' && typeof value === 'number') return processingStages[value] ?? String(value);
  if (key === 'status' && typeof value === 'number') return ['待分拣', '已完成', '分拣异常'][value] ?? String(value);
  if (key === 'requestStatus' && typeof value === 'number') return ['未访问', '成功', '失败'][value] ?? String(value);
  if (key === 'type' && typeof value === 'number') return ['普通包裹', '大型包裹', '聚合包裹', '超薄包裹', '异形件', '流体包裹', '易碎品'][value] ?? String(value);
  if (key === 'noReadType' && typeof value === 'number') return ['未分类', '画面无包裹', '无面单', '面单模糊', '面单褶皱', '条码截断', '反光', '光线不足', '条码污损', '对焦模糊'][value] ?? String(value);
  if (typeof value === 'number') return formatNumber(value);
  if (typeof value === 'object') return JSON.stringify(value, null, 2);
  return String(value).replace(/^(\d{4}-\d{2}-\d{2})T/, '$1 ');
}

/** 按实际容器宽度展示合同字段，长正文独占整行并保留全文复制。 */
export function ParcelFacts({ facts, keys, labels }: { facts: object; keys?: string[]; labels?: Record<string, string> }) {
  const values = facts as Record<string, unknown>;
  const entries = (keys ?? Object.keys(values)).map(key => [key, values[key]] as const);
  return <dl className="parcel-facts">{entries.map(([key, value]) => <div key={key} className={/Payload|Body|Headers|Json|Data|Message|Exception|Path|Reason/i.test(key) ? 'parcel-fact parcel-fact-full' : 'parcel-fact'}>
    <dt>{labels?.[key] ?? parcelFieldLabels[key] ?? key}</dt>
    <dd><Typography.Text copyable={value !== null && value !== undefined && value !== ''}>{parcelFactValue(key, value)}</Typography.Text></dd>
  </div>)}</dl>;
}
