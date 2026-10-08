import { useCallback, useEffect } from 'react';
import { useBlocker, type BlockerFunction } from 'react-router';

/** 统一保护运行配置和运维策略草稿，覆盖站内导航、历史返回及浏览器刷新。 */
export function useConfigurationNavigationGuard(dirty: boolean, saving: boolean) {
  const protectedDraft = dirty || saving;
  const shouldBlock = useCallback<BlockerFunction>(({ currentLocation, nextLocation }) =>
    protectedDraft && currentLocation.pathname !== nextLocation.pathname, [protectedDraft]);
  const blocker = useBlocker(shouldBlock);
  useEffect(() => {
    if (!protectedDraft) return;
    const protect = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', protect);
    return () => window.removeEventListener('beforeunload', protect);
  }, [protectedDraft]);
  useEffect(() => {
    if (!protectedDraft && blocker.state === 'blocked') blocker.proceed();
  }, [protectedDraft, blocker]);
  return blocker;
}
