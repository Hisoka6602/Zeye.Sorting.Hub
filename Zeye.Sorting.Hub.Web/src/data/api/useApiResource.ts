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
  const authenticated = useSyncExternalStore(subscribeRealtimeAuthenticated, getRealtimeAuthenticated, getRealtimeAuthenticated);
  const refresh = useCallback(() => setRevision(value => value + 1), []);
  useEffect(() => {
    if (path === null) { setState({ loading: false }); return; }
    const controller = new AbortController();
    const sameResource = previousPath.current === path;
    previousPath.current = path;
    setState(previous => sameResource ? { ...previous, loading: true, error: undefined } : { loading: true });
    if (realtime && usesRealtimeTransport() && isRealtimeResource(path)) {
      let unsubscribe: (() => void) | undefined;
      void import('./realtimeTransport').then(({ watchRealtimeResource }) => {
        if (controller.signal.aborted) return;
        unsubscribe = watchRealtimeResource(path, response => {
          if (controller.signal.aborted) return;
          try {
            const data = decodeApiResponse<T>(response.statusCode, response.json, path.startsWith('/health/') ? [503] : []);
            setState({ data: path.startsWith('/health/') ? requireHealthReport<T>(data) : data, loading: false });
          } catch (error) {
            if (error instanceof ApiError && error.status === 401) setRealtimeAuthenticated(false);
            if (error instanceof ApiError && (error.status === 401 || error.status === 403)) window.dispatchEvent(new Event('zeye-session-changed'));
            setState(previous => ({ ...previous, loading: false, error: error instanceof Error ? error : new Error(String(error)) }));
          }
        }, error => {
          if (controller.signal.aborted) return;
          if (error instanceof ApiError && error.status === 401) { setRealtimeAuthenticated(false); window.dispatchEvent(new Event('zeye-session-changed')); }
          setState(previous => ({ ...previous, loading: false, error }));
        }, revision > 0);
      }).catch(error => { if (!controller.signal.aborted) setState(previous => ({ ...previous, loading: false, error })); });
      return () => { controller.abort(); unsubscribe?.(); };
    }
    loader(path, controller.signal).then(data => {
      if (!controller.signal.aborted) setState({ data, loading: false });
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setState(previous => ({
        ...(retainOnError && sameResource && !(error instanceof ApiError && [401, 403].includes(error.status)) ? { data: previous.data } : {}),
        loading: false, error: error instanceof Error ? error : new Error(String(error)),
      }));
    });
    return () => controller.abort();
  }, [path, revision, loader, realtime, authenticated, retainOnError]);
  return { ...state, refresh };
}
