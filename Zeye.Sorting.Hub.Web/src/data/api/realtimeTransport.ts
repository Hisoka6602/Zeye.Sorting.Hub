import type { HubConnection, ISubscription } from '@microsoft/signalr';
import { ApiError } from './apiResponse.ts';
import type { RealtimeCommand } from './realtimePolicy.ts';

/** 实时消息携带 JSON 原文，64 位主键由统一接口解析器处理。 */
export interface RealtimeResponse { statusCode: number; json: string }
/** 连接状态用于展示在线、重连和失败，不将未知状态展示为正常。 */
export type RealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';
/** 同一资源共享流和快照，不为每个组件重复注册连接。 */
interface ResourceSubscription {
  listeners: Set<{ next: (response: RealtimeResponse) => void; error: (error: Error) => void }>;
  stream?: ISubscription<RealtimeResponse>;
  last?: RealtimeResponse;
}

let connection: HubConnection | undefined;
let starting: Promise<HubConnection> | undefined;
let generation = 0;
let retryTimer: ReturnType<typeof setTimeout> | undefined;
let connectionState: RealtimeConnectionState = 'disconnected';
const resources = new Map<string, ResourceSubscription>();
const stateListeners = new Set<() => void>();

/** 通知观察者连接状态改变，避免重复触发组件渲染。 */
function setState(value: RealtimeConnectionState) {
  if (connectionState === value) return;
  connectionState = value; stateListeners.forEach(listener => listener());
}
/** 获取当前连接状态供 React 外部存储订阅。 */
export function getRealtimeState() { return connectionState; }
/** 订阅连接状态，不读取或保存登录凭据。 */
export function subscribeRealtimeState(listener: () => void) {
  stateListeners.add(listener); return () => { stateListeners.delete(listener); };
}
/** 将握手状态保留为接口错误，普通断网不冒充认证失败。 */
function connectionError(error: unknown): Error {
  if (error && typeof error === 'object' && 'statusCode' in error && typeof error.statusCode === 'number')
    return new ApiError(error.statusCode, error.statusCode === 401 ? '登录会话已失效，请重新登录。' : `实时连接失败（${error.statusCode}）`);
  return error instanceof Error ? error : new Error(String(error));
}
/** 仅重试建立连接，不自动重复任何写入操作。 */
function scheduleStart() {
  if (retryTimer || !resources.size) return;
  retryTimer = setTimeout(() => {
    retryTimer = undefined;
    void getConnection().catch(error => {
      resources.forEach(resource => resource.listeners.forEach(listener => listener.error(connectionError(error))));
      if (!(error instanceof ApiError && (error.status === 401 || error.status === 403))) scheduleStart();
    });
  }, 3000);
}
/** 为资源注册一次服务器流，重连后重新获得最新快照。 */
function startResource(path: string, resource: ResourceSubscription) {
  if (resource.stream || !resource.listeners.size || connection?.state !== 'Connected') return;
  resource.stream = connection.stream<RealtimeResponse>('Watch', path).subscribe({
    next: response => {
      if (resources.get(path) !== resource) return;
      resource.last = response; resource.listeners.forEach(listener => listener.next(response));
    },
    error: error => {
      resource.stream = undefined;
      if (resources.get(path) !== resource) return;
      resource.listeners.forEach(listener => listener.error(connectionError(error)));
    },
    complete: () => { resource.stream = undefined; },
  });
}
/** 延迟加载官方客户端；所有页面复用一个带 Cookie 的同源长连接。 */
async function getConnection(): Promise<HubConnection> {
  if (connection?.state === 'Connected') return connection;
  if (starting) return starting;
  if (connection?.state === 'Reconnecting') throw new Error('实时连接正在恢复，请稍后重试。');
  const currentGeneration = generation;
  starting = (async () => {
    const { HubConnectionBuilder, HttpTransportType, LogLevel } = await import('@microsoft/signalr');
    if (currentGeneration !== generation) throw new DOMException('会话已切换', 'AbortError');
    const candidate = new HubConnectionBuilder()
      .withUrl((import.meta.env?.VITE_API_BASE_URL ?? '') + '/hubs/sorting', { withCredentials: true, transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: context => Math.min(30_000, 1000 * 2 ** Math.min(context.previousRetryCount, 5)) })
      .configureLogging(LogLevel.None).build();
    connection = candidate; setState('connecting');
    candidate.onreconnecting(() => {
      if (connection !== candidate) return;
      setState('reconnecting'); resources.forEach(resource => { resource.stream = undefined; });
    });
    candidate.onreconnected(() => {
      if (connection !== candidate) return;
      setState('connected'); resources.forEach((resource, path) => startResource(path, resource));
    });
    candidate.onclose(() => {
      if (connection !== candidate) return;
      setState('disconnected'); resources.forEach(resource => { resource.stream = undefined; }); scheduleStart();
    });
    try {
      await candidate.start();
      if (currentGeneration !== generation) { await candidate.stop(); throw new DOMException('会话已切换', 'AbortError'); }
      setState('connected'); resources.forEach((resource, path) => startResource(path, resource));
      return candidate;
    } catch (error) {
      if (connection === candidate) { connection = undefined; setState('disconnected'); }
      throw connectionError(error);
    }
  })();
  try { return await starting; } finally { if (currentGeneration === generation) starting = undefined; }
}
/** 读取一次快照，取消后忽略响应，服务端仍有独立的有界执行预算。 */
export async function readRealtimeResource(path: string, signal?: AbortSignal): Promise<RealtimeResponse> {
  if (signal?.aborted) throw new DOMException('查询已取消', 'AbortError');
  const active = await getConnection();
  if (signal?.aborted) throw new DOMException('查询已取消', 'AbortError');
  return new Promise((resolve, reject) => {
    const abort = () => { cleanup(); reject(new DOMException('查询已取消', 'AbortError')); };
    const timeout = setTimeout(() => { cleanup(); reject(new ApiError(504, '实时查询超时，请重试。')); }, 20_000);
    const cleanup = () => { clearTimeout(timeout); signal?.removeEventListener('abort', abort); };
    signal?.addEventListener('abort', abort, { once: true });
    active.invoke<RealtimeResponse>('Read', path).then(value => { cleanup(); resolve(value); }, error => { cleanup(); reject(connectionError(error)); });
  });
}
/** 提交一次明确用例，重连仅恢复查询，不能重放提交或改用 HTTP 再提交。 */
export async function submitRealtimeCommand(command: RealtimeCommand, signal?: AbortSignal): Promise<RealtimeResponse> {
  if (signal?.aborted) throw new DOMException('提交已取消', 'AbortError');
  const active = await getConnection();
  if (signal?.aborted) throw new DOMException('提交已取消', 'AbortError');
  return new Promise((resolve, reject) => {
    const unknownResult = () => { cleanup(); reject(new Error('实时提交结果尚未确认，请先查看包裹记录再决定是否重试。')); };
    const timeout = setTimeout(unknownResult, 20_000);
    const cleanup = () => { clearTimeout(timeout); signal?.removeEventListener('abort', unknownResult); };
    signal?.addEventListener('abort', unknownResult, { once: true });
    active.invoke<RealtimeResponse>(command.method, ...command.arguments).then(value => { cleanup(); resolve(value); }, unknownResult);
  });
}
/** 共享订阅；最后一个观察者离开时取消服务器流，筛选切换不接受旧结果。 */
export function watchRealtimeResource(path: string, next: (response: RealtimeResponse) => void, error: (error: Error) => void, refresh = false): () => void {
  let resource = resources.get(path);
  if (!resource) { resource = { listeners: new Set() }; resources.set(path, resource); }
  const listener = { next, error }; resource.listeners.add(listener);
  if (resource.last) next(resource.last);
  const current = resource;
  if (refresh && current.last) void readRealtimeResource(path).then(response => {
    if (resources.get(path) !== current) return;
    current.last = response; current.listeners.forEach(item => item.next(response));
  }).catch(cause => { if (resources.get(path) === current) error(connectionError(cause)); });
  void getConnection().then(() => startResource(path, current)).catch(cause => {
    if (resources.get(path) !== current) return;
    current.listeners.forEach(item => item.error(connectionError(cause)));
    if (!(cause instanceof ApiError && (cause.status === 401 || cause.status === 403))) scheduleStart();
  });
  return () => {
    current.listeners.delete(listener);
    if (!current.listeners.size && resources.get(path) === current) {
      resources.delete(path); current.stream?.dispose();
      if (!resources.size && retryTimer) { clearTimeout(retryTimer); retryTimer = undefined; }
    }
  };
}
/** 登录、退出或身份变更后重新握手，不复用旧 Cookie 建立的连接。 */
export function resetRealtimeConnection() {
  generation += 1; const old = connection; connection = undefined; starting = undefined;
  if (retryTimer) { clearTimeout(retryTimer); retryTimer = undefined; }
  resources.forEach(resource => { resource.stream?.dispose(); resource.stream = undefined; resource.last = undefined; });
  setState('disconnected');
  void old?.stop();
  scheduleStart();
}
