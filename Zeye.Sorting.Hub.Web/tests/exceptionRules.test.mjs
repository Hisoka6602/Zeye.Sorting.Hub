import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { classifyException, normalizeExceptionRules } from '../src/data/exceptionRules.ts';
import { initialExceptionRules, legacyExceptionRules, sorterExceptionDefinitions, unknownExceptionRule } from '../src/data/mock/operations.ts';

const facts = code => ({ '来源异常代码': code });

test('Fusion 的三种协议编码精确匹配，未知或相似编码最后归入未知异常', () => {
  for (const definition of sorterExceptionDefinitions) {
    assert.equal(classifyException(initialExceptionRules, facts(definition.code)).exceptionType, definition.type);
    assert.equal(classifyException(initialExceptionRules, facts(` ${definition.code.toLowerCase()} `)).exceptionType, definition.type);
  }
  for (const code of ['FutureDeviceFailure', 'DEVICE_MOTOR', 'RoutingTimeoutExtra', '13', '', undefined]) {
    assert.equal(classifyException([unknownExceptionRule, ...initialExceptionRules], facts(code)).exceptionType, 0);
  }
  assert.equal(classifyException([], facts('anything')).exceptionType, 0);
});

test('删除、改名、停用或复制兜底规则后，规范化仍只保留一条不可变的系统兜底', () => {
  const changed = { ...unknownExceptionRule, name: '改名', status: '草稿', conditions: [{ field: '来源异常代码', operator: '等于', value: 'always' }] };
  const duplicate = { ...changed, id: 500 };
  for (const input of [[], [changed], [changed, duplicate]]) {
    const normalized = normalizeExceptionRules(input);
    assert.equal(normalized.filter(rule => rule.exceptionType === 0).length, 1);
    assert.deepEqual(normalized.at(-1), unknownExceptionRule);
    assert.deepEqual(normalizeExceptionRules(normalized), normalized);
    assert.equal(normalized.length, 4);
  }
});

test('升级旧示例会补齐协议默认规则，并保留改过的旧规则和新建草稿', () => {
  const modified = { ...legacyExceptionRules[0], name: '用户修改的规则' };
  const created = { ...modified, id: 501, name: '用户新增草稿', status: '草稿' };
  const normalized = normalizeExceptionRules([...legacyExceptionRules, modified, created]);
  assert.equal(normalized.length, 6);
  assert.equal(normalized.filter(rule => rule.systemRule === 'sorter-protocol').length, 3);
  assert.equal(normalized.some(rule => rule.name === modified.name), true);
  assert.equal(normalized.some(rule => rule.name === created.name), true);
  assert.equal(normalized.some(rule => rule.name === legacyExceptionRules[5].name), false);
});

test('只有已发布且条件满足的自定义规则先于未知兜底，草稿及缺失比较字段不会误命中', () => {
  const custom = { ...legacyExceptionRules[0], id: 501, status: '已发布', matchMode: 'all', conditions: [
    { field: '来源异常代码', operator: '等于', value: 'CustomFailure' },
    { field: '异常信息', operator: '包含', value: '电机' },
  ] };
  assert.equal(classifyException([unknownExceptionRule, custom], { ...facts('CustomFailure'), '异常信息': '电机故障' }).id, 501);
  assert.equal(classifyException([custom], facts('CustomFailure')).exceptionType, 0);
  assert.equal(classifyException([{ ...custom, status: '草稿' }], { ...facts('CustomFailure'), '异常信息': '电机故障' }).exceptionType, 0);
  assert.equal(classifyException([{ ...custom, matchMode: 'any' }], facts('CustomFailure')).id, 501);
  assert.equal(classifyException([{ ...custom, conditions: [{ field: '响应状态码', operator: '不等于', value: '200' }] }], facts('CustomFailure')).exceptionType, 0);
});

test('前端协议默认编码及类型与后端实际分类器一致，现有枚举编号保持稳定', () => {
  const classifier = readFileSync(new URL('../../Zeye.Sorting.Hub.Domain/Aggregates/Parcels/Processing/SorterExceptionClassifier.cs', import.meta.url), 'utf8');
  const enumSource = readFileSync(new URL('../../Zeye.Sorting.Hub.Domain/Enums/ParcelExceptionType.cs', import.meta.url), 'utf8');
  const mappings = [...classifier.matchAll(/\["([^"]+)"\] = ParcelExceptionType\.(\w+)/g)];
  assert.equal(mappings.length, sorterExceptionDefinitions.length);
  for (const [, code, name] of mappings) {
    const numericType = Number(enumSource.match(new RegExp(`${name} = (\\d+)`))[1]);
    assert.equal(sorterExceptionDefinitions.find(item => item.code === code)?.type, numericType);
  }
  assert.match(enumSource, /Unknown = 0/);
  assert.match(enumSource, /SourceDeviceException = 15/);
});