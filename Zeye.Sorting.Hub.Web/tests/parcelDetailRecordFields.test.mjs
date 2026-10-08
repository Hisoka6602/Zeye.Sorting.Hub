import assert from 'node:assert/strict';
import test from 'node:test';
import { formatParcelDetailPayload, groupParcelDetailRecordFields, parcelDetailApiTitle } from '../src/features/parcels/parcelDetailRecordFields.ts';

test('各分区保留所有原始字段，未知字段与缺省字段不丢失', () => {
  const facts = { recordId: '900000000000000001', elapsedMilliseconds: 0, requestStatus: null, requestBody: '{"id":900000000000000001}', responseBody: '', exception: '{"outcome":"started"}', rawData: 'raw', futureBoolean: false, futurePayload: { accepted: false }, futureText: 'future' };
  const original = structuredClone(facts);
  const groups = groupParcelDetailRecordFields(facts);
  assert.deepEqual(new Set(groups.flatMap(group => group.keys)), new Set(Object.keys(facts)));
  assert.equal(groups.flatMap(group => group.keys).length, Object.keys(facts).length);
  assert.deepEqual(groups.find(group => group.id === 'facts').keys, ['futureBoolean', 'futureText']);
  assert.deepEqual(groups.find(group => group.id === 'missing').keys, ['requestStatus', 'responseBody']);
  assert.ok(!groups.find(group => group.id === 'missing').keys.includes('elapsedMilliseconds'));
  assert.deepEqual(facts, original);
});

test('没有内容时不生成空分区，空对象和数组仍保留为实际内容', () => {
  assert.deepEqual(groupParcelDetailRecordFields({}), []);
  assert.deepEqual(groupParcelDetailRecordFields({ futureObject: {}, futureArray: [] })[0].keys, ['futureObject', 'futureArray']);
});

test('处理记录按业务分组，零值、false、未知字段与技术标识均完整保留', () => {
  const facts = { barcode: 'ABC', stage: 1, sourceParcelId: '640791253325101000', parcelId: null, weightGrams: 0, lengthMm: 300, isSpacingViolation: false, previousCreationGapMilliseconds: 0, recordedAt: '2026-10-06T10:24:32.801044', bindingMode: '时间窗匹配', deltaMilliseconds: 0, isFallback: false, recordId: '900000000000000001', messageIdentity: 'frame-001', attemptNumber: 1, decisionReason: '未关联', rawPayload: '{"id":900000000000000001}', futureFlag: false, futurePayload: { value: 0 }, responseBody: '' };
  const original = structuredClone(facts);
  const groups = groupParcelDetailRecordFields(facts, true);
  assert.equal(groups.flatMap(group => group.keys).length, Object.keys(facts).length);
  assert.deepEqual(new Set(groups.flatMap(group => group.keys)), new Set(Object.keys(facts)));
  assert.deepEqual(groups.find(group => group.id === 'measurement').keys, ['weightGrams', 'lengthMm']);
  assert.deepEqual(groups.find(group => group.id === 'binding').keys, ['bindingMode', 'deltaMilliseconds']);
  assert.deepEqual(groups.find(group => group.id === 'identity').keys, ['recordId', 'messageIdentity', 'attemptNumber']);
  assert.deepEqual(groups.find(group => group.id === 'missing').keys, ['parcelId', 'responseBody']);
  assert.ok(groups.find(group => group.id === 'facts').keys.includes('futureFlag'));
  assert.ok(groups.find(group => group.id === 'raw').keys.includes('futurePayload'));
  assert.deepEqual(facts, original);
});

test('JSON 仅整理空白，大整数、科学计数法、转义及字符串内部空白保持原样', () => {
  const text = '{"id":900000000000000001,"negative":-900000000000000002,"text":"a  b \\"quoted\\" \\u626b\\u63cf","zero":0,"flags":[false,null,{}],"number":1.00e+3}';
  const result = formatParcelDetailPayload(text);
  assert.ok(result.includes('900000000000000001'));
  assert.ok(result.includes('-900000000000000002'));
  assert.ok(result.includes('1.00e+3'));
  assert.ok(result.includes('a  b \\"quoted\\" \\u626b\\u63cf'));
  assert.deepEqual(JSON.parse(result), JSON.parse(text));
  assert.equal(formatParcelDetailPayload(result), result);
});

test('非 JSON、截断报文及超大报文均返回原文', () => {
  for (const text of ['HTTP/1.1 200 OK\nbody', '{"partial":', `{"value":"${'a'.repeat(262144)}"}`, `${'['.repeat(80)}0${']'.repeat(80)}`]) assert.equal(formatParcelDetailPayload(text), text);
});

test('旧合同按显式接口类型编码区分业务，未知编码不猜测', () => {
  assert.equal(parcelDetailApiTitle(0), '请求格口');
  assert.equal(parcelDetailApiTitle(3), '落格回传');
  assert.equal(parcelDetailApiTitle(5), '扫描上传');
  assert.equal(parcelDetailApiTitle(null), '外部接口');
  assert.equal(parcelDetailApiTitle(99), '外部接口 · 类型 99');
});
