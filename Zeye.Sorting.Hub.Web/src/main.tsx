import '@ant-design/v5-patch-for-react-19';
import React from 'react';
import ReactDOM from 'react-dom/client';
import { App as AntApp, ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import { createBrowserRouter, useLocation } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import App from './app/App';
import { PageErrorBoundary } from './app/PageErrorBoundary';
import { theme } from './app/theme';
import { typographyCssVariables } from './app/typography';
import 'antd/dist/reset.css';
import './styles.css';
import './app/shell.css';
import './app/typography.css';
import './components/controlAlignment.css';

for (const [name, value] of Object.entries(typographyCssVariables)) {
  document.documentElement.style.setProperty(name, value);
}

/** 根级异常也能提供恢复入口，正常页面导航保留应用外壳状态。 */
function ApplicationRoot() {
  const location = useLocation();
  return <PageErrorBoundary resetKey={location.pathname}><App /></PageErrorBoundary>;
}

// 数据路由提供正式的导航拦截机制，保留现有页面路由和按需加载结构。
const router = createBrowserRouter([{ path: '*', element: <ApplicationRoot /> }]);

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={zhCN} theme={theme} button={{ autoInsertSpace: false }}>
      <AntApp><RouterProvider router={router} /></AntApp>
    </ConfigProvider>
  </React.StrictMode>,
);
