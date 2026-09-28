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
 * Parcel 查询压测选项。
 */
export const options = {
    vus: resolveInt('PERF_VUS', 4),
    duration: resolveDuration('30s'),
    thresholds: createPerformanceThresholds(300, 600)
};

/**
 * 执行 Parcel 游标分页与普通分页压测。
 */
export default function () {
    const baseUrl = resolveBaseUrl();
    const pageSize = resolveInt('PARCEL_PAGE_SIZE', 50);
    const timeWindow = createLocalWindow(120);

    group('parcel-cursor-query', () => {
        // 步骤 1：先访问游标分页链路，验证高频列表默认读取性能。
        const cursorResponse = http.get(
            `${baseUrl}/api/parcels/cursor?pageSize=${pageSize}&scannedTimeStart=${encodeURIComponent(timeWindow.start)}&scannedTimeEnd=${encodeURIComponent(timeWindow.end)}`,
            createReadRequestParams('parcel-cursor'));
        assertAcceptedResponse(cursorResponse, 'parcel cursor query', [200]);

        // 步骤 2：再访问普通分页链路，形成同一窗口下的双路径基线对照。
        const pageResponse = http.get(
            `${baseUrl}/api/parcels?pageNumber=1&pageSize=${pageSize}&scannedTimeStart=${encodeURIComponent(timeWindow.start)}&scannedTimeEnd=${encodeURIComponent(timeWindow.end)}`,
            createReadRequestParams('parcel-page'));
        assertAcceptedResponse(pageResponse, 'parcel page query', [200]);
    });

    sleep(resolveInt('PERF_SLEEP_SECONDS', 1));
}
