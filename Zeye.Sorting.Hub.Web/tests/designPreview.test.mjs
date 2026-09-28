import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readDesignPreview } from '../src/data/api/designPreview.ts';

test('reference ledger remains a seven-row snapshot and its filters work', () => {
  const list = readDesignPreview('/api/parcels?scannedTimeStart=2026-09-25T00:00:00&scannedTimeEnd=2026-09-25T23:59:59&pageNumber=1&pageSize=10');
  assert.equal(list.totalCount, 7);
  assert.equal(list.items[0].id, '2509250001');
  assert.equal(list.items[6].id, '2509250007');
  const filtered = readDesignPreview('/api/parcels?barCodeKeyword=SF0987&status=0&pageNumber=1&pageSize=10');
  assert.deepEqual(filtered.items.map(row => row.id), ['2509250005']);
});

test('detail reference is a later snapshot with five trace records', () => {
  const parcel = readDesignPreview('/api/parcels/2509250005');
  assert.equal(parcel.status, 1);
  assert.equal(parcel.targetChuteCode, 'D04-01');
  assert.equal(parcel.processingRecords.length, 5);
  assert.equal(parcel.processingRecords[0].previewType, '分拣完成');
});

test('report reference keeps its KPI and seven daily figures consistent', () => {
  const report = readDesignPreview('/api/parcels/analytics?fromDate=2026-09-19&toDate=2026-09-25&site=%E5%8D%8E%E4%B8%9C%E5%88%86%E6%8B%A8%E4%B8%AD%E5%BF%83&line=%E5%85%A8%E9%83%A8%E4%BA%A7%E7%BA%BF');
  assert.equal(report.detectedCount, 125680);
  assert.equal(report.exceptionCount, 707);
  assert.equal((100 * report.exceptionCount / report.detectedCount).toFixed(2), '0.56');
  assert.equal(report.averageLifecycleSeconds.toFixed(1), '18.6');
  assert.equal(report.daily.length, 7);
  assert.equal(report.daily.reduce((sum, row) => sum + row.detectedCount, 0), report.detectedCount);
});

test('preview does not fabricate unknown API responses', () => {
  assert.throws(() => readDesignPreview('/api/admin/parcels'), /没有.*样本/);
});
