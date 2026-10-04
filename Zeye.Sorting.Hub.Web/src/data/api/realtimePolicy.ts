/** 与服务端一致的读取资源白名单；登录、文件和危险管理动作保留原入口。 */
const realtimePaths = new Set([
  '/api/parcels', '/api/parcels/cursor', '/api/parcels/analytics', '/api/parcels/adjacent', '/api/parcels/processing-records/unbound',
  '/api/data-governance/archive-tasks', '/api/diagnostics/slow-queries', '/api/audit/web-requests',
  '/api/operations/partitions', '/api/operations/configuration', '/api/operations/configuration/policy',
  '/api/operations/rules/parcel', '/api/operations/rules/exception', '/api/operations/backup', '/api/operations/backup/artifacts',
  '/health/live', '/health/ready', '/health/deep',
]);
let authenticated = false;
const authenticationListeners = new Set<() => void>();

/** 登录状态只来自公开会话接口，不读取 Cookie 或浏览器凭据存储。 */
export function setRealtimeAuthenticated(value: boolean) {
  if (authenticated === value) return;
  authenticated = value; authenticationListeners.forEach(listener => listener());
}
/** 为公开健康检查保留匿名 HTTP 读取，登录后才建立认证实时通道。 */
export function getRealtimeAuthenticated() { return authenticated; }
/** 订阅会话授权状态，登录、退出及失效后切换相应读取入口。 */
export function subscribeRealtimeAuthenticated(listener: () => void) {
  authenticationListeners.add(listener); return () => { authenticationListeners.delete(listener); };
}

/** 只在真实浏览器中启用实时通道，离线设计预览和 Node 单元测试保持原合同。 */
export function usesRealtimeTransport(): boolean {
  return authenticated && typeof window !== 'undefined' && import.meta.env?.MODE !== 'design-preview';
}

/** 路径必须为明确的本地读取资源；查询参数可包含合法 URL 编码。 */
export function isRealtimeResource(path: string): boolean {
  if (!path || path.length > 4096 || !path.startsWith('/') || /[\\#\x00-\x1f\x7f]/.test(path) || path.includes('//')) return false;
  const route = path.split('?', 1)[0];
  if (route.includes('%') || route.includes('..')) return false;
  return realtimePaths.has(route) || /^\/api\/parcels\/[1-9][0-9]{0,18}(\/images)?$/.test(route)
    || /^\/api\/audit\/web-requests\/[1-9][0-9]{0,18}$/.test(route)
    || /^\/api\/diagnostics\/slow-queries\/[a-zA-Z0-9_-]{1,128}$/.test(route);
}

/** 实时提交只包含两个明确用例，不能传入自由管理路径或 HTTP 方法。 */
export interface RealtimeCommand { method: 'AppendProcessingRecord' | 'UpdateParcelStatus'; arguments: string[] }

/** 小正文高频提交复用原业务入口；超出实时消息预算的请求仍走 HTTP。 */
export function selectRealtimeCommand(path: string, method: string, body: BodyInit | null | undefined): RealtimeCommand | undefined {
  if (typeof body !== 'string' || !body.trim() || new TextEncoder().encode(body).byteLength > 4096) return;
  if (method === 'POST' && path === '/api/admin/parcels/processing-records') return { method: 'AppendProcessingRecord', arguments: [body] };
  const match = method === 'PUT' && /^\/api\/admin\/parcels\/([1-9][0-9]{0,18})$/.exec(path);
  if (match) return { method: 'UpdateParcelStatus', arguments: [match[1], body] };
}
