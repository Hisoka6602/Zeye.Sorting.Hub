import { processingStages, type ParcelProcessingRecord } from '../../data/api/parcelTypes.ts';

/** 单条处理事实的业务名称、诊断状态与原始记录。 */
export interface ParcelProcessingEvent {
  record: ParcelProcessingRecord;
  title: string;
  description: string;
  state: string;
  color: 'blue' | 'green' | 'red' | 'orange';
  attemptNumber: number;
  isIssue: boolean;
  provider: string;
}

/** 按一次明确调用尝试归组的轨迹节点，原始事实全部保留。 */
export interface ParcelProcessingTimelineItem {
  key: string;
  title: string;
  occurredAt: string;
  description: string;
  state: string;
  color: ParcelProcessingEvent['color'];
  attemptNumber: number | null;
  events: ParcelProcessingEvent[];
}

/** 仅用于诊断事实关联的元数据，不改变后端阶段或业务状态。 */
interface ProviderEvent extends ParcelProcessingEvent {
  scope: string;
  operationId: string;
  attemptId: string;
  outcome: string;
  transport: boolean;
}

/** 安全读取可截断的诊断 JSON；不可解析的原文仍保存在记录详情。 */
function objectValue(value: unknown): Record<string, unknown> {
  if (typeof value === 'string') {
    try { value = JSON.parse(value); } catch { return {}; }
  }
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

/** 兼容协议的驼峰命名及 Provider 结果中的首字母大写属性。 */
function field(data: Record<string, unknown>, name: string): unknown {
  return data[name] ?? data[name[0].toUpperCase() + name.slice(1)];
}

/** 读取非空诊断文字，避免把任意对象显示为业务说明。 */
function text(data: Record<string, unknown>, name: string): string {
  const value = field(data, name);
  return typeof value === 'string' ? value.trim() : '';
}

/** 按明确的操作名称识别业务；裸 assignment 分类无法区分扫描与格口请求。 */
function operationTitle(operation: string, fallback: string): string {
  if (/目标格口|请求格口|chute[-_ ]?assignment|request[-_ ]?chute/i.test(operation)) return '请求格口';
  if (/扫描上传|scan[-_ ]?upload|scan[-_ ]?result/i.test(operation)) return '扫描上传';
  if (/落格|landing|discharge[-_ ]?report/i.test(operation)) return '落格回传';
  if (/图片上传|上传图片|image[-_ ]?upload|upload[-_ ]?image/i.test(operation)) return '图片上传';
  return fallback;
}

/** 请求、响应及原文沿用同一个已关联的业务名称，不按地址或正文猜测类型。 */
export function parcelProcessingFieldLabels(event: ParcelProcessingEvent): Record<string, string> {
  const business = event.title === 'Provider 交互' || event.title === 'Provider 调用' ? '未识别业务' : event.title;
  return {
    stage: '业务类型', apiType: '接口业务类型', requestStatus: '业务请求状态',
    rawPayload: `${business} · 原始报文`, rawData: `${business} · 原始报文`,
    requestUrl: `${business} · 接口地址`, requestHeaders: `${business} · 请求头`, headers: `${business} · 请求头`,
    requestBody: `${business} · 请求报文`, responseBody: `${business} · 响应报文`,
    responseStatusCode: `${business} · HTTP 状态码`,
  };
}

/** 读取调用状态，HTTP 成功或 completed 只代表调用结束，不推断业务成功。 */
function eventState(outcome: string, accepted: unknown): Pick<ParcelProcessingEvent, 'state' | 'color'> {
  const states: Record<string, ParcelProcessingEvent['state']> = {
    started: '调用开始', completed: '调用完成', unknown: '结果未知', failed: '调用失败', cancelled: '已取消',
    'late-completion': '迟到响应', 'late-failure': '迟到失败', accepted: '业务已接受',
    'failed-or-unknown': '失败或结果未知',
  };
  if (outcome.startsWith('late-')) return { state: states[outcome] ?? outcome, color: 'orange' };
  if (accepted === true) return { state: '业务已接受', color: 'green' };
  if (accepted === false) return { state: '业务未接受', color: 'red' };
  return { state: states[outcome] ?? outcome, color: /unknown|超时|取消/.test(outcome) ? 'orange'
    : /failed|失败/.test(outcome) ? 'red' : 'blue' };
}

/** 提取来源、操作及尝试标识；不为缺少元数据的记录虚构调用身份。 */
function describeRecord(record: ParcelProcessingRecord): ProviderEvent {
  const data = objectValue(record.rawPayload);
  const detail = objectValue(field(data, 'detail'));
  const kind = text(data, 'kind');
  const transport = kind === 'provider-call' || text(data, 'outcomeLevel') === 'transport';
  const operation = text(detail, 'operation') || text(data, 'operation')
    || (transport ? text(data, 'category') : text(data, 'name'));
  const providerRecord = record.stage === 3 || record.stage === 8;
  const title = providerRecord ? operationTitle(operation, transport ? 'Provider 交互'
    : record.stage === 8 ? '落格回传' : kind === 'provider-attempt' || kind === 'parcel-event' || text(data, 'operation') ? operation || 'Provider 调用'
    : processingStages[record.stage]) : processingStages[record.stage] ?? String(record.stage);
  const provider = record.provider || (kind === 'provider-attempt' ? text(data, 'category') : transport ? text(data, 'name') : text(data, 'provider'));
  const outcome = text(detail, 'outcome') || text(data, 'outcome') || text(data, 'status');
  // completed 的响应包含明确的业务结果时才使用它；传输成功不替代业务接受。
  const response = transport ? {} : objectValue(field(data, 'response'));
  const accepted = field(data, 'businessAccepted') ?? field(detail, 'businessAccepted')
    ?? field(response, 'isAccepted') ?? field(response, 'isAssigned');
  const skipped = outcome === 'completed' && text(response, 'rawResponse') === 'Scan upload disabled by config.';
  const status = providerRecord ? skipped ? { state: '已跳过', color: 'blue' as const } : eventState(outcome, accepted)
    : { state: '', color: record.isSuccess === false ? 'red' as const : 'blue' as const };
  const rawNumber = field(detail, 'attemptNumber') ?? field(data, 'attemptNumber') ?? record.attemptNumber;
  const attemptNumber = typeof rawNumber === 'number' && Number.isSafeInteger(rawNumber) && rawNumber > 0 ? rawNumber : 1;
  const operationId = text(detail, 'operationId') || text(data, 'operationId');
  const diagnostic = providerRecord && Boolean(operationId || kind === 'provider-attempt' || transport || text(data, 'operation'));
  // Hub 将 detail 保存到 errorMessage；完整的调用元数据不是异常，真实错误文本仍展示。
  const error = record.errorMessage?.trim();
  const diagnosticMessage = diagnostic && Boolean(text(objectValue(error), 'operationId'));
  const actualError = diagnosticMessage ? '' : error;
  const isIssue = record.stage === 7 || record.isSuccess === false || (diagnostic
    ? accepted === false || /unknown|failed|失败|超时|cancelled|取消/.test(outcome) || Boolean(actualError)
    : Boolean(error));
  const description = providerRecord && outcome ? [provider, status.state, actualError].filter(Boolean).join(' · ')
    : record.errorMessage || record.decisionReason || (record.actualChuteCode ? `实际格口：${record.actualChuteCode}`
      : record.targetChuteCode ? `目标格口：${record.targetChuteCode}` : record.messageIdentity || record.recordId);
  return { record, title, description, ...status, attemptNumber, isIssue, provider, outcome, transport, operationId,
    attemptId: text(detail, 'attemptId') || text(data, 'attemptId'),
    scope: JSON.stringify([record.sourceInstanceId, record.sourceRunId, record.sourceParcelId ?? record.parcelId]) };
}

/** 将本地时间字符串的小数秒补齐，精确比较同一毫秒内的事实，不引入时间转换。 */
function localOrder(value: string): string {
  const match = /^(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?$/.exec(value);
  return match ? `${match[1].replace(' ', 'T')}.${(match[2] ?? '').padEnd(7, '0')}` : '';
}

/** 事实按发生时间稳定排序，来源和原文不参与业务推断。 */
function compareEvents(left: ParcelProcessingEvent, right: ParcelProcessingEvent): number {
  return left.record.occurredAt.localeCompare(right.record.occurredAt) || left.record.recordId.localeCompare(right.record.recordId);
}

/** 创建独立节点；后续仅合并具备明确尝试身份的事实。 */
function newItem(event: ProviderEvent): ParcelProcessingTimelineItem {
  return { key: event.record.recordId, title: event.title, occurredAt: event.record.occurredAt,
    description: event.description, state: event.state, color: event.color,
    attemptNumber: event.operationId && !event.transport ? event.attemptNumber : null, events: [event] };
}

/** 匹配完整的调用窗口，时间或来源不明确的 HTTP 记录保持独立。 */
function callWindow(events: ProviderEvent[]): { start: string; end: string } | null {
  const start = events.find(event => event.outcome === 'started');
  const end = events.find(event => ['completed', 'accepted', 'failed', 'unknown', 'cancelled', 'failed-or-unknown'].includes(event.outcome));
  const startTime = start && localOrder(start.record.occurredAt);
  const endTime = end && localOrder(end.record.occurredAt);
  return startTime && endTime && startTime <= endTime ? { start: startTime, end: endTime } : null;
}

/** 归组操作事实并关联唯一窗口内的传输记录；每次重试及全部原始记录仍可查看。 */
export function buildParcelProcessingTimeline(records: readonly ParcelProcessingRecord[]): {
  events: ParcelProcessingEvent[]; items: ParcelProcessingTimelineItem[];
} {
  const events = records.map(describeRecord).sort(compareEvents);
  const attempts = new Map<string, ParcelProcessingTimelineItem>();
  const independent: ParcelProcessingTimelineItem[] = [];
  const transports: ProviderEvent[] = [];
  // 步骤一：按来源和操作、尝试标识归组，禁止只凭阶段合并不同请求。
  for (const event of events) {
    if (!event.operationId || (event.record.stage !== 3 && event.record.stage !== 8)) {
      if (event.transport) transports.push(event); else independent.push(newItem(event));
      continue;
    }
    const key = JSON.stringify([event.scope, event.operationId, event.attemptId, event.attemptNumber]);
    const item = attempts.get(key);
    if (item) item.events.push(event); else attempts.set(key, { ...newItem(event), key });
  }
  // 步骤二：老协议 HTTP 事实没有尝试标识，只在来源、Provider 与窗口唯一时关联。
  const windows = new Map<string, { item: ParcelProcessingTimelineItem; start: string; end: string }[]>();
  for (const item of attempts.values()) {
    const diagnosticEvents = item.events as ProviderEvent[];
    const anchor = diagnosticEvents.find(event => !event.transport);
    const window = callWindow(diagnosticEvents);
    if (!anchor?.record.sourceInstanceId || !window) continue;
    // 老版落格操作只上报 operation=landing；明确的业务分类可补足缺少 Provider 的关联。
    const identity = anchor.provider ? ['provider', anchor.provider] : ['operation', anchor.title];
    const key = JSON.stringify([anchor.scope, ...identity]);
    const bucket = windows.get(key) ?? [];
    bucket.push({ item, ...window });
    windows.set(key, bucket);
  }
  for (const event of transports) {
    const time = localOrder(event.record.occurredAt);
    const candidates = [
      ...(event.provider ? windows.get(JSON.stringify([event.scope, 'provider', event.provider])) ?? [] : []),
      ...(event.title !== 'Provider 交互' ? windows.get(JSON.stringify([event.scope, 'operation', event.title])) ?? [] : []),
    ];
    const matches = time ? candidates
      .filter(window => window.start <= time && time <= window.end
        && (event.title === 'Provider 交互' || event.title === window.item.title)) : [];
    if (matches.length === 1) matches[0].item.events.push(event); else independent.push(newItem(event));
  }
  // 步骤三：显示业务操作摘要，超时后的迟到结果不得改写为已成功。
  const items = [...attempts.values(), ...independent];
  for (const item of items) {
    const diagnosticEvents = item.events as ProviderEvent[];
    diagnosticEvents.sort(compareEvents);
    const anchor = diagnosticEvents.find(event => !event.transport) ?? diagnosticEvents[0];
    const diagnostics = diagnosticEvents.filter(event => !event.transport);
    const result = diagnostics.find(event => ['unknown', 'failed-or-unknown', 'cancelled'].includes(event.outcome))
      ?? diagnostics.at(-1) ?? anchor;
    item.title = anchor.title;
    item.attemptNumber = anchor.operationId && !anchor.transport ? anchor.attemptNumber : null;
    item.description = result.description;
    item.state = result.state;
    item.color = result.color;
    item.occurredAt = (diagnostics.find(event => event.outcome === 'started') ?? anchor).record.occurredAt;
    for (const event of diagnosticEvents) event.title = item.title;
  }
  items.sort((left, right) => right.occurredAt.localeCompare(left.occurredAt) || right.key.localeCompare(left.key));
  return { events: events.slice().reverse(), items };
}
