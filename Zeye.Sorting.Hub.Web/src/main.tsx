import '@ant-design/v5-patch-for-react-19';
import React from 'react';
import ReactDOM from 'react-dom/client';
import { App as AntApp, ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import { BrowserRouter } from 'react-router';
import App from './app/App';
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

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={zhCN} theme={theme} button={{ autoInsertSpace: false }}>
      <AntApp><BrowserRouter><App /></BrowserRouter></AntApp>
    </ConfigProvider>
  </React.StrictMode>,
);
