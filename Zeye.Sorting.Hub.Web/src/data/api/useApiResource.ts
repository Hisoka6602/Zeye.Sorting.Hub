import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { requestApi } from './client';
import { ApiError, decodeApiResponse, requireHealthReport } from './apiResponse';
import { getRealtimeAuthenticated, isRealtimeResource, setRealtimeAuthenticated, subscribeRealtimeAuthenticated, usesRealtimeTransport } from './realtimePolicy';

/** 查询状态，加载、空结果和错误均由真实请求决定。 */
interface ResourceState<T> { data?: T; loading: boolean; error?: Error }

/** 查询键变更时取消旧请求，避免上一包裹响应覆盖当前详情。 */
export function useApiResource<T>(path: string | null, loader: (path: string, signal?: AbortSignal) => Promise<T> = requestApi, realtime = true, retainOnError = false) {
  const [state, setState] = useState<ResourceState<T>>({ loading: path !== null });
  const [revision, setRevision] = useState(0);
  const previousPath = useRef<string | null | undefined>(undefined);
  const appliedRevision = useRef(0);
  const authenticated = useSyncExternalStore(subscribeRealtimeAuthenticated, getRealtimeAuthenticated, getRealtimeAuthenticated);
  const refresh = useCallback(() => setRevision(value => value + 1), []);
  useEffect(() => {
    const sameResource = previousPath.current === path;
    const refreshRequested = sameResource && appliedRevision.current !== revision;
    previousPath.current = path;
    appliedRevision.current = revision;
    if (path === null) { setState({ loading: false }); return; }
    const controller = new AbortController();
    const realtimeResource = realtime && usesRealtimeTransport() && isRealtimeResource(path);
    /** 所有失败入口统一清理；权限变化不得保留先前授权的数据。 */
    const fail = (error: unknown) => {
      if (controller.signal.aborted) return;
      const denied = error instanceof ApiError && (error.status === 401 || error.status === 403);
      setState(previous => ({
        ...(retainOnError && sameResource && !denied ? { data: previous.data } : {}),
        loading: false, error: error instanceof Error ? error : new Error(String(error)),
      }));
      if (realtimeResource && denied) {
        if (error.status === 401) setRealtimeAuthenticated(false);
        window.dispatchEvent(new Event('zeye-session-changed'));
      }
    };
    setState(previous => sameResource ? { ...previous, loading: true, error: undefined } : { loading: true });
    if (realtimeResource) {
      let unsubscribe: (() => void) | undefined;
      void import('./realtimeTransport').then(({ watchRealtimeResource }) => {
        if (controller.signal.aborted) return;
        unsubscribe = watchRealtimeResource(path, response => {
          if (controller.signal.aborted) return;
          try {
            const data = decodeApiResponse<T>(response.statusCode, response.json, path.startsWith('/health/') ? [503] : []);
            setState({ data: path.startsWith('/health/') ? requireHealthReport<T>(data) : data, loading: false });
          } catch (error) { fail(error); }
        }, fail, refreshRequested);
      }).catch(fail);
      return () => { controller.abort(); unsubscribe?.(); };
    }
    loader(path, controller.signal).then(data => {
      if (!controller.signal.aborted) setState({ data, loading: false });
    }).catch(fail);
    return () => controller.abort();
  }, [path, revision, loader, realtime, authenticated, retainOnError]);
  return { ...state, refresh };
}
