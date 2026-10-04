/** 接口错误保留真实状态，HTTP 与实时响应使用同一错误合同。 */
export class ApiError extends Error {
  status: number;
  constructor(status: number, detail: string) { super(detail); this.status = status; }
}

/** 使用 JSON 原始数字文本保留 64 位编号，不重新解析正文中的 JSON 字符串。 */
export function parseApiJson<T>(text: string): T {
  return JSON.parse(text, (key: string, value: unknown, context?: { source: string }) => {
    if (typeof value !== 'number') return value;
    const identifier = key === 'id' || key.endsWith('Id') || key === 'parcelTimestamp';
    if (identifier || !Number.isSafeInteger(value) && Number.isInteger(value)) {
      if (context?.source) return context.source;
      if (!Number.isSafeInteger(value)) throw new Error('浏览器无法准确读取此包裹编号，请更新浏览器后重试。');
      return String(value);
    }
    return value;
  }) as T;
}

/** 统一解析原文和状态码；503 健康报告等明确允许的状态仍展示真实结果。 */
export function decodeApiResponse<T>(status: number, text: string, acceptedStatuses: readonly number[] = []): T {
  let payload: Record<string, unknown> | undefined;
  const successful = status >= 200 && status < 300;
  try { payload = text ? parseApiJson<Record<string, unknown>>(text) : undefined; }
  catch (error) { if (!successful) throw new ApiError(status, `请求失败（${status}），服务未返回有效数据`); throw error; }
  if (!successful && !acceptedStatuses.includes(status)) throw new ApiError(status, String(payload?.detail ?? payload?.title ?? `请求失败（${status}）`));
  return payload as T;
}

/** 两种传输统一校验健康报告，异常的 503 报告有效，任意错误对象无效。 */
export function requireHealthReport<T>(report: unknown): T {
  if (!report || typeof report !== 'object' || !('status' in report) || !report.status || !('entries' in report)
    || !report.entries || typeof report.entries !== 'object' || Array.isArray(report.entries)) throw new Error('健康检查未返回有效报告');
  return report as T;
}
