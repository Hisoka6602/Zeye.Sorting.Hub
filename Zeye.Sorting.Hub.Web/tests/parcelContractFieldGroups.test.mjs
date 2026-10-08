import assert from 'node:assert/strict';
import test from 'node:test';
import { groupParcelContractFields } from '../src/features/parcels/parcelContractFieldGroups.ts';

test('分组不遗漏或重复字段，未知字段按原顺序保留', () => {
  const keys = ['futureCode', 'id', 'status', 'sourceRunId', 'length', 'modifyTime', 'hasImages', 'futurePayload', 'id'];
  const before = [...keys];
  const groups = groupParcelContractFields(keys);
  assert.deepEqual(groups.map(group => group.id), ['identity', 'routing', 'measurement', 'times', 'attachments', 'other']);
  assert.deepEqual(new Set(groups.flatMap(group => group.keys)), new Set(keys));
  assert.equal(groups.flatMap(group => group.keys).length, new Set(keys).size);
  assert.deepEqual(groups.at(-1).keys, ['futureCode', 'futurePayload']);
  assert.deepEqual(keys, before);
});

test('分组仅依据字段名，零值、false 和空值字段均保持可见', () => {
  const facts = { status: 0, hasImages: false, weight: null, sourceExceptionCode: '', createdTime: '2026-10-06 10:24:28.847272' };
  const before = structuredClone(facts);
  assert.deepEqual(new Set(groupParcelContractFields(Object.keys(facts)).flatMap(group => group.keys)), new Set(Object.keys(facts)));
  assert.deepEqual(facts, before);
});

test('无字段时不生成空信息分组', () => {
  assert.deepEqual(groupParcelContractFields([]), []);
});
