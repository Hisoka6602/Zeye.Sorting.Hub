import { formatNumber } from '../../data/formatNumber.ts';
import type { ParcelAnalysisView, ParcelChuteRoute, ParcelDurationBucket } from '../../data/api/parcelAnalysisTypes.ts';

export interface AnalysisFilters {
  fromDate: string; toDate: string; workstationName: string; sourceInstanceId: string; issue: string; exceptionType: string;
  durationType: string;
  minimumMilliseconds: string; maximumMilliseconds: string; mismatchOnly: boolean; fallbackOnly: boolean; targetChuteCode: string; actualChuteCode: string; pageNumber: string;
}

/** 固定业务类型，不按HTTP地址或报文正文猜测业务。 */
export const durationTypes = [
  { key: 'completion', label: '完成耗时', group: 'stage' }, { key: 'dws', label: 'DWS 获取', group: 'stage' },
  { key: 'routing', label: '格口决策', group: 'stage' }, { key: 'sorting', label: '分拣执行', group: 'stage' },
  { key: 'scan-upload', label: '扫描上传', group: 'api' }, { key: 'chute-request', label: '请求格口', group: 'api' },
  { key: 'landing-report', label: '落格回传', group: 'api' }, { key: 'image-upload', label: '图片上传', group: 'api' },
  { key: 'other-api', label: '其他接口', group: 'api' },
] as const;

/** 严格校验本地日期，拒绝日历溢出、UTC和带偏移日期。 */
export function validAnalysisDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map(Number);
  if (year < 1 || year > 9998 || month < 1 || month > 12 || day < 1 || day > 31) return false;
  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

/** 默认日期由页面的本地今天提供，日期运算不受时区或夏令时影响。 */
export function analysisDateOffset(value: string, days: number): string {
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day + days);
  return date.toISOString().slice(0, 10);
}

/** URL保留总体和下钻范围，便于回退、刷新及分享。 */
export function readAnalysisFilters(params: URLSearchParams, today: string): AnalysisFilters {
  return { fromDate: params.get('fromDate') ?? analysisDateOffset(today, -6), toDate: params.get('toDate') ?? today,
    workstationName: params.get('workstationName') ?? '', sourceInstanceId: params.get('sourceInstanceId') ?? '',
    issue: params.get('issue') ?? 'exception', exceptionType: params.get('exceptionType') ?? '',
    durationType: params.get('durationType') ?? 'completion',
    minimumMilliseconds: params.get('minimumMilliseconds') ?? '', maximumMilliseconds: params.get('maximumMilliseconds') ?? '',
    mismatchOnly: params.get('mismatchOnly') === 'true', fallbackOnly: params.get('fallbackOnly') === 'true', targetChuteCode: params.get('targetChuteCode') ?? '',
    actualChuteCode: params.get('actualChuteCode') ?? '', pageNumber: params.get('pageNumber') ?? '1' };
}

/** 查询预算在发请求前说明，具体服务器配置仍由服务端验证。 */
export function analysisValidation(filters: AnalysisFilters, view: ParcelAnalysisView): string | null {
  if (!validAnalysisDate(filters.fromDate) || !validAnalysisDate(filters.toDate)) return '请选择有效的本地日期，格式为 YYYY-MM-DD。';
  const days = (Date.parse(filters.toDate) - Date.parse(filters.fromDate)) / 86400000;
  if (days < 0 || days > 30) return '日期范围最多 31 天，结束日期不能早于开始日期。';
  if (filters.workstationName.length > 128 || filters.sourceInstanceId.length > 96) return '来源筛选字段超过允许长度。';
  const integer = (value: string) => /^\d+$/.test(value) && Number.isSafeInteger(Number(value));
  if (!integer(filters.pageNumber) || Number(filters.pageNumber) < 1 || Number(filters.pageNumber) > 100000) return '页码无效。';
  if (view === 'exceptions' && (!['exception', 'noread', 'blocked'].includes(filters.issue)
    || filters.exceptionType && !integer(filters.exceptionType))) return '异常筛选范围无效。';
  if (view === 'duration') {
    if (!durationTypes.some(type => type.key === filters.durationType)) return '耗时类型无效，请重新选择。';
    if ([filters.minimumMilliseconds, filters.maximumMilliseconds].some(value => value !== '' && !integer(value))) return '耗时筛选必须为非负整数毫秒。';
    if (filters.maximumMilliseconds && Number(filters.maximumMilliseconds) <= Number(filters.minimumMilliseconds || 0)) return '耗时上限必须大于下限。';
  }
  if (view === 'chutes' && (filters.targetChuteCode.length > 128 || filters.actualChuteCode.length > 128)) return '格口编码超过允许长度。';
  return null;
}

/** 只发送当前分析使用的条件，统计总体与包裹下钻条件分开。 */
export function analysisApiPath(filters: AnalysisFilters, view: ParcelAnalysisView): string {
  const query = new URLSearchParams({ view, fromDate: filters.fromDate, toDate: filters.toDate, pageNumber: filters.pageNumber });
  for (const key of ['workstationName', 'sourceInstanceId'] as const) if (filters[key].trim()) query.set(key, filters[key].trim());
  if (view === 'exceptions') {
    query.set('issue', filters.issue);
    if (filters.issue === 'exception' && filters.exceptionType) query.set('exceptionType', filters.exceptionType);
  } else if (view === 'duration') {
    query.set('durationType', filters.durationType);
    if (filters.minimumMilliseconds) query.set('minimumMilliseconds', filters.minimumMilliseconds);
    if (filters.maximumMilliseconds) query.set('maximumMilliseconds', filters.maximumMilliseconds);
  } else {
    if (filters.mismatchOnly) query.set('mismatchOnly', 'true');
    if (filters.fallbackOnly) query.set('fallbackOnly', 'true');
    for (const key of ['targetChuteCode', 'actualChuteCode'] as const) if (filters[key].trim()) query.set(key, filters[key]);
  }
  return `/api/parcels/analysis?${query}`;
}

/** 保留有效的零，无分母或缺少数值时不制造0%。 */
export function analysisPercent(count: number, total: number): string { return total > 0 ? `${formatNumber(count / total * 100)}%` : '—'; }
/** 图表和表格都使用相同的毫秒区间边界。 */
export function durationBucketLabel(bucket: Pick<ParcelDurationBucket, 'minimumMilliseconds' | 'maximumMilliseconds'>): string {
  return bucket.maximumMilliseconds === null ? `≥ ${formatNumber(bucket.minimumMilliseconds, { grouping: true })} ms`
    : `${formatNumber(bucket.minimumMilliseconds, { grouping: true })}–<${formatNumber(bucket.maximumMilliseconds, { grouping: true })} ms`;
}
/** 编码缺失代表未知；比较忽略首尾空格和大小写，与后端口径一致。 */
export function chuteComparison(route: Pick<ParcelChuteRoute, 'targetChuteCode' | 'actualChuteCode'>): 'match' | 'mismatch' | 'unknown' {
  const target = route.targetChuteCode?.trim(), actual = route.actualChuteCode?.trim();
  return !target || !actual ? 'unknown' : target.toLowerCase() === actual.toLowerCase() ? 'match' : 'mismatch';
}
