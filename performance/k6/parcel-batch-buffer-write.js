import http from 'k6/http';
import { sleep } from 'k6';
import {
    assertAcceptedResponse,
    createJsonRequestParams,
    createParcelBatchPayload,
    createPerformanceThresholds,
    resolveBaseUrl,
    resolveDuration,
    resolveInt
} from './common.js';

/**
 * Parcel 批量缓冲写入压测选项。
 */
export const options = {
    vus: resolveInt('PERF_VUS', 2),
    duration: resolveDuration('30s'),
    thresholds: createPerformanceThresholds(500, 1000)
};

/**
 * 执行 Parcel 批量缓冲写入压测。
 */
export default function () {
    const baseUrl = resolveBaseUrl();
    const batchSize = resolveInt('PARCEL_BATCH_SIZE', 10);
    const identitySeed = (__VU * 1_000_000) + (__ITER * batchSize) + 1;
    const payload = createParcelBatchPayload(batchSize, identitySeed);
    const response = http.post(
        `${baseUrl}/api/admin/parcels/batch-buffer`,
        payload,
        {
            ...createJsonRequestParams(),
            tags: {
                endpoint: 'parcel-batch-buffer'
            }
        });

    assertAcceptedResponse(response, 'parcel batch buffer write', [200]);
    sleep(resolveInt('PERF_SLEEP_SECONDS', 1));
}
