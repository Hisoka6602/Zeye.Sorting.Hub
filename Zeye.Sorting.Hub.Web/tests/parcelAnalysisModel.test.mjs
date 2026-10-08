import assert from 'node:assert/strict';
import test from 'node:test';
import { analysisApiPath, analysisDateOffset, analysisPercent, analysisValidation, chuteComparison, durationBucketLabel,
  durationTypes, readAnalysisFilters, validAnalysisDate } from '../src/features/parcels/parcelAnalysisModel.ts';

const filters = (query = '') => readAnalysisFilters(new URLSearchParams(query), '2026-10-07');
test('耗时类型保存在链接中，默认完成耗时，未知类型不发请求', () => {
  assert.equal(filters().durationType, 'completion');
  for (const type of durationTypes) {
    const current = filters(`durationType=${type.key}&sourceInstanceId=A&minimumMilliseconds=0&maximumMilliseconds=100&pageNumber=2`);
    assert.equal(analysisValidation(current, 'duration'), null);
    const query = new URL(analysisApiPath(current, 'duration'), 'http://local').searchParams;
    assert.equal(query.get('durationType'), type.key);
    assert.equal(query.get('minimumMilliseconds'), '0');
    assert.equal(query.get('sourceInstanceId'), 'A');
    assert.equal(query.get('pageNumber'), '2');
    assert.equal(new URL(analysisApiPath(current, 'exceptions'), 'http://local').searchParams.get('durationType'), null);
    assert.equal(new URL(analysisApiPath(current, 'chutes'), 'http://local').searchParams.get('durationType'), null);
  }
  assert.ok(analysisValidation(filters('durationType=unknown'), 'duration'));
});
test('本地日期拒绝日历溢出、UTC和时区偏移，保留闰年与历史日期', () => {
  for (const value of ['2026-02-29', '2026-02-30', '2026-13-01', '0000-01-01', '2026-10-07Z', '2026-10-07T00:00:00', '2026-10-07+08:00']) assert.equal(validAnalysisDate(value), false, value);
  for (const value of ['2024-02-29', '2026-10-07', '0001-01-01']) assert.equal(validAnalysisDate(value), true, value);
  assert.equal(analysisDateOffset('2024-03-01', -1), '2024-02-29');
  assert.equal(analysisDateOffset('2026-01-01', -6), '2025-12-26');
});
test('日期总体默认本地近7天，31天包含两端，跨年范围不受时区影响', () => {
  assert.equal(filters().fromDate, '2026-10-01');
  assert.equal(filters().toDate, '2026-10-07');
  assert.equal(analysisValidation(filters('fromDate=2026-09-07&toDate=2026-10-07'), 'duration'), null);
  assert.match(analysisValidation(filters('fromDate=2026-09-06&toDate=2026-10-07'), 'duration'), /31/);
  assert.match(analysisValidation(filters('fromDate=2026-10-08'), 'exceptions'), /结束/);
});
test('非法分页和非整数耗时不发请求，区间左闭右开且允许真实0 ms', () => {
  for (const pageNumber of ['0', '-1', '1.5', '100001', '9007199254740993']) assert.ok(analysisValidation(filters('pageNumber=' + pageNumber), 'chutes'));
  assert.equal(analysisValidation(filters('minimumMilliseconds=0&maximumMilliseconds=500'), 'duration'), null);
  for (const query of ['minimumMilliseconds=-1', 'minimumMilliseconds=0.1', 'minimumMilliseconds=500&maximumMilliseconds=500', 'maximumMilliseconds=0']) assert.ok(analysisValidation(filters(query), 'duration'));
  assert.ok(analysisValidation(filters('issue=unknown'), 'exceptions'));
});
test('异常下钻只筛选当前问题范围，保留总体来源和日期，NoRead不附带异常类型', () => {
  const current = filters('sourceInstanceId=A&workstationName=%E4%B8%80%E5%8F%B7%E7%BA%BF&issue=noread&exceptionType=13&pageNumber=2&minimumMilliseconds=500&mismatchOnly=true');
  const query = new URL(analysisApiPath(current, 'exceptions'), 'http://local').searchParams;
  assert.equal(query.get('sourceInstanceId'), 'A');
  assert.equal(query.get('workstationName'), '一号线');
  assert.equal(query.get('issue'), 'noread');
  assert.equal(query.get('pageNumber'), '2');
  assert.equal(query.get('exceptionType'), null);
  assert.equal(query.get('minimumMilliseconds'), null);
  assert.equal(query.get('mismatchOnly'), null);
});
test('格口下钻保留原始非数字编码、64位外观编码以及实例，耗时不附带格口条件', () => {
  const current = filters('sourceInstanceId=A&targetChuteCode=9007199254741005&actualChuteCode=X01&mismatchOnly=true&minimumMilliseconds=0&maximumMilliseconds=500');
  const chutes = new URL(analysisApiPath(current, 'chutes'), 'http://local').searchParams;
  assert.equal(chutes.get('targetChuteCode'), '9007199254741005');
  assert.equal(chutes.get('actualChuteCode'), 'X01');
  assert.equal(chutes.get('mismatchOnly'), 'true');
  const rawCodes = new URL(analysisApiPath({ ...current, targetChuteCode: ' x01 ' }, 'chutes'), 'http://local').searchParams;
  assert.equal(rawCodes.get('targetChuteCode'), ' x01 ');
  const duration = new URL(analysisApiPath(current, 'duration'), 'http://local').searchParams;
  assert.equal(duration.get('minimumMilliseconds'), '0');
  assert.equal(duration.get('maximumMilliseconds'), '500');
  assert.equal(duration.get('targetChuteCode'), null);
});
test('占比使用完整指定总体，空总体不制造0%，区间名称保留边界', () => {
  assert.equal(analysisPercent(0, 100), '0%');
  assert.equal(analysisPercent(0, 0), '—');
  assert.equal(analysisPercent(3, 10), '30%');
  assert.equal(durationBucketLabel({ minimumMilliseconds: 0, maximumMilliseconds: 500 }), '0–<500 ms');
  assert.equal(durationBucketLabel({ minimumMilliseconds: 5000, maximumMilliseconds: null }), '≥ 5,000 ms');
});
test('格口比对忽略大小写和首尾空格，缺失、空白与数字外观编码不做猜测', () => {
  assert.equal(chuteComparison({ targetChuteCode: ' x01 ', actualChuteCode: 'X01' }), 'match');
  assert.equal(chuteComparison({ targetChuteCode: 'X01', actualChuteCode: 'X02' }), 'mismatch');
  assert.equal(chuteComparison({ targetChuteCode: '0010', actualChuteCode: '10' }), 'mismatch');
  assert.equal(chuteComparison({ targetChuteCode: null, actualChuteCode: 'X02' }), 'unknown');
  assert.equal(chuteComparison({ targetChuteCode: ' ', actualChuteCode: 'X02' }), 'unknown');
});
