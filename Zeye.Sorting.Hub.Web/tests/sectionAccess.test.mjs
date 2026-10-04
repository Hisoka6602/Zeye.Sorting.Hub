import test from 'node:test';
import assert from 'node:assert/strict';
import { canAccessRestrictedSections, isRestrictedSection } from '../src/app/sectionAccess.ts';

test('三个敏感版块的菜单、直接地址、明细及编码路径共用访问边界', () => {
  for (const path of ['test-data', 'governance', 'observability', '/parcels/new', '/parcels/batch/', '/PARCELS/DETECTION/NEW',
    '/governance/sharding', '/governance/archive-tasks', '/governance/parcel-cleanup', '/audit/requests/123', '/diagnostics/slow-queries/test',
    '/diagnostics/health', '/%61udit/requests', '/diagnostics/%68ealth', '/%']) assert.equal(isRestrictedSection(path), true, path);
  for (const path of ['/parcels', '/parcels/123', '/parcels/newer', '/overview', '/data-overview', '/rules', '/analytics',
    '/settings', '/access', '/governance/backup', '/governance/archive-tasks-other', '/diagnostics-other']) assert.equal(isRestrictedSection(path), false, path);
});

test('敏感版块仅采用已登录会话的服务端超级管理员标志，单项权限不能替代身份', () => {
  assert.equal(canAccessRestrictedSections(), false);
  assert.equal(canAccessRestrictedSections({ authenticated: false, isSuperAdministrator: true }), false);
  assert.equal(canAccessRestrictedSections({ authenticated: true, isSuperAdministrator: false, permissions: ['access.manage', 'audit.read', 'governance.manage'] }), false);
  assert.equal(canAccessRestrictedSections({ authenticated: true }), false);
  assert.equal(canAccessRestrictedSections({ authenticated: true, isSuperAdministrator: 'true' }), false);
  assert.equal(canAccessRestrictedSections({ authenticated: true, isSuperAdministrator: true }), true);
});
