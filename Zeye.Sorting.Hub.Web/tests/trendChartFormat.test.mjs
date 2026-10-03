import test from 'node:test';
import assert from 'node:assert/strict';
import { formatTrendCount, trendPlotWidth } from '../src/features/parcels/trendChartFormat.ts';

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

test('连续高件量日期会增宽绘图区，避免 31 个柱顶标签重叠', () => {
  assert.equal(trendPlotWidth(Array(31).fill(12)), 914);
  assert.ok(trendPlotWidth(Array(31).fill(125_000)) > 914);
  assert.equal(trendPlotWidth([0, 0, 125_000, 0, 0]), 914);
});
