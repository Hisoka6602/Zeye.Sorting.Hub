import assert from 'node:assert/strict';
import test from 'node:test';
import { buildParcelProcessingTimeline } from '../src/features/parcels/parcelProcessingTimeline.ts';

/** 构造来源一致、保留完整原文的最小处理事实。 */
function fact(id, time, data, override = {}) {
  return { recordId: id, sourceInstanceId: 'fusion-a', sourceRunId: 'run-a', sourceParcelId: '948', parcelId: '123',
    stage: 3, occurredAt: `2026-10-05T21:55:14.${time}`, attemptNumber: 1, isSuccess: null,
    rawPayload: JSON.stringify(data), ...override };
}

/** 构造 Fusion 执行器发布的 started/completed 尝试事实。 */
function attempt(id, time, operation, outcome, operationId = operation, attemptNumber = 1, extra = {}) {
  return fact(id, time, { kind: 'provider-attempt', name: operation, category: 'EverydayChainHub', outcome,
    detail: JSON.stringify({ operation, outcome, operationId, attemptId: `${operationId}-${attemptNumber}`, attemptNumber }),
    ...extra });
}

/** HTTP 事实的 assignment 分类在旧协议中同时用于扫描上传和格口请求。 */
function http(id, time, override = {}) {
  return fact(id, time, { kind: 'provider-call', name: 'EverydayChainHub', category: 'assignment', outcome: 'HTTP成功',
    outcomeLevel: 'transport', statusCode: 200, request: 'POST /gateway', response: 'HTTP/1.1 200 OK', ...override });
}

test('截图中的六条事实显示两次业务调用，扫描上传与请求格口正确区分，原文均保留', () => {
  const records = [
    attempt('scan-start', '6480000', 'EverydayChainHub 扫描上传', 'started'), http('scan-http', '6680000'),
    attempt('scan-end', '6850000', 'EverydayChainHub 扫描上传', 'completed'),
    attempt('chute-start', '6980000', '目标格口分配', 'started'), http('chute-http', '7090000'),
    attempt('chute-end', '7190000', '目标格口分配', 'completed'),
  ];
  const before = structuredClone(records);
  const { items, events } = buildParcelProcessingTimeline(records.slice().reverse());
  assert.deepEqual(items.map(item => item.title), ['请求格口', '扫描上传']);
  assert.deepEqual(items.map(item => item.events.length), [3, 3]);
  assert.equal(events.length, 6);
  assert.equal(events.find(event => event.record.recordId === 'chute-http').title, '请求格口');
  assert.equal(items[0].state, '调用完成');
  assert.deepEqual(records, before);
  for (const event of events) assert.strictEqual(event.record, records.find(record => record.recordId === event.record.recordId));
});

test('读取嵌套的真实重试次数，每次尝试独立展示，不受后端阶段默认次数1影响', () => {
  const records = [attempt('s1', '100', '目标格口分配', 'started', 'operation', 1),
    attempt('e1', '200', '目标格口分配', 'unknown', 'operation', 1),
    attempt('s2', '300', '目标格口分配', 'started', 'operation', 2),
    http('h2', '350'), attempt('e2', '400', '目标格口分配', 'completed', 'operation', 2)];
  const { items } = buildParcelProcessingTimeline(records);
  assert.deepEqual(items.map(item => item.attemptNumber), [2, 1]);
  assert.equal(items[0].events.length, 3);
  assert.equal(items[1].state, '结果未知');
});

test('HTTP 200 和 completed 均不推断业务已接受，明确业务拒绝才显示业务未接受', () => {
  const first = buildParcelProcessingTimeline([http('http', '350')]);
  assert.equal(first.items[0].title, 'Provider 交互');
  assert.equal(first.items[0].state, 'HTTP成功');
  assert.equal(first.items[0].color, 'blue');
  const { items } = buildParcelProcessingTimeline([attempt('s', '100', '目标格口分配', 'started'),
    attempt('e', '200', '目标格口分配', 'completed', '目标格口分配', 1, { response: JSON.stringify({ IsAssigned: false }) })]);
  assert.equal(items[0].state, '业务未接受');
  assert.equal(items[0].color, 'red');
});

test('Fusion 明确关闭扫描上传时显示已跳过，不把放行结果当作实际接口受理', () => {
  const { items } = buildParcelProcessingTimeline([attempt('s', '100', '扫描上传', 'started'),
    attempt('e', '200', '扫描上传', 'completed', '扫描上传', 1, { response: JSON.stringify({ IsAccepted: true, RawResponse: 'Scan upload disabled by config.' }) })]);
  assert.equal(items[0].state, '已跳过');
  assert.equal(items[0].events.length, 2);
});

test('超时后收到迟到响应仍为结果未知，响应记录可以展开追溯', () => {
  const { items } = buildParcelProcessingTimeline([attempt('s', '100', '目标格口分配', 'started'),
    attempt('timeout', '200', '目标格口分配', 'unknown'),
    attempt('late', '500', '目标格口分配', 'late-completion', '目标格口分配', 1, { response: JSON.stringify({ IsAssigned: true }) })]);
  assert.equal(items.length, 1);
  assert.equal(items[0].state, '结果未知');
  assert.equal(items[0].events[2].state, '迟到响应');
});

test('重叠调用没有明确尝试标识时禁止猜测归组，不把扫描 HTTP 误认成格口请求', () => {
  const { items, events } = buildParcelProcessingTimeline([
    attempt('s1', '100', '扫描上传', 'started'), attempt('s2', '150', '目标格口分配', 'started'), http('http', '200'),
    attempt('e1', '300', '扫描上传', 'completed'), attempt('e2', '350', '目标格口分配', 'completed'),
  ]);
  assert.equal(items.length, 3);
  assert.equal(events.find(event => event.record.recordId === 'http').title, 'Provider 交互');
  assert.equal(items.flatMap(item => item.events).length, 5);
});

test('来源实例、编号会话和包裹不同，即使操作标识相同也不合并', () => {
  const records = [attempt('a', '100', '目标格口分配', 'started'),
    { ...attempt('b', '200', '目标格口分配', 'completed'), sourceInstanceId: 'fusion-b' },
    { ...attempt('c', '300', '目标格口分配', 'completed'), sourceRunId: 'run-b' },
    { ...attempt('d', '400', '目标格口分配', 'completed'), sourceParcelId: '949' }];
  assert.equal(buildParcelProcessingTimeline(records).items.length, 4);
});

test('HTTP 事实携带明确尝试标识时优先使用身份关联，时间重叠也不会丢失归属', () => {
  const { items } = buildParcelProcessingTimeline([attempt('s1', '100', '扫描上传', 'started'),
    attempt('s2', '100', '目标格口分配', 'started'),
    http('http', '200', { detail: JSON.stringify({ operationId: '目标格口分配', attemptId: '目标格口分配-1', attemptNumber: 1 }) }),
    attempt('e1', '300', '扫描上传', 'completed'), attempt('e2', '300', '目标格口分配', 'completed')]);
  assert.equal(items.length, 2);
  assert.equal(items.find(item => item.title === '请求格口').events.length, 3);
});

test('落格回传的操作与传输事件归组，实际落格和格口决策仍为独立事实', () => {
  const landing = (id, time, status) => fact(id, time, { operation: 'landing', operationId: 'landing-1', status }, { stage: 8 });
  const { items } = buildParcelProcessingTimeline([landing('start', '100', 'started'), http('http', '200', { category: 'landing' }),
    landing('end', '300', 'completed'), fact('physical', '050', {}, { stage: 6 }), fact('decision', '040', {}, { stage: 4 })]);
  assert.equal(items.length, 3);
  assert.equal(items.find(item => item.title === '落格回传').events.length, 3);
  assert.ok(items.some(item => item.title === '实际落格'));
  assert.ok(items.some(item => item.title === '格口分配'));
});

test('历史缺少或截断元数据时保留全部记录，不崩溃、不按相同阶段合并', () => {
  const records = [fact('legacy', '100', {}, { rawPayload: null }), fact('broken', '200', {}, { rawPayload: '{"detail":' }),
    fact('other', '300', {}, { stage: 42 })];
  const { items, events } = buildParcelProcessingTimeline(records);
  assert.equal(items.length, 3);
  assert.equal(events.length, 3);
  assert.equal(events.find(event => event.record.recordId === 'legacy').title, '扫描上传');
  assert.equal(items[0].title, '42');
  assert.deepEqual(buildParcelProcessingTimeline([]), { events: [], items: [] });
});

test('不同小数秒精度按本地时间精确比较，不合并窗口外事实', () => {
  const { items, events } = buildParcelProcessingTimeline([attempt('start', '1', '目标格口分配', 'started'),
    http('in', '1000001'), http('out', '2000001'), attempt('end', '2', '目标格口分配', 'completed')]);
  assert.equal(items.length, 2);
  assert.equal(events.find(event => event.record.recordId === 'in').title, '请求格口');
  assert.equal(events.find(event => event.record.recordId === 'out').title, 'Provider 交互');
});

test('明确的落格 HTTP 分类不会被相邻的格口请求窗口改写', () => {
  const { items, events } = buildParcelProcessingTimeline([attempt('s', '100', '目标格口分配', 'started'),
    http('landing', '200', { category: 'landing' }), attempt('e', '300', '目标格口分配', 'completed')]);
  assert.equal(items.length, 2);
  assert.equal(events.find(event => event.record.recordId === 'landing').title, '落格回传');
});
