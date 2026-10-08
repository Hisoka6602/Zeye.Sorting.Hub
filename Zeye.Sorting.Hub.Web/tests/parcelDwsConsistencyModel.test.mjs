import assert from 'node:assert/strict';
import test from 'node:test';
import { readDwsFilters, dwsValidation, dwsQueryParams, dwsApiPath, dwsPlotSamples, dwsValue, dwsScanReason } from '../src/features/parcels/parcelDwsConsistencyModel.ts';

const defaults = () => readDwsFilters(new URLSearchParams(), '2026-10-08');
test('DWS 日期、阈值和明细选择可从 URL 恢复，条码不折叠大小写', () => {
  const original = { ...defaults(), barcode: ' AbC-001 ', detailBarcode: 'AbC-001', measurementPageNumber: '3',
    referenceWeightGrams: '1000.125', weightToleranceGrams: '0', onlyDeviations: true, sourceInstanceId: 'source-01' };
  assert.equal(dwsValidation(original), null);
  const restored = readDwsFilters(dwsQueryParams(original), '2026-11-01');
  assert.equal(restored.barcode, 'AbC-001'); assert.equal(restored.referenceWeightGrams, '1000.125');
  assert.equal(restored.measurementPageNumber, '3'); assert.equal(restored.fromDate, '2026-10-02'); assert.equal(restored.onlyDeviations, true);
  assert.ok(dwsApiPath(restored).startsWith('/api/parcels/dws-consistency?'));
  assert.ok(!dwsQueryParams(defaults()).has('referenceWeightGrams'));
});
test('扫码阈值和 P95 排序可从 URL 恢复，非法扫码阈值不能提交', () => {
  const filters = { ...defaults(), scanDurationToleranceMilliseconds: '12.125', scanDurationTolerancePercent: '5.5', sortBy: 'scan-duration-p95' };
  assert.equal(dwsValidation(filters), null);
  const restored = readDwsFilters(dwsQueryParams(filters), '2026-10-09');
  assert.equal(restored.scanDurationToleranceMilliseconds, '12.125'); assert.equal(restored.scanDurationTolerancePercent, '5.5');
  assert.equal(restored.sortBy, 'scan-duration-p95'); assert.equal(dwsValidation({ ...filters, sortBy: 'scan-duration' }), null);
  assert.equal(defaults().scanDurationToleranceMilliseconds, '50'); assert.equal(defaults().scanDurationTolerancePercent, '20');
  for (const value of ['-1', 'NaN', 'Infinity', '', '1000000001']) assert.notEqual(dwsValidation({ ...filters, scanDurationToleranceMilliseconds: value }), null);
});
test('扫码趋势独立使用有效条码接收时间，不借用设备测量或 Hub 事件时间', () => {
  const rows = [{ key: 'zero', scanCompletedAt: '2026-10-08T10:00:00', scanDurationMilliseconds: 0, measuredAt: null },
    { key: 'fraction', scanCompletedAt: '2026-10-08T10:00:01', scanDurationMilliseconds: 123.4567, measuredAt: '2026-10-08T08:00:00' },
    { key: 'missing-end', scanCompletedAt: null, scanDurationMilliseconds: 100, measuredAt: '2026-10-08T10:00:02', occurredAt: '2026-10-08T10:00:02' },
    { key: 'missing-value', scanCompletedAt: '2026-10-08T10:00:03', scanDurationMilliseconds: null },
    { key: 'negative', scanCompletedAt: '2026-10-08T10:00:04', scanDurationMilliseconds: -10 }];
  const points = dwsPlotSamples(rows, 'scanDurationMilliseconds');
  assert.deepEqual(points.map(point => point.sample.key), ['zero', 'fraction']); assert.equal(points[0].value, 0);
  assert.equal(points[1].timestamp, '2026-10-08T10:00:01'); assert.equal(points[1].value, 123.4567);
  assert.equal(dwsValue(points[1].value, 'ms'), '123.4567 ms'); assert.equal(rows.length, 5);
});
test('扫码缺失原因用明确业务文字展示，未知原因保持不可用', () => {
  assert.equal(dwsScanReason('reversed-scan-time'), '条码接收早于检测时间');
  assert.equal(dwsScanReason('missing-parcel-identity'), '包裹关联不完整');
  assert.equal(dwsScanReason('conflicting-parcel-identity'), '来源包裹关联存在冲突');
  assert.equal(dwsScanReason('conflicting-detection-time'), '检测时间存在冲突');
  assert.equal(dwsScanReason(null), '未取得有效扫码时间'); assert.equal(dwsScanReason('unknown'), '未取得有效扫码时间');
  assert.equal(dwsScanReason('__proto__'), '未取得有效扫码时间');
});
test('DWS 查询拒绝非法日历、过宽范围、负阈值、非有限值和不明确的标准值', () => {
  for (const changes of [{ fromDate: '2026-02-30' }, { fromDate: '2026-10-09' }, { fromDate: '2026-09-01' },
    { toDate: '2026-10-08Z' }, { weightToleranceGrams: '-1' }, { volumeTolerancePercent: 'NaN' },
    { referenceWeightGrams: '1000' }, { barcode: 'A', referenceVolumeCm3: '0' }, { measurementPageNumber: '0' },
    { sortBy: 'arbitrary' }, { barcode: 'A\nB' }]) assert.notEqual(dwsValidation({ ...defaults(), ...changes }), null, JSON.stringify(changes));
  assert.equal(dwsValidation({ ...defaults(), fromDate: '2026-09-08' }), null);
});
test('DWS 趋势保留真实零量测，缺少时间或指标的样本仅留在原始表格', () => {
  const rows = [{ key: 'latest', measuredAt: '2026-10-08T10:00:02', weightGrams: 1050, volumeCm3: 2000 },
    { key: 'zero', measuredAt: '2026-10-08T10:00:00', weightGrams: 0, volumeCm3: null },
    { key: 'missing-time', measuredAt: null, occurredAt: '2026-10-08T09:00:00', weightGrams: 500, volumeCm3: 1000 },
    { key: 'bad-time', measuredAt: 'bad', weightGrams: 100, volumeCm3: 500 },
    { key: 'missing-value', measuredAt: '2026-10-08T10:00:01', weightGrams: null, volumeCm3: 900 }];
  const weight = dwsPlotSamples(rows, 'weightGrams'); assert.deepEqual(weight.map(point => point.sample.key), ['zero', 'latest']); assert.equal(weight[0].value, 0);
  assert.deepEqual(dwsPlotSamples(rows, 'volumeCm3').map(point => point.sample.key), ['missing-value', 'latest']);
  assert.equal(rows.length, 5); assert.equal(dwsValue(null), '—'); assert.equal(dwsValue(0, 'g'), '0 g');
  assert.equal(dwsValue(0.001, 'g'), '0.001 g'); assert.equal(dwsValue(0.000001, 'cm³'), '0.000001 cm³');
});
