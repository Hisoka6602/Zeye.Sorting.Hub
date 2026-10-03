import test from 'node:test';
import assert from 'node:assert/strict';
import { editableExceptionCondition, exceptionFactsFromInput, saveExceptionCondition } from '../src/data/exceptionConditions.ts';
import { classifyException, matchesExceptionRule, normalizeExceptionRules } from '../src/data/exceptionRules.ts';
import { initialExceptionRules, unknownExceptionRule } from '../src/data/mock/operations.ts';

test('包裹类型验证与服务端一致，中文及枚举名称均能匹配', () => {
  const configured = { id: 1, name: '大件异常', exceptionType: 11, status: '草稿', matchMode: 'all', conditions: [{ field: '包裹类型', operator: '等于', values: ['Large'] }] };
  assert.equal(matchesExceptionRule(configured, exceptionFactsFromInput({ typeName: '大型包裹' })), true);
  configured.conditions[0].values = ['易碎品'];
  assert.equal(matchesExceptionRule(configured, exceptionFactsFromInput({ typeName: 'Fragile' })), true);
  assert.equal(matchesExceptionRule(configured, exceptionFactsFromInput({})), false);
});

const rule = (conditions, matchMode = 'all') => ({
  id: 601, name: '自定义异常', exceptionType: 1, targetType: '接口响应异常', status: '草稿', matchMode, conditions,
});
const matches = (condition, input) => matchesExceptionRule(rule([condition]), exceptionFactsFromInput(input));
const threshold = (field, operator, value, unit) => ({ field, operator, value, unit });
const content = (field, operator, values) => ({ field, operator, values });

test('重量按数值比较并换算 kg/g，边界、零与浮点换算保持一致', () => {
  assert.equal(matches(threshold('包裹重量', '小于', '1000', 'g'), { weightKg: 0.5 }), true);
  assert.equal(matches(threshold('包裹重量', '大于', '500', 'g'), { weightKg: 1 }), true);
  assert.equal(matches(threshold('包裹重量', '等于', '500', 'g'), { weightKg: '0.500' }), true);
  assert.equal(matches(threshold('包裹重量', '等于', '100', 'g'), { weightKg: 0.1 }), true);
  for (const operator of ['小于', '大于', '不等于']) assert.equal(matches(threshold('包裹重量', operator, '100', 'g'), { weightKg: 0.1 }), false);
  for (const operator of ['小于或等于', '大于或等于']) assert.equal(matches(threshold('包裹重量', operator, '100', 'g'), { weightKg: 0.1 }), true);
  assert.equal(matches(threshold('包裹重量', '等于', '0', 'kg'), { weightKg: 0 }), true);
  assert.equal(matches(threshold('包裹重量', '小于', '2', 'kg'), { weightKg: 10 }), false);
});

test('缺失、非法数值、单位错误和溢出不会误判为零或满足不等于', () => {
  for (const value of [undefined, null, '', ' ', false, -1, '-1', '0x10', 'Infinity', NaN, Infinity]) {
    for (const operator of ['等于', '小于', '不等于']) assert.equal(matches(threshold('包裹重量', operator, '1', 'kg'), { weightKg: value }), false);
  }
  for (const value of ['', ' ', '20 kg', '-1', 'NaN', '0x10', 'Infinity']) assert.equal(matches(threshold('包裹重量', '不等于', value, 'kg'), { weightKg: 1 }), false);
  assert.equal(matches(threshold('包裹重量', '大于', '1', 'cm'), { weightKg: 2 }), false);
  assert.equal(matches(threshold('包裹体积（长×宽×高）', '小于', '1e308', 'm³'), { lengthMm: 100, widthMm: 100, heightMm: 100 }), false);
});

test('长宽高分别比较，物理体积按完整尺寸计算及单位换算，缺项不计算', () => {
  const input = { lengthMm: 100, widthMm: 200, heightMm: 300, volumetricWeightGrams: 9999 };
  assert.equal(matches(threshold('包裹长度', '等于', '10', 'cm'), input), true);
  assert.equal(matches(threshold('包裹宽度', '小于', '0.3', 'm'), input), true);
  assert.equal(matches(threshold('包裹高度', '大于', '200', 'mm'), input), true);
  assert.equal(matches(threshold('包裹体积（长×宽×高）', '等于', '6000', 'cm³'), input), true);
  assert.equal(matches(threshold('包裹体积（长×宽×高）', '等于', '0.006', 'm³'), input), true);
  assert.equal(matches(threshold('包裹体积（长×宽×高）', '大于', '5000', 'cm³'), input), true);
  assert.equal(matches(threshold('包裹体积（长×宽×高）', '小于', '7000000', 'mm³'), input), true);
  for (const partial of [{ lengthMm: 100, widthMm: 200 }, { lengthMm: 100, widthMm: '', heightMm: 300 }, { volumetricWeightGrams: 100 }]) {
    assert.equal(exceptionFactsFromInput(partial)['包裹体积（长×宽×高）'], undefined);
    assert.equal(matches(threshold('包裹体积（长×宽×高）', '小于', '7000', 'cm³'), partial), false);
  }
  assert.equal(matches(threshold('包裹体积（长×宽×高）', '等于', '0', 'cm³'), { lengthMm: 0, widthMm: 100, heightMm: 100 }), true);
  assert.equal(exceptionFactsFromInput({ lengthMm: 1e308, widthMm: 1e308, heightMm: 2 })['包裹体积（长×宽×高）'], undefined);
});

test('条码支持多条码和多个匹配内容，精确匹配保留前导零与大小写', () => {
  const input = { barcodes: ['00123', 'SF98765'] };
  assert.equal(matches(content('条码', '等于', ['00123', '不存在']), input), true);
  assert.equal(matches(content('条码', '等于', ['123']), input), false);
  assert.equal(matches(content('条码', '包含', ['YT', 'SF']), input), true);
  assert.equal(matches(content('条码', '包含', ['sf']), input), false);
  assert.equal(matches(content('条码', '不包含', ['YT', 'SF']), input), false);
  assert.equal(matches(content('条码', '不等于', ['00123', '不存在']), input), false);
  assert.equal(matches(content('条码', '不包含', ['YT', 'JD']), input), true);
  assert.equal(matches(content('条码', '不包含', ['SF']), {}), false);
  assert.equal(matches(content('条码', '等于', []), input), false);
});

test('Provider 名称与响应正文可组合匹配，JSON 内容按实际文本包含或等于', () => {
  const response = '{"code":"ERROR","message":"无路由,请复核"}';
  const input = { provider: 'RouteProvider', providerResponse: response, responseStatusCode: 500 };
  const conditions = [content('Provider 名称', '等于', ['RouteProvider']), content('Provider 响应内容', '包含', ['拒绝', '无路由']), threshold('响应状态码', '大于或等于', '400')];
  assert.equal(matchesExceptionRule(rule(conditions), exceptionFactsFromInput(input)), true);
  assert.equal(matchesExceptionRule(rule(conditions), exceptionFactsFromInput({ ...input, provider: 'OtherProvider' })), false);
  assert.equal(matches(content('Provider 响应内容', '等于', [response]), input), true);
  assert.equal(matches(content('Provider 响应内容', '等于', ['无路由']), input), false);
  assert.equal(matches(content('Provider 响应内容', '包含', ['"code":"ERROR"']), input), true);
  assert.equal(matches(content('Provider 响应内容', '不包含', ['无路由']), {}), false);
});

test('数值、条码与响应条件支持且/或；草稿预览不发布规则，未命中仍有唯一未知兜底', () => {
  const custom = rule([threshold('包裹重量', '大于', '1000', 'g'), content('条码', '包含', ['SF']), content('Provider 响应内容', '包含', ['无路由'])]);
  const facts = exceptionFactsFromInput({ weightKg: 2, barcodes: ['SF00123'], providerResponse: '无路由' });
  assert.equal(matchesExceptionRule(custom, facts), true);
  assert.equal(matchesExceptionRule(custom, { ...facts, '包裹重量': 0.5 }), false);
  assert.equal(matchesExceptionRule({ ...custom, matchMode: 'any' }, { '条码': ['SF00123'] }), true);
  assert.equal(matchesExceptionRule({ ...custom, conditions: [] }, facts), false);
  const stored = normalizeExceptionRules([...initialExceptionRules, custom]);
  assert.equal(classifyException(stored, facts).exceptionType, 0);
  assert.equal(custom.status, '草稿');
  assert.equal(classifyException([{ ...custom, status: '已发布' }, unknownExceptionRule], facts).id, custom.id);
  assert.equal(classifyException([{ ...custom, status: '已发布' }], { ...facts, '包裹重量': 0.5 }).exceptionType, 0);
  assert.equal(stored.filter(item => item.exceptionType === 0).length, 1);
});

test('旧单值规则编辑保存后仍可匹配，数值单位和多值内容可以持久化再编辑', () => {
  const legacy = { field: '来源异常代码', operator: '等于', value: ' RoutingTimeout ' };
  const saved = saveExceptionCondition(editableExceptionCondition(legacy));
  assert.equal(matches(saved, { sourceCode: 'routingtimeout' }), true);
  const numeric = saveExceptionCondition(editableExceptionCondition(threshold('包裹体积（长×宽×高）', '等于', '6000000')));
  assert.equal(numeric.unit, 'mm³');
  assert.equal(matches(numeric, { lengthMm: 100, widthMm: 200, heightMm: 300 }), true);
  const original = rule([threshold('包裹重量', '等于', '500', 'g'), content('条码', '包含', ['SF', 'YT']), content('Provider 响应内容', '包含', ['无路由', '拒绝'])]);
  const restored = JSON.parse(JSON.stringify({ ...original, conditions: original.conditions.map(item => saveExceptionCondition(editableExceptionCondition(item))) }));
  assert.equal(matchesExceptionRule(restored, exceptionFactsFromInput({ weightKg: 0.5, barcodes: ['YT00123'], providerResponse: '已拒绝' })), true);
  const empty = saveExceptionCondition({ field: '包裹重量', operator: '为空', value: '9', unit: 'g', values: ['old'] });
  assert.equal(matches(empty, {}), true);
  assert.equal(matches(empty, { weightKg: 0 }), false);
});
