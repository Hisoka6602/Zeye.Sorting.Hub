import test from 'node:test';
import assert from 'node:assert/strict';
import { formatNumber, formatNumericInput } from '../src/data/formatNumber.ts';
import { exceptionFactsFromInput, formatConditionValue } from '../src/data/exceptionConditions.ts';
import { matchesExceptionRule } from '../src/data/exceptionRules.ts';

test('整数和短小数按实际显示，不补尾零', () => {
  for (const [value, expected] of [[0, '0'], [12, '12'], [12.5, '12.5'], [12.34, '12.34'], [1.2, '1.2']]) {
    assert.equal(formatNumber(value), expected);
  }
  assert.equal(formatNumber(1234.5, { grouping: true }), '1,234.5');
});

test('超过两位小数四舍五入，浮点边界和进位正确', () => {
  for (const [value, expected] of [[12.3456, '12.35'], [12.344, '12.34'], [1.005, '1.01'], [2.675, '2.68'], [999.999, '1000'], [-1.005, '-1.01'], [0.1 + 0.2, '0.3']]) {
    assert.equal(formatNumber(value), expected);
  }
});

test('极大和极小的数字均使用定点表示，舍入后的零不显示负号', () => {
  assert.equal(formatNumber(1e21), '1000000000000000000000');
  assert.equal(formatNumber(1e-7), '0');
  assert.equal(formatNumber(5e-3), '0.01');
  assert.equal(formatNumber(-0), '0');
  assert.equal(formatNumber(-0.004), '0');
  assert.equal(formatNumber(9223372036854775807n), '9223372036854775807');
});

test('未知和非法数值保留缺失语义，真实零仍显示零', () => {
  for (const value of [null, undefined, NaN, Infinity, -Infinity]) assert.equal(formatNumber(value), '—');
  assert.equal(formatNumber(0), '0');
});

test('数字输入保留编辑中的原文，结束编辑后统一显示且大整数不失真', () => {
  const settled = { userTyping: false, input: '' };
  for (const input of ['1.', '1.2300', '-', '1e-7']) {
    assert.equal(formatNumericInput(undefined, { userTyping: true, input }), input);
  }
  assert.equal(formatNumericInput('1.23456', settled), '1.23');
  assert.equal(formatNumericInput('1.200', settled), '1.2');
  assert.equal(formatNumericInput('1e21', settled), '1000000000000000000000');
  assert.equal(formatNumericInput('9223372036854775807', settled), '9223372036854775807');
  assert.equal(formatNumericInput(undefined, settled), '');
  assert.equal(formatNumericInput('', settled), '');
});

test('规则摘要格式化数字，条码原文和实际规则比较精度保持完整', () => {
  const condition = { field: '包裹体积（长×宽×高）', operator: '等于', value: '0.006', unit: 'm³' };
  assert.equal(formatConditionValue(condition), '0.01 m³');
  assert.equal(condition.value, '0.006');
  const rule = { conditions: [condition], matchMode: 'all' };
  assert.equal(matchesExceptionRule(rule, exceptionFactsFromInput({ lengthMm: 100, widthMm: 200, heightMm: 300 })), true);
  assert.equal(formatConditionValue({ field: '包裹重量', operator: '等于', value: '2.000', unit: 'kg' }), '2 kg');
  assert.equal(formatConditionValue({ field: '条码', operator: '等于', value: '001.2300' }), '001.2300');
});
