import test from 'node:test';
import assert from 'node:assert/strict';
import { ApiError, decodeApiResponse, requireHealthReport } from '../src/data/api/apiResponse.ts';
import { isRealtimeResource, selectRealtimeCommand, getRealtimeAuthenticated, setRealtimeAuthenticated, subscribeRealtimeAuthenticated } from '../src/data/api/realtimePolicy.ts';

test('实时快照保持长编号、处理原文以及 503 健康状态', () => {
  const response = decodeApiResponse(200, '{"id":9223372036854775806,"rawPayload":"{\\"id\\":9223372036854775806}"}');
  assert.equal(response.id, '9223372036854775806');
  assert.equal(response.rawPayload, '{"id":9223372036854775806}');
  assert.equal(decodeApiResponse(503, '{"status":"Unhealthy","entries":{}}', [503]).status, 'Unhealthy');
  assert.throws(() => decodeApiResponse(403, '{"detail":"权限已撤销"}'), error => error instanceof ApiError && error.status === 403);
});

test('命名提交保持编号原文，正文有界且禁止危险管理命令', () => {
  const body = '{"status":2}';
  assert.deepEqual(selectRealtimeCommand('/api/admin/parcels/9223372036854775806', 'PUT', body), { method: 'UpdateParcelStatus', arguments: ['9223372036854775806', body] });
  assert.equal(selectRealtimeCommand('/api/admin/parcels/processing-records', 'POST', body).method, 'AppendProcessingRecord');
  for (const path of ['/api/admin/parcels/cleanup-expired', '/api/access/users', '/api/admin/parcels/batch-buffer', '/api/admin/parcels/1?x=2', '/api/admin/parcels/0', '/api/admin/parcels/../access'])
    for (const method of ['POST', 'PUT', 'DELETE']) assert.equal(selectRealtimeCommand(path, method, body), undefined);
  assert.equal(selectRealtimeCommand('/api/admin/parcels/processing-records', 'POST', '中'.repeat(1366)), undefined);
  assert.equal(selectRealtimeCommand('/api/admin/parcels/processing-records', 'POST', ' '), undefined);
});

test('HTTP 与实时通道只接受真实健康报告', () => {
  assert.equal(requireHealthReport({ status: 'Unhealthy', entries: {} }).status, 'Unhealthy');
  assert.throws(() => requireHealthReport({ detail: '异常' }));
  assert.throws(() => requireHealthReport({ status: 'Healthy', entries: [] }));
});

test('危险动作、身份、文件和跨站地址不进入实时只读通道', () => {
  for (const path of ['/api/admin/parcels/cleanup-expired', '/api/admin/parcels', '/api/access', '/api/access/profile/avatar',
    '/api/operations/backup/artifacts/x/download', 'https://example.test/api/parcels', '//example.test/api/parcels',
    '/api/parcels/../access', '/api/parcels/%2e%2e/access', '/api/parcels#x', '/api/parcels/0', '/hubs/sorting']) assert.equal(isRealtimeResource(path), false, path);
  for (const path of ['/api/parcels', '/api/parcels?barCodeKeyword=SF%2B1', '/api/parcels/9223372036854775806/images', '/health/deep', '/api/operations/rules/exception']) assert.equal(isRealtimeResource(path), true, path);
});

test('会话状态变更只通知一次，退出后不建立匿名实时连接', () => {
  setRealtimeAuthenticated(false); let changes = 0;
  const unsubscribe = subscribeRealtimeAuthenticated(() => changes++);
  setRealtimeAuthenticated(true); setRealtimeAuthenticated(true); assert.equal(getRealtimeAuthenticated(), true); assert.equal(changes, 1);
  setRealtimeAuthenticated(false); assert.equal(getRealtimeAuthenticated(), false); assert.equal(changes, 2);
  unsubscribe(); setRealtimeAuthenticated(false); assert.equal(changes, 2);
});
