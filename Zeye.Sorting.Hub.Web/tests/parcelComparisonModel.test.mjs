import assert from 'node:assert/strict';
import test from 'node:test';
import { comparisonIds, comparisonQueries, comparisonValue, comparisonDelta, comparisonIntervals, formatComparisonValue } from '../src/features/parcels/parcelComparisonModel.ts';
import { buildParcelTimingRows } from '../src/features/parcels/parcelTimingModel.ts';

test('多票身份保持64位字符串与输入顺序；超额、空身份和损坏链接不静默裁剪', () => {
  const value = '9223372036854775806';
  assert.deepEqual(comparisonIds(`${value},1,${value}`), { ids: [value, '1'], error: null });
  for (const raw of ['0', '-1', '1,,2', '9223372036854775808', '1,2,3,4,5,6,7,8,9', '01']) assert.ok(comparisonIds(raw).error);
  assert.deepEqual(comparisonIds(null), { ids: [], error: null });
});

test('批量输入保留完整条码内空格，重复输入去重，不截断超长或超额条码', () => {
  assert.deepEqual(comparisonQueries(' A B\n9223372036854775806，C,A B '), ['A B', '9223372036854775806', 'C']);
  assert.throws(() => comparisonQueries(' \n '));
  assert.throws(() => comparisonQueries('x'.repeat(1025)));
  assert.throws(() => comparisonQueries('1,2,3,4,5,6,7,8,9'));
});

test('体积仅换算已保存mm³，缺失体积不从尺寸计算，未知量测不能变成零', () => {
  const item = { weight: 1.001, length: 300, width: 200, height: 100, volume: 5_000_001 };
  assert.equal(comparisonValue(item, 'weight'), 1.001);
  assert.equal(comparisonValue(item, 'volume'), 5.000001);
  assert.equal(comparisonValue({ ...item, volume: null }, 'volume'), null);
  assert.equal(comparisonValue({ ...item, weight: 0 }, 'weight'), 0);
  for (const value of [null, undefined, NaN, Infinity, -1, '1']) assert.equal(comparisonValue({ ...item, weight: value }, 'weight'), null);
});

test('差值相对明确基准，零基准无百分比，微小的真实量测不显示成零', () => {
  assert.deepEqual(comparisonDelta(2.5, 1.25), { difference: 1.25, percent: 100 });
  assert.deepEqual(comparisonDelta(1, 0), { difference: 1, percent: null });
  assert.deepEqual(comparisonDelta(null, 1), { difference: null, percent: null });
  assert.deepEqual(comparisonDelta(1, null), { difference: null, percent: null });
  assert.equal(formatComparisonValue(0), '0');
  assert.equal(formatComparisonValue(null), '—');
  assert.equal(formatComparisonValue(0.0000001), '1.000e-7');
});

const stamp = value => `2026-10-08T10:00:00.${value}`;
const row = overrides => buildParcelTimingRows({ anchorId: '42', beforeCount: 0, afterCount: 0, items: [{ id: '42', barCodes: 'PKG', detectedTime: stamp('0500000'), scannedTime: stamp('2000000'), createdTime: '2026-10-09T10:00:00.000', dischargeTime: stamp('7000000'), completedTime: null, processingRecords: [], apiRequests: [], ...overrides }] })[0];
const call = (start, end) => ({ apiType: 0, requestStatus: 2, requestTime: stamp(start), responseTime: end === null ? null : stamp(end), elapsedMilliseconds: 999 });

test('阶段使用首次真实请求，保留失败、0ms和七位时间精度，不从上报耗时补造响应', () => {
  const intervals = comparisonIntervals(row({ apiRequests: [call('2000000', null), call('4000000', '4500000')] }));
  assert.deepEqual(intervals.map(interval => interval.value), [150, 0, null, 650]);
  assert.equal(comparisonIntervals(row({ apiRequests: [call('3000000', '3000001')] }))[2].value, 0.0001);
  assert.equal(comparisonIntervals(row({ apiRequests: [call('1000000', '1500000')] }))[1].value, -100);
});

test('检测不由Hub入库时间代替，来源标记不可靠的端点不用于阶段耗时', () => {
  assert.equal(comparisonIntervals(row({ detectedTime: null }))[0].value, null);
  const record = { recordId: 'detected', parcelId: '42', stage: 0, occurredAt: stamp('0500000'), hasReliableTimestamp: false, isSuccess: true };
  assert.equal(comparisonIntervals(row({ processingRecords: [record] }))[0].value, null);
  assert.equal(comparisonIntervals(row({ dischargeTime: null, completedTime: stamp('9000000') }))[3].value, null);
});
