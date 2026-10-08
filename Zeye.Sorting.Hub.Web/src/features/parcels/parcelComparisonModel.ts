import type { ParcelComparisonItem } from '../../data/api/parcelComparisonTypes.ts';
import { timingOverview } from './parcelTimingPresentation.ts';
import { validTimingParcelId, timingInterval, type TimingRow } from './parcelTimingModel.ts';

export const comparisonLimit = 8;
export type ComparisonMeasure = 'weight' | 'length' | 'width' | 'height' | 'volume';
export const comparisonMeasures: { key: ComparisonMeasure; label: string; unit: string }[] = [
  { key: 'weight', label: '重量', unit: 'kg' }, { key: 'length', label: '长度', unit: 'mm' },
  { key: 'width', label: '宽度', unit: 'mm' }, { key: 'height', label: '高度', unit: 'mm' },
  { key: 'volume', label: '体积', unit: 'L' },
];

/** 中心ID始终按字符串处理；损坏链接整体报错，不能悄悄丢掉某票。 */
export function comparisonIds(raw: string | null): { ids: string[]; error: string | null } {
  if (!raw) return { ids: [], error: null };
  const ids = [...new Set(raw.split(',').map(id => id.trim()))];
  return ids.length > comparisonLimit || ids.some(id => !validTimingParcelId(id))
    ? { ids: [], error: '链接中应包含1至8个有效的包裹 ID，请清空后重新选择。' }
    : { ids, error: null };
}

/** 输入按行或逗号分隔，完整条码不会被空格拆散，单项沿用1024字符限制。 */
export function comparisonQueries(text: string): string[] {
  const queries = [...new Set(text.split(/[\r\n,，]+/).map(value => value.trim()).filter(Boolean))];
  if (!queries.length) throw new Error('请输入包裹 ID 或完整条码。');
  if (queries.length > comparisonLimit) throw new Error('每次最多输入8项，请减少输入后查询。');
  if (queries.some(value => value.length > 1024)) throw new Error('每个包裹 ID 或条码不能超过1024字符。');
  return queries;
}

/** 仅接受已保存、有限且非负的量测；真实零保留，体积由mm³换算为L。 */
export function comparisonValue(item: ParcelComparisonItem, measure: ComparisonMeasure): number | null {
  const value = item[measure];
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? measure === 'volume' ? value / 1_000_000 : value : null;
}

/** 格式精度允许辨别克级重量和微小体积，不把已记录的小数显示为零。 */
export function formatComparisonValue(value: number | null): string {
  if (value === null || !Number.isFinite(value)) return '—';
  if (value !== 0 && Math.abs(value) < 0.000001) return value.toExponential(3);
  return value.toLocaleString('zh-CN', { maximumFractionDigits: 6 });
}

/** 差值不把缺失基准当零，百分比在基准为0时不计算。 */
export function comparisonDelta(value: number | null, baseline: number | null): { difference: number | null; percent: number | null } {
  if (value === null || baseline === null) return { difference: null, percent: null };
  const difference = value - baseline;
  return { difference, percent: baseline === 0 ? null : difference / baseline * 100 };
}

/** 固定阶段只使用明确的真实节点；接口从请求至响应，未提供或声明不可靠时保留空值。 */
export function comparisonIntervals(row: TimingRow): { label: string; value: number | null }[] {
  const overview = timingOverview(row);
  const reliable = (mark: typeof overview.scan) => mark && !/不可靠|无效|迟到/.test(mark.warning);
  const gap = (a: typeof overview.scan, b: typeof overview.scan) => a && b && reliable(a) && reliable(b) ? timingInterval(a.start, b.start) : null;
  const request = overview.request;
  const landing = row.marks.find(mark => mark.title === '实际落格') ?? null;
  return [
    { label: '检测 → 扫码', value: gap(overview.detected, overview.scan) },
    { label: '扫码 → 首次格口请求', value: gap(overview.scan, request) },
    { label: '首次格口请求 → 响应', value: request && request.end !== null && reliable(request) ? timingInterval(request.start, request.end) : null },
    { label: '检测 → 实际落格', value: gap(overview.detected, landing) },
  ];
}
