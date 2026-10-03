import test from 'node:test';
import assert from 'node:assert/strict';
import { canManageParcelTests, isParcelTestPath } from '../src/features/parcels/testAccess.ts';

test('手工测试页面，包括直接访问和大小写路径，均需要管理员保护', () => {
  for (const path of ['/parcels/new', '/parcels/batch/', '/PARCELS/DETECTION/NEW']) assert.equal(isParcelTestPath(path), true);
  for (const path of ['/parcels', '/parcels/123', '/parcels/newer', '/overview']) assert.equal(isParcelTestPath(path), false);
});

test('测试入口只对已登录管理员开放，普通包裹写权限不允许手工新建', () => {
  assert.equal(canManageParcelTests(), false);
  assert.equal(canManageParcelTests({ authenticated: false, permissions: ['access.manage'] }), false);
  assert.equal(canManageParcelTests({ authenticated: true, permissions: ['parcels.read', 'parcels.write'] }), false);
  assert.equal(canManageParcelTests({ authenticated: true, permissions: ['access.manage'] }), true);
});
