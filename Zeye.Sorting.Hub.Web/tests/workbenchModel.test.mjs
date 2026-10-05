import assert from 'node:assert/strict';
import test from 'node:test';
import { mergeWorkstationSources, observeWorkstations, workstationKey } from '../src/features/parcels/workbenchModel.ts';

test('登记来源无包裹也展示，心跳与包裹统计保持独立且不修改原观察结果', () => {
  const observed = observeWorkstations([parcel('fusion-a', '旧名称')]);
  const presence = { sourceInstanceId: 'fusion-a', workstationName: '登记名称', isOnline: false, pendingFacts: 8 };
  const result = mergeWorkstationSources(observed, [presence, { sourceInstanceId: 'fusion-b', workstationName: '新工作台', isOnline: true }]);
  assert.equal(result.workstations.length, 2);
  assert.equal(result.workstations[0].name, '登记名称');
  assert.equal(result.workstations[0].parcelCount, 1);
  assert.equal(result.workstations[0].presence.isOnline, false);
  assert.equal(result.workstations[1].parcelCount, 0);
  assert.equal(result.workstations[1].presence.isOnline, true);
  assert.equal(observed.workstations[0].presence, undefined);
});

const parcel = (sourceInstanceId, workstationName, status = 1, createdTime = '2026-10-03T10:00:00') => ({ sourceInstanceId, workstationName, status, createdTime });

test('同名工作台的不同来源实例保持独立，编号会话不改变实例身份', () => {
  const first = { ...parcel('fusion-a', '一号工作台'), sourceRunId: 'run-1' };
  const second = { ...parcel('fusion-a', '一号工作台', 2), sourceRunId: 'run-2' };
  const { workstations } = observeWorkstations([first, second, parcel('fusion-b', '一号工作台', 0)]);
  assert.equal(workstations.length, 2);
  const a = workstations.find(item => item.sourceInstanceId === 'fusion-a');
  assert.equal(a.parcelCount, 2);
  assert.equal(a.completedCount, 1);
  assert.equal(a.exceptionCount, 1);
  assert.equal(workstationKey(first), workstationKey(second));
});

test('同一实例改名使用最近记录名称，不创建额外工作台', () => {
  const { workstations } = observeWorkstations([parcel('fusion-a', '新名称', 1, '2026-10-03T11:00:00'), parcel('fusion-a', '旧名称')]);
  assert.equal(workstations.length, 1);
  assert.equal(workstations[0].name, '新名称');
  assert.equal(workstations[0].lastParcelAt, '2026-10-03T11:00:00');
});

test('缺少实例编码时按名称归组，缺少全部来源标识不伪造工作台', () => {
  const { workstations, unassignedCount } = observeWorkstations([parcel(null, '工作台 A'), parcel(null, '工作台 A', 0), parcel(null, ''), parcel(null, '   ')]);
  assert.equal(workstations.length, 1);
  assert.equal(workstations[0].sourceInstanceId, null);
  assert.equal(workstations[0].parcelCount, 2);
  assert.equal(unassignedCount, 2);
  assert.deepEqual(observeWorkstations([]), { workstations: [], unassignedCount: 0 });
});

test('实例和名称使用独立身份空间，长编号不转换为数值', () => {
  const id = '9223372036854775807';
  const { workstations } = observeWorkstations([parcel(id, 'A'), parcel(null, id)]);
  assert.equal(workstations.length, 2);
  assert.equal(workstations.find(item => item.name === 'A').sourceInstanceId, id);
});

test('未知状态单独计数，无有效时间保留未知并排在有效观察之后', () => {
  const { workstations } = observeWorkstations([parcel('a', 'A', 99, 'invalid'), parcel('b', 'B', 2)]);
  assert.equal(workstations[0].sourceInstanceId, 'b');
  assert.equal(workstations[1].lastParcelAt, null);
  assert.equal(workstations[1].otherCount, 1);
  assert.equal(workstations[1].pendingCount + workstations[1].completedCount + workstations[1].exceptionCount, 0);
  assert.ok(workstations.every(item => !('online' in item)));
});

test('大量工作台不截断，汇总不改写原始包裹', () => {
  const parcels = Array.from({ length: 120 }, (_, index) => Object.freeze(parcel(`fusion-${index}`, `工作台 ${index}`, index % 3)));
  const { workstations } = observeWorkstations(Object.freeze(parcels));
  assert.equal(workstations.length, 120);
  assert.equal(workstations.reduce((sum, item) => sum + item.parcelCount, 0), 120);
});
