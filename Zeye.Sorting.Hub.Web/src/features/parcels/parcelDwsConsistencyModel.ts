import { analysisDateOffset, validAnalysisDate } from './parcelAnalysisModel.ts';
import type { DwsMeasurementSample } from '../../data/api/parcelDwsConsistencyTypes.ts';

export const dwsThresholdKeys = ['weightToleranceGrams', 'weightTolerancePercent', 'volumeToleranceCm3', 'volumeTolerancePercent', 'scanDurationToleranceMilliseconds', 'scanDurationTolerancePercent'] as const;
export const dwsReferenceKeys = ['referenceWeightGrams', 'referenceVolumeCm3'] as const;
export interface DwsFilters {
  fromDate: string; toDate: string; barcode: string; sourceInstanceId: string; workstationName: string;
  weightToleranceGrams: string; weightTolerancePercent: string; volumeToleranceCm3: string; volumeTolerancePercent: string;
  scanDurationToleranceMilliseconds: string; scanDurationTolerancePercent: string;
  referenceWeightGrams: string; referenceVolumeCm3: string; onlyDeviations: boolean; sortBy: string; pageNumber: string;
  detailBarcode: string; measurementPageNumber: string;
}
/** 过滤条件和所选条码保留在URL，刷新或回退可恢复同一比较范围。 */
export function readDwsFilters(params: URLSearchParams, today: string): DwsFilters {
  return { fromDate: params.get('fromDate') ?? analysisDateOffset(today, -6), toDate: params.get('toDate') ?? today,
    barcode: params.get('barcode') ?? '', sourceInstanceId: params.get('sourceInstanceId') ?? '', workstationName: params.get('workstationName') ?? '',
    weightToleranceGrams: params.get('weightToleranceGrams') ?? '20', weightTolerancePercent: params.get('weightTolerancePercent') ?? '2',
    volumeToleranceCm3: params.get('volumeToleranceCm3') ?? '100', volumeTolerancePercent: params.get('volumeTolerancePercent') ?? '3',
    scanDurationToleranceMilliseconds: params.get('scanDurationToleranceMilliseconds') ?? '50', scanDurationTolerancePercent: params.get('scanDurationTolerancePercent') ?? '20',
    referenceWeightGrams: params.get('referenceWeightGrams') ?? '', referenceVolumeCm3: params.get('referenceVolumeCm3') ?? '',
    onlyDeviations: params.get('onlyDeviations') === 'true', sortBy: params.get('sortBy') ?? 'weight', pageNumber: params.get('pageNumber') ?? '1',
    detailBarcode: params.get('detailBarcode') ?? '', measurementPageNumber: params.get('measurementPageNumber') ?? '1' };
}
/** 不允许负值、非有限值、超长内容及跨超过31天的查询。 */
export function dwsValidation(filters: DwsFilters): string | null {
  if (!validAnalysisDate(filters.fromDate) || !validAnalysisDate(filters.toDate)) return '请选择有效的本地日期。';
  const days = (Date.parse(filters.toDate) - Date.parse(filters.fromDate)) / 86400000;
  if (days < 0 || days > 30) return '日期范围最多 31 天，结束日期不能早于开始日期。';
  if ([filters.barcode, filters.detailBarcode].some(value => value.length > 1024) || filters.sourceInstanceId.length > 96 || filters.workstationName.length > 128
    || [filters.barcode, filters.detailBarcode, filters.sourceInstanceId, filters.workstationName].some(value => /[\x00-\x1f\x7f]/.test(value))) return '条码或来源筛选内容无效。';
  const validNumber = (value: string) => /^\d+(?:\.\d+)?$/.test(value) && Number.isFinite(Number(value)) && Number(value) <= 1e9;
  if (dwsThresholdKeys.some(key => !validNumber(filters[key]))) return '偏差阈值必须为非负数值。';
  if (dwsReferenceKeys.some(key => filters[key] && (!validNumber(filters[key]) || Number(filters[key]) <= 0))) return '标准参考值必须大于 0。';
  if (dwsReferenceKeys.some(key => filters[key]) && !filters.barcode.trim()) return '填写标准参考值前，请指定精确条码。';
  if (![filters.pageNumber, filters.measurementPageNumber].every(value => /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= 100000)
    || !['weight', 'volume', 'scan-duration', 'scan-duration-p95', 'count'].includes(filters.sortBy)) return '页码或排序条件无效。';
  return null;
}
/** 查询只发送明确字段，不将空参考值转换为零。 */
export function dwsQueryParams(filters: DwsFilters): URLSearchParams {
  const query = new URLSearchParams({ fromDate: filters.fromDate, toDate: filters.toDate, sortBy: filters.sortBy,
    pageNumber: filters.pageNumber, measurementPageNumber: filters.measurementPageNumber });
  for (const key of ['barcode', 'sourceInstanceId', 'workstationName', 'detailBarcode', ...dwsThresholdKeys, ...dwsReferenceKeys] as const)
    if (filters[key].trim()) query.set(key, filters[key].trim());
  if (filters.onlyDeviations) query.set('onlyDeviations', 'true');
  return query;
}
export function dwsApiPath(filters: DwsFilters): string { return `/api/parcels/dws-consistency?${dwsQueryParams(filters)}`; }
const measurementNumber = new Intl.NumberFormat('zh-CN', { useGrouping: true, maximumFractionDigits: 6 });
/** 量测差保留小数精度，避免毫克或细小体积差被显示为零。 */
export const dwsValue = (value: number | null | undefined, unit = '') => value == null || !Number.isFinite(value) ? '—' : `${measurementNumber.format(value)}${unit ? ` ${unit}` : ''}`;
export const dwsPercent = (value: number | null | undefined) => dwsValue(value, '%');

export type DwsPlotMetric = 'weightGrams' | 'volumeCm3' | 'scanDurationMilliseconds';
/** 扫码趋势使用来源条码接收时间，重量体积使用设备测量时间；未知不补零。 */
export function dwsPlotSamples(samples: DwsMeasurementSample[], metric: DwsPlotMetric) {
  return samples.map(sample => ({ sample, timestamp: metric === 'scanDurationMilliseconds' ? sample.scanCompletedAt : sample.measuredAt }))
    .filter(point => point.timestamp && Number.isFinite(Date.parse(point.timestamp)) && point.sample[metric] != null && Number.isFinite(point.sample[metric]) && point.sample[metric]! >= 0)
    .map(point => ({ ...point, at: Date.parse(point.timestamp!), value: point.sample[metric]! })).sort((left, right) => left.at - right.at);
}
/** 时间缺失原因使用业务文字，原始测量仍可查看。 */
export function dwsScanReason(reason: string | null | undefined): string {
  const reasons: Record<string, string> = { 'missing-scan-result': '缺少有效条码接收记录', 'missing-parcel-identity': '包裹关联不完整',
    'missing-detection': '缺少检测记录', 'conflicting-parcel-identity': '来源包裹关联存在冲突', 'conflicting-detection-time': '检测时间存在冲突', 'conflicting-scan-time': '条码接收时间存在冲突',
    'unreliable-detection-time': '检测时间不可靠', 'unreliable-scan-time': '条码接收时间不可靠', 'invalid-scan-time': '条码接收时间无效', 'reversed-scan-time': '条码接收早于检测时间' };
  return reason && Object.hasOwn(reasons, reason) ? reasons[reason] : '未取得有效扫码时间';
}
