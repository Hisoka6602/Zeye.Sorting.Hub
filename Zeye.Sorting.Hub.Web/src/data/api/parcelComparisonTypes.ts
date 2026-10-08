import type { ParcelTimingParcel } from './parcelTimingTypes';

/** 对比只读取选定包裹的当前快照；kg、mm、mm³保持后端原始单位。 */
export interface ParcelComparisonItem {
  timing: ParcelTimingParcel;
  weight: number | null;
  length: number | null;
  width: number | null;
  height: number | null;
  volume: number | null;
  targetChuteCode: string | null;
  actualChuteCode: string | null;
}

/** 缺失身份显式返回，避免将未入库或已删除包裹视作量测为零。 */
export interface ParcelComparison {
  requestedIds: string[];
  missingIds: string[];
  items: ParcelComparisonItem[];
}
