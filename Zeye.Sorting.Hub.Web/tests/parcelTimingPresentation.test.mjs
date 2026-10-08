import assert from 'node:assert/strict';
import test from 'node:test';
import { buildParcelTimingRows, createTimingScale, parcelTimingNodes, parseTimingTime, timingPosition } from '../src/features/parcels/parcelTimingModel.ts';
import { groupTimingDiagnostics, layoutTimingOverview, timingAxisTicks, timingOverview, timingOverviewSpans } from '../src/features/parcels/parcelTimingPresentation.ts';

const stamp = ms => '2026-10-07T10:00:00.' + String(ms).padStart(3, '0');
const parcel = overrides => ({ id: '9223372036854775806', barCodes: 'PKG', scannedTime: stamp(200),
  detectedTime: null, dischargeTime: null, completedTime: null, processingRecords: [], apiRequests: [], ...overrides });
const row = overrides => buildParcelTimingRows({ anchorId: '9223372036854775806', items: [parcel(overrides)], beforeCount: 0, afterCount: 0 })[0];
const call = (start, end, status = 1) => ({ apiType: 0, requestStatus: status, requestTime: stamp(start), responseTime: end === null ? null : stamp(end), elapsedMilliseconds: 999 });
const fact = (stage, at) => ({ recordId: `${stage}-${at}`, parcelId: '9223372036854775806', stage, occurredAt: stamp(at),
  requestAt: null, responseAt: null, rawPayload: null, isSuccess: true });

test('概览从首次检测连接扫码并覆盖落格回传，扫码到请求的蓝色等待与紫色调用窗口仍独立', () => {
  const value = row({ detectedTime: stamp(50), createdTime: '2026-10-09T10:00:00.500',
    apiRequests: [call(300, 350), { apiType: 3, requestStatus: 1, requestTime: stamp(750), responseTime: stamp(800), elapsedMilliseconds: 50 }],
    processingRecords: [fact(2, 200), fact(5, 450), fact(6, 700)],
  });
  const overview = timingOverview(value);
  assert.equal(overview.milestones[0].title, '分拣机检测');
  assert.equal(overview.milestones.at(-1).title, '落格报告');
  assert.equal(overview.detected.start, parseTimingTime(stamp(50)));
  assert.equal(overview.landing.start, parseTimingTime(stamp(700)));
  assert.equal(overview.scanToRequest, 100);
  assert.deepEqual(timingOverviewSpans(value).map(span => [span.kind, span.label, span.partial]), [
    ['detection', '首次检测 → 扫码', false], ['wait', '扫码 → 首次请求', false],
  ]);
  const aligned = createTimingScale([value], 'detected');
  assert.ok(timingAxisTicks(aligned, 680).includes(0));
  const detection = layoutTimingOverview(value, aligned, 680, []).find(point => point.mark.kind === 'detection');
  assert.equal(detection.label, '首次检测');
  assert.notEqual(detection.labelLane, null);
});

test('完整请求沿用真实蓝色等待和紫色调用端点，格口分配事实不替代首次请求', () => {
  const value = row({ apiRequests: [call(300, 350)], processingRecords: [fact(4, 400), fact(5, 450)] });
  const spans = timingOverviewSpans(value);
  assert.equal(spans.length, 1);
  assert.equal(spans[0].start.start, parseTimingTime(stamp(200)));
  assert.equal(spans[0].end.start, parseTimingTime(stamp(300)));
  assert.equal(spans[0].partial, false);
  assert.equal(timingOverview(value).request.end, parseTimingTime(stamp(350)));
});

test('没有请求起止时展示已记录的格口分配与分拣阶段，右侧首次请求耗时仍未知', () => {
  const value = row({ processingRecords: [fact(4, 300), fact(5, 450), fact(6, 700)] });
  const overview = timingOverview(value);
  const spans = timingOverviewSpans(value);
  assert.equal(overview.request, null);
  assert.equal(overview.scanToRequest, null);
  assert.ok(overview.milestones.some(mark => mark.title === '格口分配'));
  assert.deepEqual(spans.map(span => [span.kind, span.label, span.partial]), [
    ['wait', '扫码 → 格口分配', true], ['chute-stage', '格口分配 → 分拣指令', true],
  ]);
  assert.equal(spans[1].start.start, parseTimingTime(stamp(300)));
  assert.equal(spans[1].end.start, parseTimingTime(stamp(450)));
  assert.equal(spans[1].start.end, null);
});

test('只有响应或只有分拣时不补造紫色请求窗口，零值、倒序和缺失端点保持原义', () => {
  const reply = fact(3, 300);
  reply.responseAt = stamp(300);
  reply.rawPayload = JSON.stringify({ kind: 'provider-attempt', operation: '请求格口', outcome: 'completed' });
  const response = row({ processingRecords: [reply] });
  assert.equal(timingOverview(response).request, null);
  assert.deepEqual(timingOverviewSpans(response).map(span => [span.kind, span.label]), [['wait', '扫码 → 格口响应']]);
  assert.equal(timingOverview(response).chute.end, null);
  assert.equal(layoutTimingOverview(response, createTimingScale([response], 'absolute'), 520, []).find(point => point.mark.title === '请求格口').label, '格口响应');
  assert.deepEqual(timingOverviewSpans(row({ processingRecords: [fact(5, 300)] })).map(span => span.label), ['扫码 → 分拣指令']);
  const inverted = timingOverviewSpans(row({ processingRecords: [fact(4, 200), fact(5, 100)] }));
  assert.equal(inverted[0].start.start, inverted[0].end.start);
  assert.ok(inverted[1].end.start < inverted[1].start.start);
  assert.deepEqual(timingOverviewSpans(row({ scannedTime: null, processingRecords: [fact(5, 300)] })), []);
  assert.deepEqual(timingOverviewSpans(row()), []);
});

test('扫码到首次请求包含失败尝试，成功重试不替换等待时间或删除原始窗口', () => {
  const value = row({ apiRequests: [call(300, 350, 2), call(500, 550)] });
  const overview = timingOverview(value);
  assert.equal(overview.scanToRequest, 100);
  assert.equal(overview.request.issue, true);
  assert.equal(overview.diagnosticCount, 1);
  assert.equal(value.marks.filter(mark => mark.title === '请求格口').length, 2);
  assert.equal(overview.milestones.filter(mark => mark.title === '请求格口').length, 1);
});

test('零间隔有效，负间隔保留，缺扫码或缺请求均不补造耗时', () => {
  assert.equal(timingOverview(row({ apiRequests: [call(200, null)] })).scanToRequest, 0);
  assert.equal(timingOverview(row({ apiRequests: [call(100, 150)] })).scanToRequest, -100);
  assert.equal(timingOverview(row({ scannedTime: null, apiRequests: [call(300, null)] })).scanToRequest, null);
  assert.equal(timingOverview(row({ detectedTime: stamp(100) })).scanToRequest, null);
  assert.equal(timingOverview(row({ apiRequests: [call(300, null)] })).request.end, null);
});

test('概览和选中隐藏动作沿用原始坐标，七位精度与响应窗口不受标签避让影响', () => {
  const value = row({ detectedTime: '2026-10-07T10:00:00.1999999', dischargeTime: stamp(600), completedTime: stamp(610),
    apiRequests: [call(250, 285), call(400, null)] });
  const scale = createTimingScale([value], 'absolute');
  const selected = parcelTimingNodes([value]).filter(node => node.mark.title === '分拣机检测' || node.mark.key.endsWith('api:1'));
  const layout = layoutTimingOverview(value, scale, 680, selected);
  for (const point of layout) {
    assert.ok(Math.abs(point.startX / 680 - timingPosition(point.mark.start, value, scale)) < 1e-12);
    if (point.mark.end !== null) assert.ok(Math.abs(point.endX / 680 - timingPosition(point.mark.end, value, scale)) < 1e-12);
    else assert.equal(point.endX, null);
  }
  assert.equal(layout.find(point => point.mark.title === '分拣机检测').mark.start, parseTimingTime('2026-10-07T10:00:00.1999999'));
  assert.ok(layout.some(point => point.mark.key.endsWith('api:1')));
  assert.equal(value.marks.length, 6);
});

test('同一时刻的关键点不会被横向挪动，扫码与请求标签仍可直接阅读', () => {
  const value = row({ dischargeTime: stamp(200), completedTime: stamp(200), apiRequests: [call(200, 200)] });
  const scale = createTimingScale([value], 'scan');
  const layout = layoutTimingOverview(value, scale, 520, []);
  assert.equal(new Set(layout.map(point => point.startX)).size, 1);
  const scan = layout.find(point => point.mark.title === '扫码');
  const request = layout.find(point => point.mark.title === '请求格口');
  assert.notEqual(scan.labelLane, null);
  assert.notEqual(request.labelLane, null);
  assert.notEqual(scan.labelLane, request.labelLane);
  assert.equal(request.startX, request.endX);
});

test('只有格口响应不冒充请求开始或零耗时窗口，默认比较保留未知', () => {
  const value = row({ processingRecords: [{
    recordId: 'reply', parcelId: '9223372036854775806', sourceInstanceId: 'line-01', sourceRunId: 'run-a', sourceParcelId: '1',
    stage: 3, occurredAt: stamp(320), requestAt: null, responseAt: stamp(300), attemptNumber: 1, isSuccess: true,
    rawPayload: JSON.stringify({ kind: 'provider-attempt', operation: '请求格口', operationId: 'chute-1', attemptId: 'chute-1-1', outcome: 'completed', businessAccepted: true }),
  }] });
  const mark = value.marks.find(mark => mark.title === '请求格口');
  assert.equal(mark.start, parseTimingTime(stamp(300)));
  assert.equal(mark.startLabel, '响应');
  assert.equal(mark.end, null);
  assert.match(mark.warning, /未提供请求/);
  assert.equal(timingOverview(value).request, null);
  assert.equal(timingOverview(value).scanToRequest, null);
  assert.equal(parcelTimingNodes([value]).filter(node => node.mark === mark).length, 1);
  assert.equal(parcelTimingNodes([value]).find(node => node.mark === mark).label, '请求格口 · 响应');
});

test('密集诊断聚合保留每个动作并锚在真实发生时刻，远处提醒保持独立', () => {
  const value = row({ apiRequests: [call(300, 310, 2), call(301, 311, 2), call(302, 312, 2), call(900, null)] });
  Object.freeze(value.marks);
  const scale = createTimingScale([value], 'absolute');
  const groups = groupTimingDiagnostics(value, scale, 680);
  assert.equal(groups.length, 2);
  assert.equal(groups[0].marks.length, 3);
  assert.equal(groups[0].issue, true);
  assert.equal(groups[1].issue, false);
  assert.equal(groups[0].x, timingPosition(groups[0].marks[0].start, value, scale) * 680);
  assert.equal(groups[0].endX, timingPosition(groups[0].marks[2].start, value, scale) * 680);
  assert.deepEqual(groups.flatMap(group => group.marks.map(mark => mark.key)).sort(), value.marks.filter(mark => mark.issue || mark.warning).map(mark => mark.key).sort());
});

test('刻度使用等距整值，扫码零点可见，绝对轴整刻度和空轴均保持有限', () => {
  const value = row({ detectedTime: stamp(123), completedTime: '2026-10-07T10:00:03.123' });
  const absolute = createTimingScale([value], 'absolute');
  const ticks = timingAxisTicks(absolute, 900);
  assert.ok(ticks.length >= 2 && ticks.length <= 10);
  const step = ticks[1] - ticks[0];
  for (let i = 1; i < ticks.length; i++) {
    assert.ok(Math.abs(ticks[i] - ticks[i - 1] - step) < 1e-8);
    assert.ok((ticks[i] - ticks[i - 1]) / (absolute.max - absolute.min) * 900 >= 90);
  }
  for (const tick of ticks) assert.equal((absolute.origin + BigInt(Math.round(tick * 10000))) % BigInt(Math.round(step * 10000)), 0n);
  const scan = createTimingScale([value], 'scan');
  assert.ok(timingAxisTicks(scan, 520).includes(0));
  assert.ok(timingAxisTicks(createTimingScale([], 'absolute'), 520).every(Number.isFinite));
});
