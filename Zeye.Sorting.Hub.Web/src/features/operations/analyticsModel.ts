import { formatNumber } from '../../data/formatNumber.ts';
export interface Analytics {
  fromDate: string;
  toDate: string;
  detectedCount: number;
  completedCount: number;
  exceptionCount: number;
  noReadCount: number;
  chuteMismatchCount: number;
  averageLifecycleSeconds: number | null;
  daily: AnalyticsDaily[];
  exceptionTypes: AnalyticsDistribution[];
  workstations: AnalyticsDistribution[];
  workstationsTruncated: boolean;
  processingEventCount: number;
  failedAttemptCount: number;
  unboundDwsEventCount: number;
}

export interface AnalyticsDaily {
  date: string;
  detectedCount: number;
  completedCount: number;
  exceptionCount: number;
  noReadCount: number;
  chuteMismatchCount: number;
  averageLifecycleSeconds: number | null;
  lifecycleSampleCount: number;
}

export interface AnalyticsDistribution {
  code: string | null;
  name: string;
  count: number;
  designPreviewPercent?: string;
}

/** 无分母时保留未知语义；工作台与异常分布分别使用自己的总体。 */
export function analyticsPercent(count: number, total: number): string {
  return total > 0 ? `${formatNumber(count / total * 100)}%` : '—';
}

/** 件数轴从零开始，保留整数刻度并为柱顶标签留白。 */
export function analyticsCountAxis(maximum: number): { upper: number; ticks: number[] } {
  const roughStep = Math.max(maximum * 1.15 / 4, 1);
  const magnitude = 10 ** Math.floor(Math.log10(roughStep));
  const normalized = roughStep / magnitude;
  const factor = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
  const step = factor * magnitude;
  return { upper: step * 4, ticks: [0, step, step * 2, step * 3, step * 4] };
}
