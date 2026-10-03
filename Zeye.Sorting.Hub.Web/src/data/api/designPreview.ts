import { initialParcels, type Parcel } from '../mock/parcels.ts';
import type { ParcelDetail, ParcelList, ParcelProcessingRecord, ParcelSummary } from './parcelTypes';

/** Reference artwork contains a frozen 2026-09-25 snapshot. This module is loaded only by Vite's design-preview mode. */
const statusCode = { '待分拣': 0, '已完成': 1, '分拣异常': 2 } as const;

function summary(parcel: Parcel): ParcelSummary {
  return {
    sourceInstanceId: null, sourceRunId: null, sourceParcelId: null,
    detectedTime: null, measurementTime: null, targetChuteCode: parcel.target,
    actualChuteCode: parcel.actual === '-' ? null : parcel.actual, taskCode: null,
    volumetricWeightGrams: null, isFallbackChuteAssigned: null,
    isRoutingBlocked: null, sourceExceptionCode: null,
    id: String(parcel.id), createdTime: parcel.scanTime, modifyTime: parcel.scanTime,
    modifyIp: '127.0.0.1', parcelTimestamp: '0', type: 0,
    status: statusCode[parcel.status], exceptionType: parcel.status === '分拣异常' ? 1 : null,
    noReadType: 0, sorterCarrierId: null, segmentCodes: null, lifecycleMilliseconds: null,
    targetChuteId: parcel.target, actualChuteId: parcel.actual === '-' ? null : parcel.actual,
    barCodes: parcel.barcode, weight: parcel.weight,
    requestStatus: parcel.requestStatus === '成功' ? 1 : parcel.requestStatus === '失败' ? 2 : 0,
    bagCode: parcel.bag, workstationName: parcel.workstation, isSticking: false,
    length: parcel.length, width: parcel.width, height: parcel.height,
    volume: parcel.length * parcel.width * parcel.height,
    scannedTime: parcel.scanTime, dischargeTime: parcel.landedTime ?? null,
    completedTime: parcel.landedTime ?? null, hasImages: false, hasVideos: false, coordinate: '',
  };
}

type PreviewRecord = ParcelProcessingRecord & {
  previewTimelineTitle: string;
  previewType: string;
  previewDescription: string;
  previewReference: string;
  previewContent: string;
};

const previewEvents = [
  ['2026-09-25 13:45:06', '已完成', '分拣完成', '包裹已完成分拣，送达目标格口 D04-01。', 'D04-01', '包裹送达目标格口 D04-01'],
  ['2026-09-25 13:32:18', '分拣中', '分拣作业', '包裹进入分拣流程，分配至 工作台 2。', '工作台 2', '分配至 工作台 2'],
  ['2026-09-25 13:20:41', '扫描入线', '扫描记录', '在 分拣线 C03-12 扫码入线。', 'C03-12', '在分拣线 C03-12 扫码入线'],
  ['2026-09-25 10:15:03', '到达场站', '场站接收', '包裹到达中心，等待分拣。', 'A01-01', '包裹到达中心'],
  ['2026-09-25 09:12:34', '创建包裹', '创建记录', '系统创建包裹记录。', '2509250005', '创建包裹'],
] as const;

function previewRecord(event: typeof previewEvents[number], index: number): PreviewRecord {
  const [occurredAt, previewTimelineTitle, previewType, previewDescription, previewReference, previewContent] = event;
  return {
    recordId: `reference-${index + 1}`, sourceInstanceId: 'reference', sourceRunId: '2026-09-25',
    sourceParcelId: '2509250005', parcelId: '2509250005', stage: [6, 5, 3, 0, 0][index],
    occurredAt, recordedAt: occurredAt, partitionTime: occurredAt, isSuccess: true,
    attemptNumber: 1, barcode: 'SF0987654321CN', barcodesJson: null,
    previousCreationGapMilliseconds: null, isSpacingViolation: null, isAwaitingWcsDecision: null,
    workstationName: '工作台 2', weightGrams: null, lengthMm: null, widthMm: null,
    heightMm: null, volumeMm3: null, volumetricWeightGrams: null,
    receivedAt: null, measuredAt: null, hasReliableTimestamp: null, hasReliableFrameBoundary: null,
    correlationId: null, triggerBatch: null, scanSequence: null, messageIdentity: null,
    bindingMode: null, candidateSourceParcelId: null, finalSourceParcelId: null,
    deltaMilliseconds: null, decisionReason: previewContent, fifoRecoveryMode: null,
    provider: null, taskCode: null, targetChuteCode: null, dispatchedChuteCode: null,
    actualChuteCode: null, isFallback: null, isRoutingBlocked: null, exceptionCode: null,
    errorMessage: null, rawPayload: null, requestUrl: null, requestHeaders: null,
    requestBody: null, responseBody: null, responseStatusCode: null, requestAt: null,
    responseAt: null, elapsedMilliseconds: null, imagePath: null, imageCamera: null,
    imageContentHash: null, previewTimelineTitle, previewType, previewDescription,
    previewReference, previewContent,
  };
}

function detail(id: string): ParcelDetail | null {
  const parcel = initialParcels.find(item => String(item.id) === id);
  if (!parcel) return null;
  const result: ParcelDetail = {
    ...summary(parcel), processingRecords: id === '2509250005' ? previewEvents.map(previewRecord) : [],
    barCodeInfos: [], weightInfos: [], apiRequests: [], commandInfos: [], imageInfos: [],
    videoInfos: [], volumeInfo: null, chuteInfo: null, sorterCarrierInfo: null,
    bagInfo: null, deviceInfo: null, grayDetectorInfo: null,
    stickingParcelInfo: null, parcelPositionInfo: null,
  };
  if (id === '2509250005') {
    result.status = 1;
    result.actualChuteCode = 'D04-01';
    result.actualChuteId = 'D04-01';
    result.createdTime = '2026-09-25 13:45:06';
  }
  return result;
}

const daily = [
  ['2026-09-19', 16832, 112, 19.4], ['2026-09-20', 17560, 98, 18.1],
  ['2026-09-21', 18904, 121, 17.8], ['2026-09-22', 17320, 76, 18.2],
  ['2026-09-23', 19441, 103, 18.6], ['2026-09-24', 18775, 96, 18.9],
  ['2026-09-25', 16848, 101, 19.1],
] as const;

// The artwork's distribution percentages do not reconcile with its KPI total;
// retain them only as display values for the isolated visual comparison.
const exceptionTypes = [
  ['地址不详', 182, '28.3%'], ['面单破损', 124, '19.3%'], ['超长超重', 98, '15.2%'],
  ['条码识别失败', 86, '13.4%'], ['禁运品', 62, '9.6%'], ['其他', 55, '8.5%'],
] as const;
const workstations = [
  ['工作台 1', 28560, '22.7%'], ['工作台 2', 24381, '19.4%'],
  ['工作台 3', 21009, '16.7%'], ['工作台 4', 18332, '14.6%'],
  ['工作台 5', 15780, '12.5%'], ['工作台 6', 12450, '9.9%'],
  ['工作台 7', 5168, '4.1%'],
] as const;

function analytics(params: URLSearchParams) {
  const fromDate = params.get('fromDate') ?? '2026-09-19';
  const toDate = params.get('toDate') ?? '2026-09-25';
  const site = params.get('site') ?? '华东分拨中心';
  const line = params.get('line') ?? '全部产线';
  const scale = line === '全部产线' ? 1 : line === '产线 1' ? 0.34 : line === '产线 2' ? 0.33 : 0.33;
  const rows = (site === '华南分拨中心' ? [] : daily.filter(([date]) => date >= fromDate && date <= toDate)).map(([date, originalCount, originalExceptions, averageLifecycleSeconds]) => ({
    date, detectedCount: Math.round(originalCount * scale), completedCount: Math.round(originalCount * scale), exceptionCount: Math.round(originalExceptions * scale),
    noReadCount: 0, chuteMismatchCount: 0, averageLifecycleSeconds,
    lifecycleSampleCount: Math.round(originalCount * scale),
  }));
  const detectedCount = rows.reduce((sum, row) => sum + row.detectedCount, 0);
  const exceptionCount = rows.reduce((sum, row) => sum + row.exceptionCount, 0);
  return {
    fromDate, toDate, detectedCount, completedCount: detectedCount, exceptionCount,
    noReadCount: 0, chuteMismatchCount: 0,
    averageLifecycleSeconds: rows.length ? rows.reduce((sum, row) => sum + row.averageLifecycleSeconds, 0) / rows.length : null,
    // 参考稿没有逐票创建事实，不从生命周期耗时伪造小时产能。
    medianCreationIntervalMilliseconds: null, minimumCreationIntervalMilliseconds: null,
    actualSortingThroughputPerHour: null, theoreticalSortingThroughputPerHour: null, creationIntervalSampleCount: 0,
    daily: rows,
    exceptionTypes: rows.length ? exceptionTypes.map(([name, count, designPreviewPercent], index) => ({ code: `reference-${index}`, name, count: Math.round(count * scale), designPreviewPercent })) : [],
    workstations: rows.length ? workstations.map(([name, count, designPreviewPercent], index) => ({ code: `reference-${index}`, name, count: Math.round(count * scale), designPreviewPercent })) : [],
    workstationsTruncated: false, processingEventCount: 0, failedAttemptCount: 0,
    unboundDwsEventCount: 0,
  };
}

/** Fetch-compatible read model for the artwork. Unknown endpoints fail explicitly. */
export function readDesignPreview<T>(path: string, signal?: AbortSignal): T {
  if (signal?.aborted) throw new DOMException('请求已取消', 'AbortError');
  const url = new URL(path, 'http://design-preview.local');
  let payload: unknown;
  if (url.pathname === '/api/parcels/analytics') payload = analytics(url.searchParams);
  else if (url.pathname === '/api/parcels/processing-records/unbound') payload = [];
  else if (url.pathname === '/api/parcels') {
    const params = url.searchParams;
    const rows = initialParcels.filter(parcel =>
      (!params.get('barCodeKeyword') || parcel.barcode.toLowerCase().includes(params.get('barCodeKeyword')!.toLowerCase())) &&
      (!params.has('status') || statusCode[parcel.status] === Number(params.get('status'))) &&
      (!params.get('bagCode') || parcel.bag.includes(params.get('bagCode')!)) &&
      (!params.get('workstationName') || parcel.workstation.includes(params.get('workstationName')!)) &&
      (!params.get('scannedTimeStart') || parcel.scanTime >= params.get('scannedTimeStart')!.replace('T', ' ')) &&
      (!params.get('scannedTimeEnd') || parcel.scanTime <= params.get('scannedTimeEnd')!.replace('T', ' '))
    );
    const pageNumber = Math.max(1, Number(params.get('pageNumber') ?? 1));
    const pageSize = Math.max(1, Number(params.get('pageSize') ?? 10));
    payload = { items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map(summary), pageNumber, pageSize, totalCount: rows.length, hasTotalCount: true } satisfies ParcelList;
  } else if (/^\/api\/parcels\/\d+$/.test(url.pathname)) payload = detail(url.pathname.split('/').at(-1)!);
  if (payload === undefined || payload === null) throw new Error(`设计预览没有 ${url.pathname} 的样本`);
  return structuredClone(payload) as T;
}
