import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { describeAuditRequest } from '../src/features/observability/requestDescriptions.ts';

/** 递归读取入口源码，排除编译产物，覆盖新的路由文件和扩展注册位置。 */
function readHostSources(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const url = new URL(entry.name + (entry.isDirectory() ? '/' : ''), directory);
    if (entry.isDirectory()) return ['bin', 'obj', 'wwwroot', 'logs'].includes(entry.name) ? [] : readHostSources(url);
    return entry.name.endsWith('.cs') ? [{ file: entry.name, source: readFileSync(url, 'utf8') }] : [];
  });
}

test('请求说明及服务端中文文档覆盖全部业务路由、健康探测与 SignalR 隐式端点', context => {
  const host = new URL('../../Zeye.Sorting.Hub.Host/', import.meta.url);
  const protocol = readFileSync(new URL('../../Zeye.Sorting.Hub.Infrastructure/Integrations/Fusion/FusionProtocol.cs', import.meta.url), 'utf8');
  const constants = Object.fromEntries([...protocol.matchAll(/const\s+string\s+(\w+)\s*=\s*("[^"]*")/g)].map(match => ['FusionProtocol.' + match[1], JSON.parse(match[2])]));
  let checked = 0;
  let hubs = 0;
  let healthChecks = 0;
  for (const { file, source } of readHostSources(host)) {
    const groups = Object.fromEntries([...source.matchAll(/var\s+(\w+)\s*=\s*\w+\s*\.Map(?:Group|BusinessModuleGroup)\("([^"]+)"/g)].map(match => [match[1], match[2]]));
    const endpoints = [...source.matchAll(/(\w+)\.Map(Get|Post|Put|Patch|Delete|HealthChecks|Hub<[^>]+>)\(\s*("[^"]*"|string\.Empty|[\w.]+)/g)];
    for (const [index, match] of endpoints.entries()) {
      const prefix = groups[match[1]] ?? '';
      const suffix = match[3] === 'string.Empty' ? '' : match[3].startsWith('"') ? JSON.parse(match[3]) : constants[match[3]];
      assert.notEqual(suffix, undefined, `${file} 无法解析路由常量 ${match[3]}`);
      const template = prefix + suffix;
      const declaration = source.slice(match.index, endpoints[index + 1]?.index ?? source.length);
      assert.ok(declaration.includes('.WithBusinessModuleEndpointConvention(')
        || /\.WithSummary\("[^"\n]*[\u4e00-\u9fff][^"\n]*"\)/u.test(declaration)
          && /\.WithDescription\("[^"\n]*[\u4e00-\u9fff][^"\n]*"\)/u.test(declaration), `${file} ${template} 缺少服务端中文摘要或业务说明`);
      const paths = (template.includes('{category}') ? ['parcel', 'exception'] : ['parcel']).map(category =>
        template.replace(/\{([^}:]+)(?::[^}]+)?\}/g, (_, parameter) => parameter === 'category' ? category : parameter === 'id' ? '9223372036854775806' : 'sample-fingerprint'));
      const isHub = match[2].startsWith('Hub<');
      if (isHub) hubs++;
      if (match[2] === 'HealthChecks') healthChecks++;
      const requests = isHub ? [['GET', template], ['POST', template], ['DELETE', template], ['POST', template + '/negotiate']]
        : paths.map(path => [match[2] === 'HealthChecks' ? 'GET' : match[2], path]);
      for (const [method, path] of requests) {
        const description = describeAuditRequest(method, path);
        assert.doesNotMatch(description, /未配置/, `${method} ${template} 缺少业务说明`);
        assert.match(description, /[\u4e00-\u9fff]/u);
        checked++;
      }
    }
  }
  assert.ok(checked >= 61, '未完整读取服务端路由');
  assert.equal(hubs, 2, '必须覆盖浏览器与 Fusion 两个实时通道');
  assert.equal(healthChecks, 3, '必须覆盖三种健康探测');
  context.diagnostic(`已校验 ${checked} 项请求说明，包含 ${hubs} 个 SignalR 通道。`);
});

test('SignalR 说明区分机器通道、页面通道与协商、发送和断开意图', () => {
  assert.match(describeAuditRequest('GET', '/hubs/fusion-ingestion?id=connection-1'), /Fusion 工作台实时通道.*处理事实、心跳及包裹图片/);
  assert.match(describeAuditRequest('POST', '/hubs/fusion-ingestion/negotiate?negotiateVersion=1'), /协商 Fusion 工作台/);
  assert.match(describeAuditRequest('POST', '/hubs/fusion-ingestion'), /提交工作台登记/);
  assert.match(describeAuditRequest('DELETE', '/hubs/fusion-ingestion'), /关闭 Fusion 工作台/);
  assert.match(describeAuditRequest('GET', '/hubs/sorting'), /管理页面实时通道/);
  assert.match(describeAuditRequest('POST', '/hubs/sorting/negotiate'), /协商管理页面/);
  assert.match(describeAuditRequest('DELETE', '/hubs/sorting'), /关闭管理页面/);
  assert.match(describeAuditRequest('GET', '/hubs/fusion-ingestion/negotiate'), /未配置/);
  assert.match(describeAuditRequest('PUT', '/hubs/sorting'), /未配置/);
  assert.match(describeAuditRequest('GET', '/hubs/fusion-ingestion/unknown'), /未配置/);
});

test('Fusion 查询与配置维护拥有独立说明，清理说明只描述永久操作汇总', () => {
  assert.match(describeAuditRequest('GET', '/api/diagnostics/fusion/facts?sourceInstanceId=source-01'), /原始处理事实与投影结果/);
  assert.match(describeAuditRequest('GET', '/api/parcels/fusion/images/image-key/content'), /完整包裹图片/);
  assert.match(describeAuditRequest('PUT', '/api/operations/configuration/fusion/sources/source-01'), /更新已登记来源工作台/);
  assert.match(describeAuditRequest('POST', '/api/operations/configuration/fusion/sources/source-01/rotate-key'), /轮换.*机器认证密钥/);
  assert.match(describeAuditRequest('POST', '/api/admin/parcels/cleanup-expired'), /操作汇总/);
  assert.doesNotMatch(describeAuditRequest('GET', '/api/admin/parcels/cleanup-history/operation-01'), /清单/);
});

test('同一路径区分读取和修改，未知方法不冒充已知业务操作', () => {
  assert.match(describeAuditRequest('GET', '/api/operations/configuration/runtime'), /读取.*生效状态/);
  assert.match(describeAuditRequest('PUT', '/api/operations/configuration/runtime'), /按已读取版本保存.*下次启动生效/);
  assert.match(describeAuditRequest('GET', '/api/operations/configuration/history'), /脱敏配置变更历史.*修改前后值/);
  assert.match(describeAuditRequest('POST', '/api/operations/configuration/runtime'), /未配置/);
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
