import test from 'node:test';
import assert from 'node:assert/strict';
import { formatTrendCount, trendValueLabelIndexes } from '../src/features/parcels/trendChartFormat.ts';

test('每日件数在大数量级使用 K/M/B，较小数量保持原值', () => {
  assert.equal(formatTrendCount(0), '0');
  assert.equal(formatTrendCount(999), '999');
  assert.equal(formatTrendCount(1_000), '1K');
  assert.equal(formatTrendCount(12_500), '12.5K');
  assert.equal(formatTrendCount(100_000), '100K');
  assert.equal(formatTrendCount(125_000), '125K');
  assert.equal(formatTrendCount(1_200_000), '1.2M');
  assert.equal(formatTrendCount(1_200_000_000), '1.2B');
  assert.equal(formatTrendCount(1_234_000), '1.23M');
  assert.equal(formatTrendCount(12.3456), '12.35');
});

test('连续高件量日期按可用宽度减少文字标签并优先保留峰值', () => {
  const counts = Array(31).fill(125_000);
  counts[15] = 999_000;
  for (const width of [220, 640, 914, 1800]) {
    const labels = [...trendValueLabelIndexes(counts, width)].sort((a, b) => a - b);
    assert.ok(labels.includes(15));
    for (let i = 1; i < labels.length; i++) {
      const [previous, current] = [labels[i - 1], labels[i]];
      const required = (formatTrendCount(counts[previous]).length + formatTrendCount(counts[current]).length) * 7.4 / 2 + 8;
      assert.ok((current - previous) * width / counts.length >= required);
    }
  }
  assert.equal(trendValueLabelIndexes(counts, 1800).size, 31);
  assert.equal(trendValueLabelIndexes(counts, 220).size < 31, true);
  assert.deepEqual([...trendValueLabelIndexes([0, 0, 125_000, 0, 0], 220)], [2]);
  assert.equal(trendValueLabelIndexes([], 220).size, 0);
});
