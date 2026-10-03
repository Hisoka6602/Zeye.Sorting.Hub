import test from 'node:test';
import assert from 'node:assert/strict';
import { parseApiJson, requestApi, readHealthReport, ApiError } from '../src/data/api/client.ts';
test('真实接口保留长编号，写请求携带来源标识', async t => {
  t.mock.method(globalThis, 'fetch', async (_url, init) => {
    assert.equal(init.headers['X-Zeye-Client'], 'web');
    return new Response('{"id":9223372036854775806,"sourceParcelId":9007199254740993}', { status: 200 });
  });
  const result = await requestApi('/api/admin/parcels', undefined, { method: 'POST', body: '{}' });
  assert.equal(result.id, '9223372036854775806');
  assert.equal(result.sourceParcelId, '9007199254740993');
});
test('接口拒绝失败响应，不会返回演示成功', async t => {
  t.mock.method(globalThis, 'fetch', async () => new Response('{"detail":"版本已变化"}', { status: 409 }));
  await assert.rejects(requestApi('/api/operations/rules/exception'), error => error instanceof ApiError && error.status === 409 && error.message === '版本已变化');
});
test('503 健康报告显示真实异常，非报告和网络错误不能冒充正常', async t => {
  const fetchMock = t.mock.method(globalThis, 'fetch', async () => new Response('{"status":"Unhealthy","entries":{"database":{"status":"Unhealthy"}}}', { status: 503 }));
  assert.equal((await readHealthReport('/health/ready')).status, 'Unhealthy');
  fetchMock.mock.mockImplementation(async () => new Response('{"detail":"不可用"}', { status: 503 }));
  await assert.rejects(readHealthReport('/health/ready'), /未返回有效报告/);
  fetchMock.mock.mockImplementation(async () => new Response('<html>错误</html>', { status: 502 }));
  await assert.rejects(readHealthReport('/health/ready'), error => error instanceof ApiError && error.status === 502);
});
test('没有把载荷 JSON 字符串中的编号或前导零重新序列化', () => {
  const result = parseApiJson('{"payloadJson":"{\\"id\\":9223372036854775806,\\"barcode\\":\\"0001\\"}"}');
  assert.equal(result.payloadJson, '{"id":9223372036854775806,"barcode":"0001"}');
});
