import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // 中文说明：开发API目标可通过ZEYE_API_PROXY配置，默认Host本地5078端口，生产使用同源反向代理或VITE_API_BASE_URL。
  server: { host: '127.0.0.1', port: 4173, proxy: { '/api': { target: process.env.ZEYE_API_PROXY ?? 'http://127.0.0.1:5078', changeOrigin: true } } },
  preview: { host: '127.0.0.1', port: 4173 },
});
