import test from 'node:test';
import assert from 'node:assert/strict';
import { parcelExceptionDetails } from '../src/features/parcels/parcelExceptionDetails.ts';

const normal = { status: 0, exceptionType: null, sourceExceptionCode: null, processingRecords: [], apiRequests: [] };
const event = { recordId: 'exception-1', stage: 7, occurredAt: '2026-10-02T10:00:00', attemptNumber: 1, exceptionCode: 'ParcelSpacingViolation', errorMessage: 'IntervalMs=220 MinimumIntervalMs=300', decisionReason: null, rawPayload: '{"ExceptionType":"ParcelSpacingViolation"}' };

test('无异常包裹及未加载状态不显示异常版块', () => {
  assert.equal(parcelExceptionDetails(undefined), null);
  assert.equal(parcelExceptionDetails(normal), null);
});

test('仅有异常快照时显示类型，对缺失规则及详细信息保持诚实', () => {
  const result = parcelExceptionDetails({ ...normal, status: 2, exceptionType: 4 });
  assert.equal(result.type, '无效目标格口');
  assert.equal(result.rule, '未提供异常判定规则');
  assert.deepEqual(result.messages, []);
});

test('异常类型零是未知异常，不能被当作无异常隐藏', () => {
  const result = parcelExceptionDetails({ ...normal, status: 2, exceptionType: 0, sourceExceptionCode: 'NewDeviceError' });
  assert.equal(result.type, '未知异常');
  assert.match(result.rule, /所有异常分类规则均未匹配/);
  assert.equal(result.sourceCode, 'NewDeviceError');
});

test('来源编码、快照类型及报文一致时展示判定规则与详细信息', () => {
  const result = parcelExceptionDetails({ ...normal, status: 2, exceptionType: 13, sourceExceptionCode: 'ParcelSpacingViolation', processingRecords: [event] });
  assert.equal(result.type, '包裹间距违规');
  assert.match(result.rule, /来源异常代码等于 ParcelSpacingViolation/);
  assert.deepEqual(result.messages, ['IntervalMs=220 MinimumIntervalMs=300']);
  assert.equal(result.records[0].rawPayload, event.rawPayload);
});

test('包裹完成后保留真实异常历史，并且不标记为当前异常', () => {
  const result = parcelExceptionDetails({ ...normal, status: 1, processingRecords: [event] });
  assert.equal(result.current, false);
  assert.equal(result.type, '包裹间距违规');
});

test('来源编码与保存的类型不一致时不虚构命中规则', () => {
  const result = parcelExceptionDetails({ ...normal, status: 2, exceptionType: 6, sourceExceptionCode: 'ParcelSpacingViolation', processingRecords: [event] });
  assert.equal(result.type, '锁格');
  assert.equal(result.rule, '未提供异常判定规则');
});

test('处理失败及接口异常有详细信息，正常成功记录不会混入异常版块', () => {
  const result = parcelExceptionDetails({ ...normal, processingRecords: [
    { ...event, recordId: 'success', stage: 3, isSuccess: true, exceptionCode: null, errorMessage: null },
    { ...event, recordId: 'failure', stage: 3, isSuccess: false, exceptionCode: null, errorMessage: '接口超时' },
  ], apiRequests: [{ exception: '连接失败' }, { exception: null }] });
  assert.deepEqual(result.records.map(record => record.recordId), ['failure']);
  assert.deepEqual(result.messages, ['接口超时', '连接失败']);
});

test('正常调用的 started/completed 诊断元数据即使保存为 errorMessage 也不是包裹异常', () => {
  const processingRecords = ['started', 'completed'].map(outcome => {
    const detail = JSON.stringify({ operationId: 'chute', attemptId: 'attempt-1', operation: '目标格口分配', outcome, attemptNumber: 1 });
    return { ...event, stage: 3, recordId: outcome, exceptionCode: null, isSuccess: null, errorMessage: detail,
      rawPayload: JSON.stringify({ kind: 'provider-attempt', name: '目标格口分配', outcome, detail }) };
  });
  assert.equal(parcelExceptionDetails({ ...normal, status: 1, processingRecords }), null);
});

test('失败和未知结果仍显示为历史异常，展示业务说明并保留诊断原文', () => {
  const detail = JSON.stringify({ operationId: 'chute', attemptId: 'attempt-1', operation: '目标格口分配', outcome: 'unknown', attemptNumber: 1 });
  const record = { ...event, stage: 3, exceptionCode: null, isSuccess: null, errorMessage: detail,
    rawPayload: JSON.stringify({ kind: 'provider-attempt', name: '目标格口分配', outcome: 'unknown', detail }) };
  const result = parcelExceptionDetails({ ...normal, status: 1, processingRecords: [record] });
  assert.deepEqual(result.messages, ['结果未知']);
  assert.equal(result.recordTitles[record.recordId], '请求格口');
  assert.equal(result.records[0].errorMessage, detail);
  assert.equal(result.current, false);
});
