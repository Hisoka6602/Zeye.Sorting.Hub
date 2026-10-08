/** 重量单位克，物理体积单位立方厘米；未知值不补零。 */
export interface DwsMeasurementMetric {
  count: number; minimum: number | null; maximum: number | null; median: number | null; spread: number | null; spreadPercent: number | null;
  average: number | null; p95: number | null;
  reference: number | null; maximumReferenceDeviation: number | null; maximumReferenceDeviationPercent: number | null;
}
export interface DwsConsistencyGroup {
  barcode: string; measurementCount: number; parcelCount: number; sourceCount: number; firstMeasuredAt: string | null; lastMeasuredAt: string | null;
  weight: DwsMeasurementMetric; volume: DwsMeasurementMetric; scanDuration: DwsMeasurementMetric;
  weightDeviates: boolean; volumeDeviates: boolean; scanDurationDeviates: boolean; referenceDeviates: boolean; isComparable: boolean;
}
export interface DwsMeasurementSample {
  key: string; messageIdentity: string; recordId: string; barcode: string; sourceInstanceId: string; sourceRunId: string;
  parcelId: string | null; workstationName: string | null; sourceParcelId: string | null; measuredAt: string | null; occurredAt: string;
  scanStartedAt: string | null; scanCompletedAt: string | null; scanDurationMilliseconds: number | null;
  scanTimingBasis: string | null; scanTimingUnavailableReason: string | null;
  weightGrams: number | null; lengthMm: number | null; widthMm: number | null; heightMm: number | null; volumeCm3: number | null; bindingConfirmed: boolean;
}
export interface DwsConsistencySource {
  sourceInstanceId: string; workstationName: string | null; measurementCount: number; repeatedBarcodeCount: number; deviationBarcodeCount: number;
  medianWeightDeviationPercent: number | null; medianVolumeDeviationPercent: number | null;
  medianScanDurationDeviationPercent: number | null;
}
export interface DwsConsistencyDetail {
  summary: DwsConsistencyGroup; measurementCount: number; items: DwsMeasurementSample[]; trend: DwsMeasurementSample[];
  trendTruncated: boolean; sources: DwsConsistencySource[];
  scanTrend: DwsMeasurementSample[]; scanTrendTruncated: boolean;
}
export interface ParcelDwsConsistency {
  generatedAt: string; measurementCount: number; repeatedBarcodeCount: number; comparableBarcodeCount: number; deviationBarcodeCount: number;
  scanTimingSampleCount: number; missingScanTimingCount: number;
  missingIdentityCount: number; conflictingMeasurementCount: number; missingBarcodeCount: number; duplicateRecordCount: number;
  filteredCount: number; items: DwsConsistencyGroup[]; sources: DwsConsistencySource[]; sourcesTruncated: boolean; detail: DwsConsistencyDetail | null;
}
