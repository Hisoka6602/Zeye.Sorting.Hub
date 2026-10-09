import { requestHttpApi } from './client.ts';
import type { DatabaseStartupStatus } from './configurationTypes.ts';

/** 重启期间只使用 HTTP 状态接口，并且必须观察到新实例，不能把旧进程误判为成功。 */
export async function waitForHostRestart(previousInstanceId: string, signal: AbortSignal,
  readStatus: (signal: AbortSignal) => Promise<DatabaseStartupStatus> = current => requestHttpApi('/api/setup/status', current),
  interval = 2000, timeout = 180000): Promise<DatabaseStartupStatus> {
  const started = performance.now();
  while (performance.now() - started < timeout) {
    signal.throwIfAborted();
    try {
      const status = await readStatus(AbortSignal.any([signal, AbortSignal.timeout(3000)]));
      if (status.instanceId && status.instanceId !== previousInstanceId) return status;
    } catch {
      signal.throwIfAborted(); // 连接中断和单次探测超时属于重启过程，组件取消则立即退出。
    }
    signal.throwIfAborted();
    await new Promise<void>((resolve, reject) => {
      const cancel = () => { clearTimeout(timer); reject(signal.reason); };
      const timer = setTimeout(() => { signal.removeEventListener('abort', cancel); resolve(); }, interval);
      signal.addEventListener('abort', cancel, { once: true });
    });
  }
  throw new Error('Host 尚未恢复，请稍后刷新页面并检查服务日志。');
}
