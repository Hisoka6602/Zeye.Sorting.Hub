/** 明细字段按展示用途归组，原始记录保持不变。 */
export interface ParcelDetailFieldGroup {
  id: 'facts' | 'overview' | 'measurement' | 'binding' | 'timing' | 'routing' | 'request' | 'response' | 'diagnostic' | 'raw' | 'identity' | 'missing';
  title: string;
  keys: string[];
}

/** 固定协议字段优先归属，未来新增字段仍保留在明细中。 */
const fieldSections: readonly { id: ParcelDetailFieldGroup['id']; title: string; keys: readonly string[] }[] = [
  { id: 'facts', title: '关键字段', keys: [] },
  { id: 'request', title: '请求报文与参数', keys: ['queryParams', 'headers', 'requestHeaders', 'requestBody'] },
  { id: 'response', title: '响应报文', keys: ['responseHeaders', 'responseBody'] },
  { id: 'diagnostic', title: '调用诊断与说明', keys: ['exception', 'errorMessage', 'formattedMessage', 'decisionReason'] },
  { id: 'raw', title: '原始报文与附加内容', keys: ['rawData', 'rawPayload', 'rawResult', 'commandPayload', 'barcodesJson'] },
  { id: 'identity', title: '来源与调用元数据', keys: ['id', 'recordId', 'parcelId', 'sourceInstanceId', 'sourceRunId', 'sourceParcelId', 'payloadHash', 'key', 'apiType', 'stage', 'provider', 'attemptNumber', 'occurredAt', 'elapsedMilliseconds'] },
  { id: 'missing', title: '未提供字段', keys: [] },
];

/** 处理记录优先按业务语义展示有效信息，报文、元数据与空值仍复用通用分区。 */
const processingSections: typeof fieldSections = [
  { id: 'overview', title: '主要信息', keys: ['barcode', 'workstationName', 'sourceParcelId', 'parcelId', 'stage', 'isSuccess'] },
  { id: 'measurement', title: '重量与尺寸', keys: ['weightGrams', 'lengthMm', 'widthMm', 'heightMm', 'volumeMm3', 'volumetricWeightGrams'] },
  { id: 'binding', title: '关联决策', keys: ['bindingMode', 'candidateSourceParcelId', 'finalSourceParcelId', 'deltaMilliseconds', 'fifoRecoveryMode', 'isAwaitingWcsDecision', 'hasReliableTimestamp', 'hasReliableFrameBoundary'] },
  { id: 'timing', title: '时间记录', keys: ['occurredAt', 'recordedAt', 'partitionTime', 'receivedAt', 'measuredAt', 'requestAt', 'responseAt', 'previousCreationGapMilliseconds', 'isSpacingViolation'] },
  { id: 'routing', title: '分拣与执行', keys: ['provider', 'taskCode', 'targetChuteCode', 'dispatchedChuteCode', 'actualChuteCode', 'isFallback', 'isRoutingBlocked', 'exceptionCode', 'responseStatusCode', 'elapsedMilliseconds', 'imageCamera'] },
  { id: 'identity', title: '来源与技术标识', keys: [...fieldSections.find(section => section.id === 'identity')!.keys, 'messageIdentity', 'correlationId', 'triggerBatch', 'scanSequence', 'imageContentHash'] },
];

/** 空字符串、null 和 undefined 表示缺省；零值与 false 属于真实信息。 */
export function isMissingParcelDetailValue(value: unknown): boolean {
  return value === null || value === undefined || value === '';
}

/** 每个字段只进入一个分区，不删除空值或无法识别的新增字段。 */
export function groupParcelDetailRecordFields(facts: Record<string, unknown>, processing = false): ParcelDetailFieldGroup[] {
  const sections = processing ? [...processingSections, ...fieldSections.filter(section => !processingSections.some(group => group.id === section.id))] : fieldSections;
  const groups = sections.map(section => ({ id: section.id, title: processing && section.id === 'diagnostic' ? '关联说明与执行诊断' : section.title, keys: [] as string[] }));
  for (const key of Object.keys(facts)) {
    const value = facts[key];
    const section = isMissingParcelDetailValue(value) ? 'missing'
      : sections.find(section => section.keys.includes(key))?.id
        ?? (typeof value === 'object' || typeof value === 'string' && (value.length > 240 || value.includes('\n')) ? 'raw' : 'facts');
    groups.find(group => group.id === section)!.keys.push(key);
  }
  return groups.filter(group => group.keys.length > 0);
}

/** 沿用领域 ApiRequestType 的显式编码；缺省或未知类型不猜测业务。 */
export function parcelDetailApiTitle(apiType: unknown): string {
  const titles = ['请求格口', '锁格', '解锁', '落格回传', '图片上传', '扫描上传', '集包报告', '补充条码', '设备信息查询', '齐格', '格口切换', '稽核'];
  if (typeof apiType === 'number' && Number.isInteger(apiType)) return titles[apiType] ?? `外部接口 · 类型 ${apiType}`;
  return typeof apiType === 'string' && apiType !== '' ? apiType : '外部接口';
}

/** 仅整理 JSON 的空白，保持大整数、转义字符串及原文复制内容的精度。 */
export function formatParcelDetailPayload(text: string): string {
  if (text.length > 262144 || !/^\s*[\[{]/.test(text)) return text;
  // 解析仅校验语法，绝不重新序列化解析结果，避免 64 位编号被转为浮点数。
  try { JSON.parse(text); } catch { return text; }
  const tokens = text.match(/"(?:\\.|[^"\\])*"|[{}\[\],:]|[^\s{}\[\],:]+/g) ?? [];
  let depth = 0;
  let result = '';
  for (let index = 0; index < tokens.length; index++) {
    const token = tokens[index];
    if (token === '{' || token === '[') {
      result += token;
      depth++;
      // 过深的设备报文保持原文，避免缩进造成异常的内存增长。
      if (depth > 64) return text;
      if (tokens[index + 1] !== (token === '{' ? '}' : ']')) result += `\n${'  '.repeat(depth)}`;
    } else if (token === '}' || token === ']') {
      depth--;
      if (tokens[index - 1] !== (token === '}' ? '{' : '[')) result += `\n${'  '.repeat(depth)}`;
      result += token;
    } else if (token === ',') result += `,\n${'  '.repeat(depth)}`;
    else if (token === ':') result += ': ';
    else result += token;
  }
  return result;
}
