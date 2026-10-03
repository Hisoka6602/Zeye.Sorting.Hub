import { formatNumber } from '../../data/formatNumber.ts';

const compactFormatter = new Intl.NumberFormat('en-US', {
  notation: 'compact',
  compactDisplay: 'short',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

/** 图表标签使用 K/M/B 缩写；精确件数仍由柱形提示展示。 */
export function formatTrendCount(count: number): string {
  return count < 1_000 || !Number.isFinite(count) ? formatNumber(count) : compactFormatter.format(count);
}

/** 为相邻柱顶数字留出间距；高件量的 31 日图表可横向滚动。 */
export function trendPlotWidth(counts: readonly number[]): number {
  const baseWidth = 914;
  const dayCount = Math.max(counts.length, 1);
  let slotWidth = baseWidth / dayCount;
  let previousIndex = -1;
  let previousLabelWidth = 0;

  counts.forEach((count, index) => {
    if (count <= 0) return;
    const labelWidth = formatTrendCount(count).length * 7.4;
    if (previousIndex >= 0) {
      const requiredGap = (previousLabelWidth + labelWidth) / 2 + 8;
      slotWidth = Math.max(slotWidth, requiredGap / (index - previousIndex));
    }
    previousIndex = index;
    previousLabelWidth = labelWidth;
  });

  return Math.max(baseWidth, Math.ceil(slotWidth * dayCount));
}
