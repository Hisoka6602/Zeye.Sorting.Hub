import type { AnalyticsDaily } from '../operations/analyticsModel.ts';

type IntakeDaily = Pick<AnalyticsDaily, 'date' | 'detectedCount' | 'completedCount' | 'exceptionCount' | 'noReadCount'>;

export interface WorkbenchMetricDay extends IntakeDaily {
  exceptionPercent: number | null;
}

export type WorkbenchMetricKey = Exclude<keyof WorkbenchMetricDay, 'date'>;

/** 与卡片使用同一批首次入库包裹；无入库的日期没有可用的异常占比。 */
export function workbenchMetricDays(rows: readonly IntakeDaily[] | undefined, range: { from: string; to: string }): WorkbenchMetricDay[] {
  if (!rows) return [];
  const start = Date.parse(`${range.from}T00:00:00Z`);
  const end = Date.parse(`${range.to}T00:00:00Z`);
  const length = (end - start) / 86_400_000 + 1;
  if (!Number.isInteger(length) || length < 1 || length > 31) return [];
  const byDate = new Map(rows.map(row => [row.date, row]));
  return Array.from({ length }, (_, index) => {
    const date = new Date(start + index * 86_400_000).toISOString().slice(0, 10);
    const row = byDate.get(date);
    const detectedCount = row?.detectedCount ?? 0;
    const exceptionCount = row?.exceptionCount ?? 0;
    return {
      date, detectedCount, exceptionCount,
      completedCount: row?.completedCount ?? 0,
      noReadCount: row?.noReadCount ?? 0,
      exceptionPercent: detectedCount > 0 ? exceptionCount / detectedCount * 100 : null,
    };
  });
}
