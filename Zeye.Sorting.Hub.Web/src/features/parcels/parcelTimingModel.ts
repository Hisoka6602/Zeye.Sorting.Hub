import type { ParcelTiming, ParcelTimingParcel } from '../../data/api/parcelTimingTypes.ts';
import { localTime } from '../../data/api/operationalTypes.ts';
import { buildParcelProcessingTimeline } from './parcelProcessingTimeline.ts';

export type TimingKind = 'detection' | 'scan' | 'chute' | 'dispatch' | 'landing' | 'other';
export type TimingAxis = 'absolute' | 'detected' | 'scan';
export interface TimingMark {
  key: string; parcelId: string; title: string; kind: TimingKind; start: bigint; end: bigint | null;
  startLabel: string; endLabel: string; state: string; issue: boolean; warning: string;
  source: string; attemptNumber: number | null; reportedElapsed: number | null;
}
export interface TimingRow { parcel: ParcelTimingParcel; marks: TimingMark[]; detected: bigint | null; scan: bigint | null; relativeIndex: number; invalidTimeCount: number }
export interface TimingNode { key: string; mark: TimingMark; boundary: 'start' | 'end'; ticks: bigint; label: string }
export interface TimingScale { min: number; max: number; origin: bigint; axis: TimingAxis }
export const timingKinds: { key: TimingKind; label: string }[] = [
  { key: 'detection', label: '首次检测 / 业务起点' },
  { key: 'scan', label: '扫码 / 扫描上传' }, { key: 'chute', label: '请求 / 分配格口' },
  { key: 'dispatch', label: '分拣指令' }, { key: 'landing', label: '落格 / 完成' }, { key: 'other', label: '其他动作' },
];
const apiTitles = ['请求格口', '锁格', '解锁', '落格报告', '上传图片', '扫描上传', '集包报告', '补充条码', '设备信息查询', '齐格', '格口切换', '稽核'];

/** 用100ns整数保存本地时间，保留.NET七位小数秒，不受浏览器时区和浮点相减影响。 */
export function parseTimingTime(value: string | null | undefined): bigint | null {
  if (!value) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(value);
  if (!match || Number(match[1]) <= 1) return null;
  const [, year, month, day, hour, minute, second, fraction = ''] = match;
  const date = new Date(0);
  date.setUTCFullYear(Number(year), Number(month) - 1, Number(day));
  date.setUTCHours(Number(hour), Number(minute), Number(second), 0);
  if (date.getUTCFullYear() !== Number(year) || date.getUTCMonth() !== Number(month) - 1 || date.getUTCDate() !== Number(day)
    || date.getUTCHours() !== Number(hour) || date.getUTCMinutes() !== Number(minute) || date.getUTCSeconds() !== Number(second)) return null;
  return BigInt(date.getTime()) * 10000n + BigInt(fraction.padEnd(7, '0'));
}

/** 还原来源本地时间并使用全站三位毫秒格式；原始整数精度独立用于计算。 */
export function formatTimingTime(value: bigint | null, clockOnly = false): string {
  if (value === null) return '未提供';
  const milliseconds = value / 10000n - (value < 0n && value % 10000n !== 0n ? 1n : 0n);
  const date = new Date(Number(milliseconds));
  const fraction = ((value % 10000000n + 10000000n) % 10000000n).toString().padStart(7, '0').replace(/0+$/, '').padEnd(3, '0');
  const formatted = localTime(`${date.toISOString().slice(0, 19)}.${fraction}`);
  return clockOnly ? formatted.slice(11) : formatted;
}

/** 间隔先进行整数相减再转毫秒，可表达100ns和负间隔。 */
export function timingInterval(start: bigint, end: bigint): number { return Number(end - start) / 10000; }
export function formatTimingMilliseconds(value: number | null): string {
  return value === null ? '—' : `${new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 4 }).format(value)} ms`;
}

/** URL中的编号必须完整保留64位精度，超界和无效值不发送查询。 */
export function validTimingParcelId(value: string | null): boolean {
  return Boolean(value && /^[1-9]\d{0,18}$/.test(value) && BigInt(value) <= 9223372036854775807n);
}

function kindFor(title: string): TimingKind {
  return title === '分拣机检测' ? 'detection' : /格口|锁格|解锁|齐格/.test(title) ? 'chute' : /扫码|扫描上传/.test(title) ? 'scan'
    : /分拣指令/.test(title) ? 'dispatch' : /实际落格|落格|完成/.test(title) ? 'landing' : 'other';
}
function compareTime(left: bigint, right: bigint): number { return left < right ? -1 : left > right ? 1 : 0; }
function earliest(values: (bigint | null)[]): bigint | null { return values.filter((value): value is bigint => value !== null).sort(compareTime)[0] ?? null; }
function latest(values: (bigint | null)[]): bigint | null { return values.filter((value): value is bigint => value !== null).sort(compareTime).at(-1) ?? null; }

/** 只用真实事实和明确关联的调用窗口生成图形，不使用elapsedMilliseconds猜测端点。 */
export function buildParcelTimingRows(data: ParcelTiming): TimingRow[] {
  const anchorIndex = data.items.findIndex(parcel => parcel.id === data.anchorId);
  return data.items.map((parcel, index) => {
    const marks: TimingMark[] = [];
    let invalidTimeCount = 0;
    const add = (mark: Omit<TimingMark, 'parcelId' | 'kind'>) => marks.push({ ...mark, parcelId: parcel.id, kind: kindFor(mark.title),
      warning: mark.end !== null && mark.end < mark.start ? [mark.warning, '响应 / 结束时间早于开始时间'].filter(Boolean).join('；') : mark.warning });
    const summary = (key: string, title: string, value: string | null) => {
      if (!value) return;
      const time = parseTimingTime(value);
      if (time === null) { invalidTimeCount++; return; }
      add({ key: `${parcel.id}:summary:${key}`, title, start: time, end: null, startLabel: '时间', endLabel: '',
        state: '', issue: false, warning: '', source: '包裹摘要', attemptNumber: null, reportedElapsed: null });
    };
    summary('scan', '扫码', parcel.scannedTime);
    const history = buildParcelProcessingTimeline(parcel.processingRecords ?? []);
    for (const item of history.items) {
      const request = earliest(item.events.map(event => parseTimingTime(event.record.requestAt)));
      const explicitStart = item.events.find(event => event.state === '调用开始');
      const response = latest(item.events.filter(event => event.state !== '迟到响应' && event.state !== '迟到失败').map(event => parseTimingTime(event.record.responseAt)));
      const explicitStartTime = parseTimingTime(explicitStart?.record.occurredAt);
      const hasStart = request !== null || explicitStartTime !== null;
      const start = request ?? explicitStartTime ?? response ?? parseTimingTime(item.occurredAt);
      if (start === null) { invalidTimeCount++; continue; }
      const terminal = explicitStart ? item.events.find(event => ['调用完成', '业务已接受', '业务未接受', '调用失败', '结果未知', '失败或结果未知', '已取消'].includes(event.state)) : undefined;
      const end = hasStart ? response ?? parseTimingTime(terminal?.record.occurredAt) : null;
      const timingInvalid = item.events.some(event => event.record.requestAt && parseTimingTime(event.record.requestAt) === null
        || event.record.responseAt && parseTimingTime(event.record.responseAt) === null);
      const unreliable = item.events.some(event => event.record.hasReliableTimestamp === false);
      const warning = [unreliable ? '来源标记时间不可靠' : '', timingInvalid ? '存在无效的请求 / 响应时间' : '',
        hasStart && end === null ? '未提供响应时间' : !hasStart && response !== null ? '未提供请求 / 开始时间' : ''].filter(Boolean).join('；');
      add({ key: `${parcel.id}:record:${item.key}`, title: item.title, start, end, startLabel: request !== null ? '请求' : explicitStartTime !== null ? '开始' : response !== null ? '响应' : '时间',
        endLabel: response !== null ? '响应' : '结束', state: item.state, issue: item.events.some(event => event.isIssue), warning,
        source: '处理事实', attemptNumber: item.attemptNumber, reportedElapsed: item.events.map(event => event.record.elapsedMilliseconds).find(value => value != null) ?? null });
      // 迟到响应保持独立节点，不能把超时窗口延长到迟到结果。
      for (const event of item.events.filter(event => event.state === '迟到响应' || event.state === '迟到失败')) {
        const time = parseTimingTime(event.record.occurredAt);
        if (time !== null && explicitStart) add({ key: `${parcel.id}:late:${event.record.recordId}`, title: `${item.title} · ${event.state}`,
          start: time, end: null, startLabel: '时间', endLabel: '', state: event.state, issue: true, warning: '迟到结果',
          source: '处理事实', attemptNumber: item.attemptNumber, reportedElapsed: null });
      }
    }
    if (!marks.some(mark => mark.title === '分拣机检测')) summary('detected', '分拣机检测', parcel.detectedTime);
    if (!parcel.processingRecords?.some(record => record.stage === 6)) summary('landed', '实际落格', parcel.dischargeTime);
    summary('completed', '处理完成', parcel.completedTime);
    for (const [requestIndex, request] of (parcel.apiRequests ?? []).entries()) {
      const start = parseTimingTime(request.requestTime);
      if (start === null) { invalidTimeCount++; continue; }
      const end = parseTimingTime(request.responseTime);
      const title = apiTitles[request.apiType] ?? `接口类型 ${request.apiType}`;
      add({ key: `${parcel.id}:api:${requestIndex}`, title, start, end, startLabel: '请求', endLabel: '响应',
        state: request.requestStatus === 2 ? '调用失败' : request.requestStatus === 1 ? '调用成功' : '未获得结果',
        issue: request.requestStatus === 2, warning: request.responseTime && end === null ? '响应时间无效' : end === null ? '未提供响应时间' : '',
        source: '接口记录', attemptNumber: null, reportedElapsed: request.elapsedMilliseconds });
    }
    marks.sort((left, right) => compareTime(left.start, right.start) || left.key.localeCompare(right.key));
    return { parcel, marks, detected: marks.find(mark => mark.title === '分拣机检测')?.start ?? null,
      scan: parseTimingTime(parcel.scannedTime), relativeIndex: index - anchorIndex, invalidTimeCount };
  });
}

/** 图形、下拉选择和明细表共用同一组真实端点。 */
export function parcelTimingNodes(rows: TimingRow[]): TimingNode[] {
  return rows.flatMap(row => row.marks.flatMap(mark => [
    { key: `${mark.key}:start`, mark, boundary: 'start' as const, ticks: mark.start, label: `${mark.title}${mark.end !== null || ['请求', '响应', '开始'].includes(mark.startLabel) ? ` · ${mark.startLabel}` : ''}` },
    ...(mark.end === null ? [] : [{ key: `${mark.key}:end`, mark, boundary: 'end' as const, ticks: mark.end, label: `${mark.title} · ${mark.endLabel}` }]),
  ]));
}

/** 对齐仅使用真实检测或扫码节点；Hub入库时间不作为业务起点。 */
export function timingReference(row: TimingRow, axis: TimingAxis): bigint | null {
  return axis === 'detected' ? row.detected : axis === 'scan' ? row.scan : null;
}

/** 绝对时间使用全图同一原点；对齐仅改变图形坐标，节点间隔仍比较真实时间。 */
export function createTimingScale(rows: TimingRow[], axis: TimingAxis): TimingScale {
  const times = rows.flatMap(row => row.marks.flatMap(mark => mark.end === null ? [mark.start] : [mark.start, mark.end]));
  const origin = earliest(times) ?? 0n;
  const values = rows.filter(row => axis === 'absolute' || timingReference(row, axis) !== null).flatMap(row => row.marks.flatMap(mark => (mark.end === null ? [mark.start] : [mark.start, mark.end])
    .map(time => timingInterval(timingReference(row, axis) ?? origin, time))));
  if (axis !== 'absolute') values.push(0);
  let min = values[0] ?? 0; let max = min;
  for (const value of values) { min = Math.min(min, value); max = Math.max(max, value); }
  const padding = max === min ? 1 : Math.max((max - min) * 0.05, 0.0001);
  return { min: min - padding, max: max + padding, origin, axis };
}

export function timingPosition(time: bigint, row: TimingRow, scale: TimingScale): number {
  const value = timingInterval(timingReference(row, scale.axis) ?? scale.origin, time);
  return (value - scale.min) / (scale.max - scale.min);
}

/** 标签和端点在各票内部避让，条形长度始终来自真实时间坐标。 */
export function layoutTimingMarks(row: TimingRow, scale: TimingScale, width: number): { mark: TimingMark; lane: number; startX: number; endX: number | null }[] {
  const occupied: number[] = [];
  return row.marks.map(mark => {
    const startX = timingPosition(mark.start, row, scale) * width;
    const endX = mark.end === null ? null : timingPosition(mark.end, row, scale) * width;
    const labelWidth = Math.min(180, mark.title.length * 11 + (mark.attemptNumber !== null ? 32 : 12));
    const labelLeft = startX > width - 130;
    const left = Math.min(startX - 10, (endX ?? startX) - 10, labelLeft ? startX - labelWidth : startX - 10);
    const right = Math.max(startX + 10, (endX ?? startX) + 10, labelLeft ? startX + 10 : startX + labelWidth);
    let lane = occupied.findIndex(end => end < left);
    if (lane < 0) lane = occupied.length;
    occupied[lane] = right;
    return { mark, lane, startX, endX };
  });
}
