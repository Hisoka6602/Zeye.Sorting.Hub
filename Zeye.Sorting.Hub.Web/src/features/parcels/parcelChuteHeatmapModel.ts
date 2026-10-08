import type { ParcelChuteHeatmapCell } from '../../data/api/parcelAnalysisTypes.ts';

export type ChuteHeatmapKind = 'actual' | 'target';
export type ChuteHeatmapMetric = 'count' | 'mismatchCount' | 'fallbackCount';
export interface ChuteHeatmapGroup {
  key: string; sourceInstanceId: string | null; workstationName: string; count: number; cells: ParcelChuteHeatmapCell[];
}

/** 格口编号仅用于显示和排序；同名编码按来源及工作台隔离，不转换成数字。 */
export function chuteHeatmapScopeKey(cell: Pick<ParcelChuteHeatmapCell, 'sourceInstanceId' | 'workstationName'>): string {
  return JSON.stringify([cell.sourceInstanceId, cell.workstationName]);
}
/** 分组只使用后端独立格口聚合；流向表被截断时也不丢失组内票数。 */
export function groupChuteHeatmapCells(cells: ParcelChuteHeatmapCell[]): ChuteHeatmapGroup[] {
  const groups = new Map<string, ChuteHeatmapGroup>();
  for (const cell of cells) {
    const key = chuteHeatmapScopeKey(cell);
    let group = groups.get(key);
    if (!group) { group = { key, sourceInstanceId: cell.sourceInstanceId, workstationName: cell.workstationName, count: 0, cells: [] }; groups.set(key, group); }
    group.cells.push(cell); group.count += cell.count;
  }
  return [...groups.values()].sort((a, b) => b.count - a.count || a.key.localeCompare(b.key));
}
/** 自然排序保留前导零编码为独立格口；相同数字外观仍以原始字符串稳定排序。 */
export function compareChuteCodes(a: string, b: string): number {
  return a.localeCompare(b, 'zh-CN', { numeric: true }) || a.localeCompare(b, 'zh-CN');
}
/** 线性等宽色阶，已记录格口的真实零票指标使用独立灰色。 */
export function chuteHeatmapLevel(value: number, maximum: number): number {
  return value <= 0 || maximum <= 0 ? 0 : Math.min(5, Math.ceil(value / maximum * 5));
}
/** 点击指标对应的票数，清除上一流向及指标条件；来源不足时不能精确下钻。 */
export function chuteHeatmapDrillFilters(cell: ParcelChuteHeatmapCell, kind: ChuteHeatmapKind,
  metric: ChuteHeatmapMetric): Record<string, string | null> | null {
  if (!cell.sourceInstanceId?.trim() || !cell.workstationName.trim() || !cell.chuteCode.trim() || cell[metric] <= 0) return null;
  return { sourceInstanceId: cell.sourceInstanceId, workstationName: cell.workstationName,
    targetChuteCode: kind === 'target' ? cell.chuteCode : null, actualChuteCode: kind === 'actual' ? cell.chuteCode : null,
    mismatchOnly: metric === 'mismatchCount' ? 'true' : null, fallbackOnly: metric === 'fallbackCount' ? 'true' : null };
}
