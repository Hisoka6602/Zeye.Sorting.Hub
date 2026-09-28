import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { parseApiJson, requestApi, ApiError } from '../src/data/api/client.ts';
import { createDetectionRecordId } from '../src/data/api/detectionIdentity.ts';
import { createHash } from 'node:crypto';

// 隔离HTTP客户端，不访问真实服务或生产数据库。
const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });

test('64位编号和时间戳保持精确，零、未知量测和小数保持语义', () => {
  const result = parseApiJson('{"id":9223372036854775806,"sourceParcelId":"9223372036854775807","parcelTimestamp":639261864000000001,"weight":1.234567,"width":null,"height":0}');
  assert.equal(result.id, '9223372036854775806');
  assert.equal(result.sourceParcelId, '9223372036854775807');
  assert.equal(result.parcelTimestamp, '639261864000000001');
  assert.equal(result.weight, 1.234567);
  assert.equal(result.width, null);
  assert.equal(result.height, 0);
});

test('成功响应完整保留处理事实和本地时间原文', async () => {
  globalThis.fetch = async () => new Response('{"processingRecords":[{"occurredAt":"2026-09-28T10:00:00.1234567","rawPayload":"RAW-DWS","isSuccess":false,"errorMessage":"timeout"}]}');
  const result = await requestApi('/api/parcels/1');
  assert.equal(result.processingRecords[0].occurredAt, '2026-09-28T10:00:00.1234567');
  assert.equal(result.processingRecords[0].rawPayload, 'RAW-DWS');
  assert.equal(result.processingRecords[0].isSuccess, false);
});

test('不存在和内容冲突保留真实状态及后端原因', async () => {
  for (const status of [404, 409]) {
    globalThis.fetch = async () => new Response('{"detail":"来源记录冲突"}', { status });
    await assert.rejects(requestApi('/api/parcels/1'), error => error instanceof ApiError && error.status === status && error.message === '来源记录冲突');
  }
});

test('代理HTML错误不回退演示数据，保留502状态', async () => {
  globalThis.fetch = async () => new Response('<html>Bad Gateway</html>', { status: 502 });
  await assert.rejects(requestApi('/api/parcels'), error => error instanceof ApiError && error.status === 502);
});

test('网络中断向调用方传播，不生成空成功结果', async () => {
  globalThis.fetch = async () => { throw new TypeError('网络中断'); };
  await assert.rejects(requestApi('/api/parcels'), { message: '网络中断' });
});

test('检测记录身份由来源三元组确定，页面重载与条码变化不改变重试身份', async () => {
  const identity = { sourceInstanceId: 'sorter-01', sourceRunId: 'counter-epoch-01', sourceParcelId: '9223372036854775807' };
  const expected = 'detected-v1-' + createHash('sha256').update(JSON.stringify([
    'detected-v1', identity.sourceInstanceId, identity.sourceRunId, identity.sourceParcelId,
  ])).digest('hex');
  assert.equal(await createDetectionRecordId(identity), expected);
  assert.equal(await createDetectionRecordId({ ...identity }), expected);
  assert.notEqual(await createDetectionRecordId({ ...identity, sourceRunId: 'counter-epoch-02' }), expected);
  await assert.rejects(createDetectionRecordId({ ...identity, sourceRunId: ' counter-epoch-01' }));
});
