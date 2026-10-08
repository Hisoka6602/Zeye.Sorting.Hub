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

/** 在现有宽度内优先标注较高票数，避免相邻柱顶数字重叠，不扩大图表。 */
export function trendValueLabelIndexes(counts: readonly number[], plotWidth: number): ReadonlySet<number> {
  const indexes = new Set<number>();
  const slotWidth = Math.max(plotWidth, 0) / Math.max(counts.length, 1);
  const labels = counts.map((count, index) => ({ count, index, width: formatTrendCount(count).length * 7.4 }))
    .filter(label => label.count > 0)
    .sort((a, b) => b.count - a.count || a.index - b.index);
  const selected: typeof labels = [];

  for (const label of labels) {
    if (selected.some(other => Math.abs(other.index - label.index) * slotWidth < (other.width + label.width) / 2 + 8)) continue;
    selected.push(label);
    indexes.add(label.index);
  }
  return indexes;
}
