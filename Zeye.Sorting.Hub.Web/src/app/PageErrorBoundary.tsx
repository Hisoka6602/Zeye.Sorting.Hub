import { Button, Result } from 'antd';
import { Component, type ErrorInfo, type ReactNode } from 'react';

/** 静态入口提前安装的恢复能力，入口与懒加载模块共用同一刷新上限。 */
interface PageLoadRecovery {
  /** 识别模块资源故障，普通业务异常不会自动刷新。 */
  isAssetError: (error: unknown) => boolean;
  /** 每个入口版本在当前标签页最多自动刷新一次。 */
  recover: (error: unknown, knownAssetFailure?: boolean) => boolean;
  /** 显式重试允许重新尝试一次自动恢复。 */
  retry: () => void;
}

declare global {
  interface Window {
    /** 不依赖 React 或页面分块的静态加载恢复入口。 */
    zeyePageLoadRecovery?: PageLoadRecovery;
  }
}

/** 页面错误状态及导航恢复标识。 */
interface PageErrorBoundaryProps {
  /** 被保护的应用或路由内容。 */
  children: ReactNode;
  /** 切换页面后清除失败状态，不重新挂载正常工作的外壳。 */
  resetKey: string;
}

/** 保留页面外壳并显示可重试的失败界面，避免渲染或懒加载异常清空整个页面。 */
export class PageErrorBoundary extends Component<PageErrorBoundaryProps, { error: Error | null }> {
  /** 当前捕获的页面异常；正常导航不会生成额外错误状态。 */
  override state: { error: Error | null } = { error: null };

  /** 失败后停止渲染异常子树，普通值异常也转换为可识别的错误对象。 */
  static getDerivedStateFromError(error: unknown) {
    return { error: error instanceof Error ? error : new Error(String(error ?? '页面加载异常')) };
  }

  /** 输出浏览器诊断并使用统一恢复入口；普通组件异常保留在可重试界面。 */
  override componentDidCatch(error: Error, info: ErrorInfo) {
    if (!window.zeyePageLoadRecovery?.recover(error))
      console.error('页面显示失败', error, info.componentStack);
  }

  /** 仅在失败状态下响应导航变化，保留正常页面的布局、菜单及登录状态。 */
  override componentDidUpdate(previous: PageErrorBoundaryProps) {
    if (this.state.error && previous.resetKey !== this.props.resetKey) this.setState({ error: null });
  }

  /** 手动重试重新请求入口文件，恢复脚本不可用时仍可执行正常刷新。 */
  private retry = () => {
    if (window.zeyePageLoadRecovery) window.zeyePageLoadRecovery.retry();
    else window.location.reload();
  };

  /** 使用现有结果控件提供明确状态和操作，不显示内部异常或业务数据。 */
  override render() {
    if (!this.state.error) return this.props.children;
    const assetFailure = window.zeyePageLoadRecovery?.isAssetError(this.state.error) === true;
    return <div role="alert"><Result status="warning" title={assetFailure ? '页面资源暂时无法加载' : '页面暂时无法显示'}
      subTitle={assetFailure ? '资源可能已更新或网络暂时不可用，请重新加载后重试。' : '页面发生异常，请重新加载后重试。'}
      extra={<Button type="primary" onClick={this.retry}>重新加载</Button>} /></div>;
  }
}
