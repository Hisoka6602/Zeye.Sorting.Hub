import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { assertTimeDisplay, inspectTimeDisplay } from './scripts/time-display-guard.mjs';

// 中文说明：开发与本机预览统一转发接口及健康检查，避免健康检查误返回前端 HTML。
const apiTarget = process.env.ZEYE_API_PROXY ?? 'http://127.0.0.1:5078';
const apiProxy = {
  '/api': { target: apiTarget, changeOrigin: true },
  '/health': { target: apiTarget, changeOrigin: true },
  '/hubs': { target: apiTarget, ws: true, changeOrigin: false },
};

export default defineConfig({
  plugins: [{
    name: 'zeye-time-display-guard', enforce: 'pre',
    /** 直接调用 Vite 构建或启动开发服务时同样执行显示规范检查。 */
    configResolved(config) { assertTimeDisplay(config.root); },
    /** 修改源码后的开发编译继续校验，不影响浏览器运行时。 */
    transform(source, id) {
      if (!id.includes('/src/') || !/\.[jt]sx?$/.test(id)) return;
      const issues = inspectTimeDisplay(source, id);
      if (issues.length) this.error(issues.map(issue => `${issue.file}:${issue.line}:${issue.column} ${issue.message}`).join('\n'));
    },
  }, react()],
  // 中文说明：开发API目标可通过ZEYE_API_PROXY配置，默认Host本地5078端口，生产使用同源反向代理或VITE_API_BASE_URL。
  server: { host: '127.0.0.1', port: 4173, proxy: apiProxy },
  preview: { host: '127.0.0.1', port: 4173, proxy: apiProxy },
});
