import http from 'k6/http';
import { group, sleep } from 'k6';
import {
    assertAcceptedResponse,
    createLocalWindow,
    createPerformanceThresholds,
    createReadRequestParams,
    resolveBaseUrl,
    resolveDuration,
    resolveInt
} from './common.js';

/**
 * 审计日志与诊断链路压测选项。
 */
export const options = {
    vus: resolveInt('PERF_VUS', 2),
    duration: resolveDuration('30s'),
    thresholds: createPerformanceThresholds(300, 600)
};

/**
 * 执行审计查询、健康探针与慢查询画像压测。
 */
export default function () {
    const baseUrl = resolveBaseUrl();
    const pageSize = resolveInt('AUDIT_PAGE_SIZE', 50);
    const timeWindow = createLocalWindow(120);

    group('audit-and-diagnostics-query', () => {
        // 步骤 1：查询审计日志分页接口，记录只读热路径基线。
        const auditResponse = http.get(
            `${baseUrl}/api/audit/web-requests?pageNumber=1&pageSize=${pageSize}&startedAtStart=${encodeURIComponent(timeWindow.start)}&startedAtEnd=${encodeURIComponent(timeWindow.end)}`,
            createReadRequestParams('audit-list'));
        assertAcceptedResponse(auditResponse, 'audit list query', [200]);

        // 步骤 2：探测就绪健康检查，确认基础设施治理链路在高频采样下的响应水位。
        const healthResponse = http.get(`${baseUrl}/health/ready`, createReadRequestParams('health-ready'));
        assertAcceptedResponse(healthResponse, 'health ready query', [200]);

        // 步骤 3：读取慢查询画像接口，观测诊断读路径是否稳定。
        const diagnosticsResponse = http.get(`${baseUrl}/api/diagnostics/slow-queries`, createReadRequestParams('slow-query-list'));
        assertAcceptedResponse(diagnosticsResponse, 'slow query profile query', [200]);
    });

    sleep(resolveInt('PERF_SLEEP_SECONDS', 1));
}
