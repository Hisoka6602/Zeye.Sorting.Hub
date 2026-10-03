import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// 中文说明：开发与本机预览统一转发接口及健康检查，避免健康检查误返回前端 HTML。
const apiTarget = process.env.ZEYE_API_PROXY ?? 'http://127.0.0.1:5078';
const apiProxy = {
  '/api': { target: apiTarget, changeOrigin: true },
  '/health': { target: apiTarget, changeOrigin: true },
};

export default defineConfig({
  plugins: [react()],
  // 中文说明：开发API目标可通过ZEYE_API_PROXY配置，默认Host本地5078端口，生产使用同源反向代理或VITE_API_BASE_URL。
  server: { host: '127.0.0.1', port: 4173, proxy: apiProxy },
  preview: { host: '127.0.0.1', port: 4173, proxy: apiProxy },
});
