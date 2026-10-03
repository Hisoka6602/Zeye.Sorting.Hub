import { useCallback, useEffect, useState } from 'react';
import { requestApi } from './client';

/** 查询状态，加载、空结果和错误均由真实请求决定。 */
interface ResourceState<T> { data?: T; loading: boolean; error?: Error }

/** 查询键变更时取消旧请求，避免上一包裹响应覆盖当前详情。 */
export function useApiResource<T>(path: string | null, loader: (path: string, signal?: AbortSignal) => Promise<T> = requestApi) {
  const [state, setState] = useState<ResourceState<T>>({ loading: path !== null });
  const [revision, setRevision] = useState(0);
  const refresh = useCallback(() => setRevision(value => value + 1), []);
  useEffect(() => {
    if (path === null) { setState({ loading: false }); return; }
    const controller = new AbortController();
    setState({ loading: true });
    loader(path, controller.signal).then(data => {
      if (!controller.signal.aborted) setState({ data, loading: false });
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setState({ loading: false, error: error instanceof Error ? error : new Error(String(error)) });
    });
    return () => controller.abort();
  }, [path, revision, loader]);
  return { ...state, refresh };
}
