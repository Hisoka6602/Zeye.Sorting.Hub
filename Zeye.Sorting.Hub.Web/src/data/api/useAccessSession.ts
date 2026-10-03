import { useEffect } from 'react';
import { useApiResource } from './useApiResource';
import type { AccessSession } from './accessTypes';
/** 登录或退出成功后立即刷新显示身份。 */
export function useAccessSession() {
  const resource = useApiResource<AccessSession>('/api/access/session');
  useEffect(() => {
    const listener = () => resource.refresh();
    window.addEventListener('zeye-session-changed', listener);
    return () => window.removeEventListener('zeye-session-changed', listener);
  }, [resource.refresh]);
  return resource;
}
export function sessionChanged() { window.dispatchEvent(new Event('zeye-session-changed')); }
