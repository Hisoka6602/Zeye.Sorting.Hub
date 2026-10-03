import { test } from 'node:test';
import assert from 'node:assert/strict';
import { workbenchMetricDays } from '../src/features/parcels/workbenchMetricDays.ts';

const day = (date, detectedCount, completedCount = 0, exceptionCount = 0, noReadCount = 0) =>
  ({ date, detectedCount, completedCount, exceptionCount, noReadCount });

test('mini charts use intake cohorts in date order and include quiet days inside the selected range', () => {
  const days = workbenchMetricDays([
    day('2026-10-03', 10, 7, 2, 1),
    day('2026-09-30', 99, 99),
    day('2026-10-01', 8, 5, 1, 2),
    day('2026-10-04', 50, 50),
  ], { from: '2026-10-01', to: '2026-10-03' });
  assert.deepEqual(days.map(row => row.date), ['2026-10-01', '2026-10-02', '2026-10-03']);
  assert.deepEqual(days.map(row => row.detectedCount), [8, 0, 10]);
  assert.deepEqual(days.map(row => row.completedCount), [5, 0, 7]);
  assert.deepEqual(days.map(row => row.exceptionCount), [1, 0, 2]);
  assert.deepEqual(days.map(row => row.noReadCount), [2, 0, 1]);
  assert.equal(days.reduce((sum, row) => sum + row.completedCount, 0), 12);
});

test('exception share stays unknown on days without intake and is zero only with a real denominator', () => {
  const days = workbenchMetricDays([
    day('2026-10-01', 4, 2, 1),
    day('2026-10-03', 6, 6),
  ], { from: '2026-10-01', to: '2026-10-03' });
  assert.deepEqual(days.map(row => row.exceptionPercent), [25, null, 0]);
});

test('empty and unavailable responses do not fabricate daily rate samples', () => {
  const range = { from: '2026-10-01', to: '2026-10-03' };
  assert.deepEqual(workbenchMetricDays(undefined, range), []);
  const empty = workbenchMetricDays([], range);
  assert.equal(empty.length, 3);
  assert.ok(empty.every(row => row.detectedCount === 0 && row.exceptionPercent === null));
});

test('single-day and leap-day ranges keep exact calendar dates without invented intermediate samples', () => {
  const single = workbenchMetricDays([day('2026-10-03', 1)], { from: '2026-10-03', to: '2026-10-03' });
  assert.equal(single.length, 1);
  assert.equal(single[0].detectedCount, 1);
  const leap = workbenchMetricDays([day('2024-02-29', 2)], { from: '2024-02-28', to: '2024-03-01' });
  assert.deepEqual(leap.map(row => row.date), ['2024-02-28', '2024-02-29', '2024-03-01']);
});
