import type { ParcelDetail } from '../../data/api/parcelTypes.ts';
import { buildParcelProcessingTimeline, type ParcelProcessingEvent } from './parcelProcessingTimeline.ts';

const exceptionLabels: Record<number, string> = {
  0: '未知异常', 1: '接口响应异常', 2: '等待DWS数据超时', 3: '等待目标格口超时',
  4: '无效目标格口', 5: '速度不匹配', 6: '锁格', 7: '叠包', 8: '灰度仪响应异常',
  9: '位置检测异常', 10: '包裹丢失', 11: '机械故障', 12: '飘格',
  13: '包裹间距违规', 14: '目标格口分配被拒绝', 15: '来源设备异常', 16: '异常落格',
};

// These exact source-code mappings are implemented by the Hub's processing projection.
const sourceRules: Record<string, { type: number; name: string }> = {
  parcelspacingviolation: { type: 13, name: '包裹间距违规分类规则' },
  targetchuteassignmentrejected: { type: 14, name: '目标格口分配被拒绝分类规则' },
  routingtimeout: { type: 3, name: '等待目标格口超时分类规则' },
};

type ExceptionParcel = Pick<ParcelDetail, 'status' | 'exceptionType' | 'sourceExceptionCode' | 'processingRecords' | 'apiRequests'>;

/** 历史异常独立于完成状态；兼容旧数据只有异常状态、缺少分类的情况。 */
export function hasParcelException(parcel: { status: number; exceptionType?: number | null }) {
  return parcel.exceptionType != null || parcel.status === 2;
}

/** Describe saved facts only; a browser draft is not evidence of the rule that classified a parcel. */
export function parcelExceptionDetails(parcel: ExceptionParcel | undefined, events?: readonly ParcelProcessingEvent[]) {
  if (!parcel) return null;
  const recordEvents = events ?? buildParcelProcessingTimeline(parcel.processingRecords).events;
  const eventsByRecord = new Map(recordEvents.map(event => [event.record.recordId, event]));
  const issues = recordEvents.filter(event => event.isIssue);
  const records = issues.map(event => event.record);
  const interfaceErrors = parcel.apiRequests.filter(request => typeof request.exception === 'string' && request.exception.trim());
  const current = parcel.status === 2;
  const classified = hasParcelException(parcel);
  if (!classified && !records.length && !interfaceErrors.length && !parcel.sourceExceptionCode?.trim()) return null;

  const sourceCode = parcel.sourceExceptionCode?.trim()
    || records.find(record => record.stage === 7 && record.exceptionCode?.trim())?.exceptionCode?.trim();
  const sourceRule = sourceCode ? sourceRules[sourceCode.toLowerCase()] : undefined;
  // 旧数据仍可展示来源异常，新数据的异常分类独立于生命周期完成状态。
  const type = parcel.exceptionType ?? (sourceRule && records.some(record => record.stage === 7 && record.exceptionCode?.trim().toLowerCase() === sourceCode?.toLowerCase()) ? sourceRule.type : undefined);
  const rule = type === 0
    ? '未知异常兜底规则：所有异常分类规则均未匹配。'
    : sourceRule && sourceRule.type === type
      ? `${sourceRule.name}：来源异常代码等于 ${sourceCode}。`
      : type === 16 ? '实际格口必须与有效目标格口一致，且不得使用兜底或强排格口。' : '未提供异常判定规则';
  const messages = [...new Set([
    ...issues.flatMap(event => event.record.stage === 3 || event.record.stage === 8 ? [event.description]
      : [event.record.errorMessage, event.record.decisionReason]).filter((value): value is string => Boolean(value?.trim())),
    ...interfaceErrors.map(request => {
      const event = typeof request.recordId === 'string' ? eventsByRecord.get(request.recordId) : undefined;
      // 仅使用同编号、同原文的现有事件摘要；不同接口错误与旧版记录仍展示自身原文。
      return event && request.exception === event.record.errorMessage ? event.description : String(request.exception);
    }),
  ])];
  return {
    current,
    classified,
    type: type == null ? '未提供异常类型' : exceptionLabels[type] ?? `异常类型 ${type}`,
    rule,
    sourceCode,
    messages,
    records,
    recordTitles: Object.fromEntries(issues.map(event => [event.record.recordId, event.title])),
    interfaceErrors,
    eventsByRecord,
  };
}
