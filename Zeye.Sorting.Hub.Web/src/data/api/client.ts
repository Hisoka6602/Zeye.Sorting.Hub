/** HTTP错误保留状态码，用于区分不存在与服务异常。 */
export class ApiError extends Error {
  status: number;
  constructor(status: number, detail: string) { super(detail); this.status = status; }
}

/** 利用JSON原始数字文本保留64位编号，禁止将已失真编号用于详情查询。 */
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

/** 使用真实后端读取或写入合同，失败时不回退到演示数据。 */
export async function requestApi<T>(path: string, signal?: AbortSignal, init?: RequestInit, acceptedStatuses: readonly number[] = []): Promise<T> {
  if (import.meta.env?.MODE === 'design-preview') {
    if ((init?.method ?? 'GET').toUpperCase() !== 'GET') throw new ApiError(403, '设计预览只读，不能提交到后端');
    const { readDesignPreview } = await import('./designPreview');
    return readDesignPreview<T>(path, signal);
  }
  const base = import.meta.env?.VITE_API_BASE_URL ?? '';
  const response = await fetch(base + path, { ...init, signal, headers: { 'Content-Type': 'application/json', 'X-Zeye-Client': 'web', ...init?.headers } });
  const text = await response.text();
  let payload: Record<string, unknown> | undefined;
  try { payload = text ? parseApiJson<Record<string, unknown>>(text) : undefined; }
  catch (error) { if (!response.ok) throw new ApiError(response.status, `请求失败（${response.status}），服务未返回有效数据`); throw error; }
  if (!response.ok && !acceptedStatuses.includes(response.status)) throw new ApiError(response.status, String(payload?.detail ?? payload?.title ?? `请求失败（${response.status}）`));
  return payload as T;
}

/** 健康探针在依赖异常时返回 503，仍须显示其中的真实检查结果。 */
export async function readHealthReport<T>(path: string, signal?: AbortSignal): Promise<T> {
  const report = await requestApi<{ status?: string; entries?: unknown }>(path, signal, undefined, [503]);
  if (!report?.status || !report.entries || typeof report.entries !== 'object') throw new Error('健康检查未返回有效报告');
  return report as T;
}
