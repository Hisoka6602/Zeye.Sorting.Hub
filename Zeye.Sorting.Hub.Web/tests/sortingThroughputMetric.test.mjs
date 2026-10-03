import { test } from 'node:test';
import assert from 'node:assert/strict';
import { sortingThroughputMetric } from '../src/features/parcels/sortingThroughputMetric.ts';

test('actual and theoretical capacity use business descriptions without formulas or timings', () => {
  const metric = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 500, minimumCreationIntervalMilliseconds: 100,
    actualSortingThroughputPerHour: 7200, theoreticalSortingThroughputPerHour: 36000,
    creationIntervalSampleCount: 3,
  });
  assert.equal(metric.actual.value, '7,200');
  assert.equal(metric.actual.note, '当前处理节奏下的小时产能');
  assert.equal(metric.theoretical.value, '36,000');
  assert.equal(metric.theoretical.note, '最佳处理节奏下的预估产能');
  assert.match(metric.hint, /实际时效.*常态处理效率/);
  assert.match(metric.hint, /理论时效.*预估产能/);
  for (const description of [metric.actual.note, metric.theoretical.note, metric.hint]) {
    assert.doesNotMatch(description, /\d|ms|间隔|中位数|÷|=/);
  }
});

test('both capacities preserve fractional intervals and display at most two decimals', () => {
  const metric = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 100.5, minimumCreationIntervalMilliseconds: 100,
    actualSortingThroughputPerHour: 3600000 / 100.5, theoreticalSortingThroughputPerHour: 36000,
    creationIntervalSampleCount: 2,
  });
  assert.equal(metric.actual.value, '35,820.9');
  assert.equal(metric.actual.note, '当前处理节奏下的小时产能');
  assert.equal(metric.theoretical.value, '36,000');
  const slow = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 86400000, minimumCreationIntervalMilliseconds: 86400000,
    actualSortingThroughputPerHour: 1 / 24, theoreticalSortingThroughputPerHour: 1 / 24,
    creationIntervalSampleCount: 1,
  });
  assert.equal(slow.actual.value, '0.04');
  assert.equal(slow.theoretical.value, '0.04');
  const fractional = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 100.56789, minimumCreationIntervalMilliseconds: 99.12345,
    actualSortingThroughputPerHour: 3600000 / 100.56789, theoreticalSortingThroughputPerHour: 3600000 / 99.12345,
    creationIntervalSampleCount: 2,
  });
  assert.equal(fractional.actual.note, '当前处理节奏下的小时产能');
  assert.equal(fractional.theoretical.note, '最佳处理节奏下的预估产能');
  const verySlow = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 31 * 86400000, minimumCreationIntervalMilliseconds: 31 * 86400000,
    actualSortingThroughputPerHour: 1 / (31 * 24), theoreticalSortingThroughputPerHour: 1 / (31 * 24),
    creationIntervalSampleCount: 1,
  });
  assert.equal(verySlow.actual.value, '< 0.01');
  assert.equal(verySlow.theoretical.value, '< 0.01');
});

test('empty or invalid samples never display zero or infinite capacity', () => {
  for (const data of [undefined, null,
    { medianCreationIntervalMilliseconds: null, minimumCreationIntervalMilliseconds: null,
      actualSortingThroughputPerHour: null, theoreticalSortingThroughputPerHour: null, creationIntervalSampleCount: 0 },
    { medianCreationIntervalMilliseconds: 0, minimumCreationIntervalMilliseconds: 0,
      actualSortingThroughputPerHour: Infinity, theoreticalSortingThroughputPerHour: Infinity, creationIntervalSampleCount: 1 },
    { medianCreationIntervalMilliseconds: 500, minimumCreationIntervalMilliseconds: 100,
      actualSortingThroughputPerHour: NaN, theoreticalSortingThroughputPerHour: NaN, creationIntervalSampleCount: 1 },
    { medianCreationIntervalMilliseconds: 500, minimumCreationIntervalMilliseconds: 100,
      actualSortingThroughputPerHour: 7200, theoreticalSortingThroughputPerHour: 36000, creationIntervalSampleCount: 0 },
  ]) {
    const metric = sortingThroughputMetric(data);
    for (const rate of [metric.actual, metric.theoretical]) {
      assert.equal(rate.value, '—');
      assert.equal(rate.note, '暂无有效时效数据');
    }
  }
});

test('an absent or invalid rate never borrows the other metric', () => {
  const missingActual = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 500, minimumCreationIntervalMilliseconds: 100,
    actualSortingThroughputPerHour: null, theoreticalSortingThroughputPerHour: 36000,
    creationIntervalSampleCount: 3,
  });
  assert.equal(missingActual.actual.value, '—');
  assert.equal(missingActual.theoretical.value, '36,000');
  const invalidMinimum = sortingThroughputMetric({
    medianCreationIntervalMilliseconds: 500, minimumCreationIntervalMilliseconds: 0,
    actualSortingThroughputPerHour: 7200, theoreticalSortingThroughputPerHour: 36000,
    creationIntervalSampleCount: 3,
  });
  assert.equal(invalidMinimum.actual.value, '7,200');
  assert.equal(invalidMinimum.theoretical.value, '—');
});
