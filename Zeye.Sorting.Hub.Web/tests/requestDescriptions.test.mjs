import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { describeAuditRequest } from '../src/features/observability/requestDescriptions.ts';

test('请求说明覆盖服务端已注册的业务接口，包括带参数的路由', () => {
  const routing = new URL('../../Zeye.Sorting.Hub.Host/Routing/', import.meta.url);
  let checked = 0;
  for (const file of readdirSync(routing).filter(name => name.endsWith('ApiRouteExtensions.cs'))) {
    const source = readFileSync(new URL(file, routing), 'utf8');
    const groups = Object.fromEntries([...source.matchAll(/var\s+(\w+)\s*=\s*\w+\s*\.MapGroup\("([^"]+)"\)/g)].map(match => [match[1], match[2]]));
    for (const match of source.matchAll(/(\w+)\.Map(Get|Post|Put|Patch|Delete)\(("[^"]*"|string\.Empty)/g)) {
      const prefix = groups[match[1]] ?? '';
      const suffix = match[3] === 'string.Empty' ? '' : JSON.parse(match[3]);
      const template = prefix + suffix;
      const path = template.replace(/\{([^}:]+)(?::[^}]+)?\}/g, (_, parameter) => parameter === 'category' ? 'parcel' : parameter === 'id' ? '9223372036854775806' : 'sample-fingerprint');
      const description = describeAuditRequest(match[2], path);
      assert.doesNotMatch(description, /未配置/, `${match[2]} ${template} 缺少业务说明`);
      assert.match(description, /[\u4e00-\u9fff]/u);
      checked++;
    }
  }
  assert.ok(checked >= 40, '未完整读取服务端路由');
});

test('同一路径区分读取和修改，未知方法不冒充已知业务操作', () => {
  assert.match(describeAuditRequest('GET', '/api/access/profile'), /读取/);
  assert.match(describeAuditRequest('PUT', '/api/access/profile'), /保存/);
  assert.match(describeAuditRequest('DELETE', '/api/access/profile'), /未配置/);
  assert.match(describeAuditRequest('GET', '/api/operations/backup/artifacts'), /查询/);
  assert.match(describeAuditRequest('POST', '/api/operations/backup/artifacts'), /创建/);
  assert.match(describeAuditRequest('PUT', '/api/operations/rules/exception'), /保存异常分类规则/);
});

test('历史路径兼容大小写、查询参数和尾斜杠，动态编号不丢失精度', () => {
  assert.equal(describeAuditRequest(' get ', '/API/PARCELS/?pageNumber=2'), describeAuditRequest('GET', '/api/parcels'));
  assert.match(describeAuditRequest('GET', '/api/audit/web-requests/9223372036854775806'), /审计详情/);
  assert.match(describeAuditRequest('POST', '/api/operations/backup/artifacts/backup-2026/restore-isolated'), /隔离数据库/);
  assert.match(describeAuditRequest('GET', '/api/parcels/not-a-number'), /未配置/);
  assert.match(describeAuditRequest('GET', '/api/parcels/analytics/extra'), /未配置/);
  assert.match(describeAuditRequest('GET', '/api/unknown'), /未配置/);
});
