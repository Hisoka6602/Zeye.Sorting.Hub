export type ParcelAnalysisView = 'exceptions' | 'duration' | 'chutes';
export type ParcelDurationType = 'completion' | 'dws' | 'routing' | 'sorting' | 'scan-upload' | 'chute-request' | 'landing-report' | 'image-upload' | 'other-api';
export interface ParcelDurationSample {
  key: string; parcelId: string; barCodes: string; workstationName: string; sourceInstanceId: string | null;
  startedAt: string | null; endedAt: string | null; milliseconds: number; timingSource: string;
  provider: string | null; requestUrl: string | null; attemptNumber: number | null; isSuccess: boolean | null;
}
export interface ParcelDurationInterface {
  provider: string | null; requestUrl: string | null; count: number; failedCount: number; averageMilliseconds: number; p95Milliseconds: number;
}
export interface ParcelDurationAnalysis {
  type: ParcelDurationType; name: string; description: string; unit: '票' | '次'; generatedAt: string;
  observedCount: number; unavailableCount: number; sampleCount: number; parcelCount: number; failedCount: number; unknownResultCount: number;
  averageMilliseconds: number | null; medianMilliseconds: number | null; p95Milliseconds: number | null;
  minimumMilliseconds: number | null; maximumMilliseconds: number | null; buckets: ParcelDurationBucket[];
  interfaces: ParcelDurationInterface[]; interfacesTruncated: boolean; filteredCount: number; items: ParcelDurationSample[];
}
export interface ParcelAnalysisGroup { code: number | null; name: string; count: number }
export interface ParcelDurationBucket { minimumMilliseconds: number; maximumMilliseconds: number | null; count: number }
export interface ParcelChuteRoute { sourceInstanceId: string | null; workstationName: string; targetChuteCode: string | null; actualChuteCode: string | null; count: number; fallbackCount: number }
export interface ParcelChuteHeatmapCell {
  sourceInstanceId: string | null; workstationName: string; chuteCode: string; count: number; mismatchCount: number; fallbackCount: number;
}
export interface ParcelChuteHeatmap {
  sampleCount: number; missingCodeCount: number; cells: ParcelChuteHeatmapCell[]; truncated: boolean;
}
export interface ParcelAnalysisParcel {
  id: string; barCodes: string; createdTime: string; scannedTime: string; sourceInstanceId: string | null; workstationName: string;
  status: number; exceptionName: string | null; lifecycleMilliseconds: number | null; targetChuteCode: string | null;
  actualChuteCode: string | null; isFallbackChuteAssigned: boolean | null; isRoutingBlocked: boolean | null;
}
/** 指标使用完整首次入库总体，items 仅代表下钻筛选的当前页。 */
export interface ParcelAnalysis {
  view: ParcelAnalysisView; fromDate: string; toDate: string; parcelCount: number; completedCount: number; exceptionCount: number;
  durationType: ParcelDurationType; durationAnalysis: ParcelDurationAnalysis | null;
  noReadCount: number; routingBlockedCount: number; lifecycleSampleCount: number; averageMilliseconds: number | null;
  medianMilliseconds: number | null; p95Milliseconds: number | null; minimumMilliseconds: number | null; maximumMilliseconds: number | null;
  comparableChuteCount: number; chuteMismatchCount: number; fallbackCount: number; exceptionTypes: ParcelAnalysisGroup[];
  durationBuckets: ParcelDurationBucket[]; chuteRoutes: ParcelChuteRoute[]; chuteRoutesTruncated: boolean;
  actualChuteHeatmap: ParcelChuteHeatmap | null; targetChuteHeatmap: ParcelChuteHeatmap | null;
  filteredCount: number; pageNumber: number; pageSize: number; items: ParcelAnalysisParcel[];
}
