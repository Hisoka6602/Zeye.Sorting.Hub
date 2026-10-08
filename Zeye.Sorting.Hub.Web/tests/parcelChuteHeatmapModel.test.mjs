import assert from 'node:assert/strict';
import test from 'node:test';
import { chuteHeatmapDrillFilters, chuteHeatmapLevel, compareChuteCodes, groupChuteHeatmapCells } from '../src/features/parcels/parcelChuteHeatmapModel.ts';
import { analysisApiPath, readAnalysisFilters } from '../src/features/parcels/parcelAnalysisModel.ts';

const cell = (overrides = {}) => ({ sourceInstanceId: 'source-a', workstationName: '线1', chuteCode: '0013', count: 50, mismatchCount: 3, fallbackCount: 2, ...overrides });
test('同名格口按来源和工作台隔离，原始前导零、空格和大编号编码不合并', () => {
  const cells = [cell(), cell({ chuteCode: '13', count: 9 }), cell({ chuteCode: '9007199254740993', count: 1 }),
    cell({ sourceInstanceId: 'source-b', count: 6 }), cell({ workstationName: '线2', count: 5 }), cell({ chuteCode: ' 13 ', count: 2 })];
  const groups = groupChuteHeatmapCells(cells);
  assert.equal(groups.length, 3);
  assert.equal(groups[0].count, 62);
  assert.equal(groups[0].cells.length, 4);
  assert.deepEqual(cells.map(value => value.chuteCode), ['0013', '13', '9007199254740993', '0013', '0013', ' 13 ']);
  assert.notEqual(compareChuteCodes('0013', '13'), 0);
  assert.ok(compareChuteCodes('X2', 'X13') < 0);
});
test('热度使用线性等宽色阶，真实零票指标独立呈现，不使用对数夸大低频格口', () => {
  assert.deepEqual([0, 1, 20, 21, 40, 60, 80, 100].map(value => chuteHeatmapLevel(value, 100)), [0, 1, 1, 2, 2, 3, 4, 5]);
  assert.equal(chuteHeatmapLevel(0, 0), 0);
  assert.equal(chuteHeatmapLevel(1, 1), 5);
});
test('点击指标精确下钻对应票数，保留原始编码，清除上一方向和指标条件', () => {
  for (const kind of ['actual', 'target']) for (const metric of ['count', 'mismatchCount', 'fallbackCount']) {
    const changes = chuteHeatmapDrillFilters(cell({ chuteCode: ' x01 ' }), kind, metric);
    assert.equal(changes[kind === 'actual' ? 'actualChuteCode' : 'targetChuteCode'], ' x01 ');
    assert.equal(changes[kind === 'actual' ? 'targetChuteCode' : 'actualChuteCode'], null);
    assert.equal(changes.mismatchOnly, metric === 'mismatchCount' ? 'true' : null);
    assert.equal(changes.fallbackOnly, metric === 'fallbackCount' ? 'true' : null);
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(changes)) if (value != null) query.set(key, value);
    query.set('heatmapKind', kind); query.set('heatmapMetric', metric);
    const filters = readAnalysisFilters(query, '2026-10-08');
    const api = new URL(analysisApiPath(filters, 'chutes'), 'http://local').searchParams;
    assert.equal(api.get('fallbackOnly'), metric === 'fallbackCount' ? 'true' : null);
    assert.equal(api.get('sourceInstanceId'), 'source-a');
    assert.equal(api.get(kind === 'actual' ? 'actualChuteCode' : 'targetChuteCode'), ' x01 ');
    assert.equal(api.get('heatmapMetric'), null);
    assert.equal(new URL(analysisApiPath(filters, 'duration'), 'http://local').searchParams.get('fallbackOnly'), null);
  }
});
test('缺少来源或工作台、空白编码及零票指标不能跨来源下钻', () => {
  for (const overrides of [{ sourceInstanceId: null }, { workstationName: ' ' }, { chuteCode: ' ' }, { fallbackCount: 0 }])
    assert.equal(chuteHeatmapDrillFilters(cell(overrides), 'actual', 'fallbackCount'), null);
});
