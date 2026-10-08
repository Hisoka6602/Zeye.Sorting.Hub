import { ApiError, decodeApiResponse, requireHealthReport } from './apiResponse.ts';
import { isRealtimeResource, selectRealtimeCommand, setRealtimeAuthenticated, usesRealtimeTransport } from './realtimePolicy.ts';
export { ApiError, parseApiJson } from './apiResponse.ts';

/** 使用真实后端读取或写入合同，失败时不回退到演示数据。 */
export async function requestApi<T>(path: string, signal?: AbortSignal, init?: RequestInit, acceptedStatuses: readonly number[] = [], forceHttp = false): Promise<T> {
  if (import.meta.env?.MODE === 'design-preview') {
    if ((init?.method ?? 'GET').toUpperCase() !== 'GET') throw new ApiError(403, '设计预览只读，不能提交到后端');
    const { readDesignPreview } = await import('./designPreview');
    return readDesignPreview<T>(path, signal);
  }
  if (!forceHttp && (init?.method ?? 'GET').toUpperCase() === 'GET' && usesRealtimeTransport() && isRealtimeResource(path)) {
    const { readRealtimeResource } = await import('./realtimeTransport.ts');
    const response = await readRealtimeResource(path, signal);
    return decodeApiResponse<T>(response.statusCode, response.json, acceptedStatuses);
  }
  const command = !forceHttp && usesRealtimeTransport() ? selectRealtimeCommand(path, (init?.method ?? 'GET').toUpperCase(), init?.body) : undefined;
  if (command) {
    const { submitRealtimeCommand } = await import('./realtimeTransport.ts');
    const response = await submitRealtimeCommand(command, signal);
    return decodeApiResponse<T>(response.statusCode, response.json, acceptedStatuses);
  }
  const base = import.meta.env?.VITE_API_BASE_URL ?? '';
  const response = await fetch(base + path, { ...init, signal, headers: { 'Content-Type': 'application/json', 'X-Zeye-Client': 'web', ...init?.headers } });
  const text = await response.text();
  const payload = decodeApiResponse<T>(response.status, text, acceptedStatuses);
  if (path === '/api/access/session') setRealtimeAuthenticated(Boolean(payload && typeof payload === 'object' && 'authenticated' in payload && payload.authenticated));
  return payload;
}

/** 配置维护读取明确使用 HTTP，复用认证头、错误解析及设计预览只读保护。 */
export function requestHttpApi<T>(path: string, signal?: AbortSignal, init?: RequestInit): Promise<T> {
  return requestApi<T>(path, signal, init, [], true);
}

/** 健康探针在依赖异常时返回 503，仍须显示其中的真实检查结果。 */
export async function readHealthReport<T>(path: string, signal?: AbortSignal): Promise<T> {
  const report = await requestApi<{ status?: string; entries?: unknown }>(path, signal, undefined, [503]);
  return requireHealthReport<T>(report);
}
