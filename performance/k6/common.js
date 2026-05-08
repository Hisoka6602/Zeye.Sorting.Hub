import http from 'k6/http';
import { check } from 'k6';

/**
 * 解析基础地址。
 * @returns {string} 压测目标服务地址。
 */
export function resolveBaseUrl() {
    return __ENV.BASE_URL || 'http://127.0.0.1:5000';
}

/**
 * 解析整型环境变量。
 * @param {string} name 变量名。
 * @param {number} fallback 默认值。
 * @returns {number} 解析后的数值。
 */
export function resolveInt(name, fallback) {
    const rawValue = __ENV[name];
    if (!rawValue) {
        return fallback;
    }

    const parsedValue = Number.parseInt(rawValue, 10);
    return Number.isNaN(parsedValue) ? fallback : parsedValue;
}

/**
 * 解析持续时间环境变量。
 * @param {string} fallback 默认值。
 * @returns {string} k6 持续时间字符串。
 */
export function resolveDuration(fallback) {
    return __ENV.PERF_DURATION || fallback;
}

/**
 * 构建通用 JSON 请求头。
 * @returns {{ headers: { 'Content-Type': string } }} 请求参数。
 */
export function createJsonRequestParams() {
    return {
        headers: {
            'Content-Type': 'application/json'
        }
    };
}

/**
 * 将本地时间格式化为无 UTC、无 offset 的字符串。
 * @param {Date} date 本地时间对象。
 * @returns {string} 本地时间字符串。
 */
export function formatLocalDateTime(date) {
    const year = date.getFullYear();
    const month = pad(date.getMonth() + 1);
    const day = pad(date.getDate());
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    const seconds = pad(date.getSeconds());
    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
}

/**
 * 构建查询时间窗口。
 * @param {number} minutes 回溯分钟数。
 * @returns {{ start: string, end: string }} 本地时间窗口。
 */
export function createLocalWindow(minutes) {
    const end = new Date();
    const start = new Date(end.getTime() - minutes * 60 * 1000);
    return {
        start: formatLocalDateTime(start),
        end: formatLocalDateTime(end)
    };
}

/**
 * 对 HTTP 响应执行统一断言。
 * @param {http.Response} response HTTP 响应。
 * @param {string} requestName 请求名称。
 * @param {number[]} acceptedStatuses 可接受状态码列表。
 * @returns {boolean} 是否通过断言。
 */
export function assertAcceptedResponse(response, requestName, acceptedStatuses) {
    return check(response, {
        [`${requestName} status accepted`]: current => acceptedStatuses.includes(current.status)
    });
}

/**
 * 构建批量缓冲写入载荷。
 * @param {number} batchSize 单批数量。
 * @param {number} identitySeed 身份种子。
 * @returns {string} 写入请求体 JSON 字符串。
 */
export function createParcelBatchPayload(batchSize, identitySeed) {
    const payload = {
        parcels: []
    };
    const scannedAt = new Date();
    const dischargeAt = new Date(scannedAt.getTime() + 3 * 1000);
    const scannedTime = formatLocalDateTime(scannedAt);
    const dischargeTime = formatLocalDateTime(dischargeAt);

    for (let index = 0; index < batchSize; index += 1) {
        const parcelId = identitySeed + index;
        const parcelTimestamp = createDotNetTicksLiteral(scannedAt.getTime() + index);
        payload.parcels.push({
            id: parcelId,
            parcelTimestamp,
            type: 0,
            barCodes: `BC${parcelId}`,
            weight: 1.25,
            workstationName: 'WS-PR-S',
            scannedTime,
            dischargeTime,
            targetChuteId: 100 + index,
            actualChuteId: 100 + index,
            requestStatus: 0,
            bagCode: `BAG-${identitySeed}`,
            isSticking: false,
            length: 10,
            width: 8,
            height: 6,
            volume: 480,
            hasImages: false,
            hasVideos: false,
            coordinate: '',
            noReadType: 0,
            sorterCarrierId: null,
            segmentCodes: null,
            lifecycleMilliseconds: null
        });
    }

    return JSON.stringify(payload).replace(/"parcelTimestamp":"(\d+)"/g, '"parcelTimestamp":$1');
}

/**
 * 生成与 .NET DateTime.Ticks 语义一致的时间戳字面量。
 * 为避免 JavaScript Number 在 64 位 ticks 上丢失精度，这里保持毫秒级精度并直接输出十进制字符串。
 * @param {number} unixMilliseconds Unix 毫秒时间戳。
 * @returns {string} .NET ticks 十进制字面量。
 */
export function createDotNetTicksLiteral(unixMilliseconds) {
    const dotNetEpochMillisecondsOffset = 62135596800000;
    // .NET Ticks 每毫秒对应 10000 ticks，这里保持毫秒精度并补齐四位零以对齐 .NET 语义。
    const ticksPerMillisecondLiteral = '0000';
    return `${unixMilliseconds + dotNetEpochMillisecondsOffset}${ticksPerMillisecondLiteral}`;
}

/**
 * 生成两位补零字符串。
 * @param {number} value 数值。
 * @returns {string} 补零后的字符串。
 */
function pad(value) {
    return value.toString().padStart(2, '0');
}
