import assert from 'node:assert/strict';
import test from 'node:test';
import { inclusiveLocalDayRange, localDateTimeFormat, localTime } from '../src/data/api/operationalTypes.ts';
import { parseTimingTime } from '../src/features/parcels/parcelTimingModel.ts';

test('包含式整日查询覆盖最后一秒和最后100ns，下一天零点不进入窗口', () => {
  const range = inclusiveLocalDayRange('2026-10-28', '2026-10-28');
  const start = parseTimingTime(range.start), end = parseTimingTime(range.end);
  assert.equal(start, parseTimingTime('2026-10-28T00:00:00'));
  for (const value of ['2026-10-28T23:59:59.001', '2026-10-28T23:59:59.9999999']) {
    const time = parseTimingTime(value);
    assert.ok(time >= start && time <= end);
  }
  assert.ok(parseTimingTime('2026-10-29T00:00:00') > end);
  assert.deepEqual(inclusiveLocalDayRange(undefined, '2026-10-28'), { start: undefined, end: range.end });
  assert.deepEqual(inclusiveLocalDayRange('2026-10-28'), { start: range.start, end: undefined });
});

test('七位小数按本地钟面截断到三位，不四舍五入或跨越日期边界', () => {
  assert.equal(localTime('2026-10-06T10:24:31.0612630'), '2026-10-06 10:24:31.061');
  assert.equal(localTime('2026-10-06 23:59:59.9999999'), '2026-10-06 23:59:59.999');
  assert.equal(localTime('2026-10-06T00:00:00.0000001'), '2026-10-06 00:00:00.000');
});

test('整秒及短小数补足三位，重复格式化稳定', () => {
  for (const [input, expected] of [['2026-10-06T10:24:31', '2026-10-06 10:24:31.000'], ['2026-10-06 10:24:31.1', '2026-10-06 10:24:31.100'], ['2026-10-06T10:24:31.12', '2026-10-06 10:24:31.120'], ['2026-10-06 10:24:31.123', '2026-10-06 10:24:31.123']]) {
    assert.equal(localTime(input), expected);
    assert.equal(localTime(localTime(input)), expected);
  }
  assert.equal(localDateTimeFormat, 'YYYY-MM-DD HH:mm:ss.SSS');
});

test('缺省、纯日期、配置时间、耗时和完整报文不被当作日期时间改写', () => {
  for (const value of [undefined, null, '']) assert.equal(localTime(value), '-');
  for (const value of ['2026-10-06', '10:24:31', '尚未接入', '3.0612630 ms', '{"occurredAt":"2026-10-06T10:24:31.0612630"}']) assert.equal(localTime(value), value);
});

test('展示截断不改写原始精度，相同毫秒内的轨迹仍可按完整时间排序', () => {
  const first = '2026-10-06T10:24:31.0612630', second = '2026-10-06T10:24:31.0612631';
  const values = Object.freeze([second, first]);
  assert.deepEqual(values.map(localTime), ['2026-10-06 10:24:31.061', '2026-10-06 10:24:31.061']);
  assert.deepEqual([...values].sort(), [first, second]);
  assert.deepEqual(values, [second, first]);
});
