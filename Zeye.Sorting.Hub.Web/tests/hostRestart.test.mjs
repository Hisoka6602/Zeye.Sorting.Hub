import { test } from 'node:test';
import assert from 'node:assert/strict';
import { waitForHostRestart } from '../src/data/api/hostRestart.ts';

test('短暂断线和旧实例响应都不能宣告重启成功', async () => {
  const controller = new AbortController();
  let reads = 0;
  const result = await waitForHostRestart('before', controller.signal, async () => {
    reads++;
    if (reads === 1) throw new TypeError('连接中断');
    return { instanceId: reads === 2 ? 'before' : 'after', ready: true, requiresConfiguration: false, localSetupAllowed: true, setupKeyPath: null };
  }, 1, 1000);
  assert.equal(reads, 3); assert.equal(result.instanceId, 'after');
});

test('新实例数据库仍未就绪时明确返回配置状态', async () => {
  const status = { instanceId: 'after', ready: false, requiresConfiguration: true, localSetupAllowed: true, setupKeyPath: '/protected/database-setup.key' };
  assert.deepEqual(await waitForHostRestart('before', new AbortController().signal, async () => status, 1, 1000), status);
});

test('取消等待后停止轮询，不再刷新已离开的页面', async () => {
  const controller = new AbortController();
  let reads = 0;
  await assert.rejects(waitForHostRestart('before', controller.signal, async () => {
    reads++; controller.abort(new Error('页面已关闭')); throw new TypeError('连接中断');
  }, 1, 1000), /页面已关闭/);
  assert.equal(reads, 1);
});

test('旧实例持续存在时等待超时，不能报重启成功', async () => {
  await assert.rejects(waitForHostRestart('before', new AbortController().signal,
    async () => ({ instanceId: 'before' }), 1, 15), /Host 尚未恢复/);
});
