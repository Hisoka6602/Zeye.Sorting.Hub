import { App, Button } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { requestHttpApi } from '../../data/api/client';
import { waitForHostRestart } from '../../data/api/hostRestart';

/** 两种配置入口共用重启确认、断线等待和新实例检测；访问码只留在当前页面内存。 */
export function HostRestartButton({ revision, disabled, setupKey, onPendingChange }: {
  revision: string; disabled: boolean; setupKey?: string; onPendingChange: (pending: boolean) => void;
}) {
  const { modal, message } = App.useApp();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const active = useRef<AbortController | null>(null);
  useEffect(() => () => active.current?.abort(), []);

  /** 确认后只提交已保存版本；短暂断线期间保持表单锁定，恢复后重新进入当前页面。 */
  const restart = async () => {
    if (disabled || active.current) return;
    const controller = new AbortController(); active.current = controller;
    setPending(true); setError(undefined); onPendingChange(true);
    try {
      const response = await requestHttpApi<{ instanceId: string }>(setupKey ? '/api/setup/host/restart' : '/api/operations/configuration/restart', controller.signal, {
        method: 'POST', body: JSON.stringify({ revision }), ...(setupKey ? { headers: { 'X-Zeye-Setup-Key': setupKey } } : {}),
      });
      const status = await waitForHostRestart(response.instanceId, controller.signal);
      if (controller.signal.aborted) return;
      // 先解除导航保护，再重新读取新实例的配置和身份；配置失败时复用本次主动重启的访问码。
      flushSync(() => { setPending(false); onPendingChange(false); });
      if (status.requiresConfiguration && setupKey && status.localSetupAllowed)
        window.location.replace(`${window.location.pathname}${window.location.search}#setup=${encodeURIComponent(setupKey)}`);
      // 仅改变片段不会重新加载页面，必须刷新以重新读取启动状态和生效配置。
      window.location.reload();
    } catch (failure) {
      if (!controller.signal.aborted) {
        const detail = failure instanceof Error ? failure.message : 'Host 重启失败，请检查服务日志。';
        setError(detail); message.error(detail);
      }
    } finally {
      active.current = null;
      if (!controller.signal.aborted) { setPending(false); onPendingChange(false); }
    }
  };
  return <Button icon={<ReloadOutlined />} loading={pending} disabled={disabled || pending} title={error} onClick={() => modal.confirm({
      title: '重启 Host？', content: 'Host 将读取已保存的配置。重启期间网页和业务接口会暂时断开，恢复后页面会自动连接。',
      okText: '重启 Host', cancelText: '取消', onOk: () => { void restart(); },
    })}>{pending ? '正在重启 Host…' : '重启 Host'}</Button>;
}
