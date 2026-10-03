import assert from 'node:assert/strict';
import test from 'node:test';
import { analyticsCountAxis, analyticsPercent } from '../src/features/operations/analyticsModel.ts';

test('报表占比使用指定总体，无分母时保留未知，真实零仍显示零', () => {
  assert.equal(analyticsPercent(4, 12), '33.33%');
  assert.equal(analyticsPercent(20, 72), '27.78%');
  assert.equal(analyticsPercent(0, 12), '0%');
  assert.equal(analyticsPercent(3, 12), '25%');
  assert.equal(analyticsPercent(1, 8), '12.5%');
  assert.equal(analyticsPercent(0, 0), '—');
});

test('件量坐标轴从零开始，零和小数量使用整数刻度', () => {
  for (const value of [0, 1, 2, 14]) {
    const axis = analyticsCountAxis(value);
    assert.equal(axis.ticks[0], 0);
    assert.ok(axis.upper > value);
    assert.ok(axis.ticks.every(Number.isInteger));
    assert.equal(axis.ticks.at(-1), axis.upper);
    assert.ok(axis.ticks.every((tick, index) => index === 0 || tick > axis.ticks[index - 1]));
  }
});

test('大件量坐标轴包含全部数值并保留标签空间', () => {
  for (const value of [999, 10_000, 1_300_000, 31_000_000]) {
    const { upper, ticks } = analyticsCountAxis(value);
    assert.ok(upper >= value * 1.15);
    assert.ok(Number.isFinite(upper));
    assert.ok(ticks.every(tick => Number.isFinite(tick) && tick >= 0));
  }
});
