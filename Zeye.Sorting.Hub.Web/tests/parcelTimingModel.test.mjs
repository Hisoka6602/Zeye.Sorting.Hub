import assert from 'node:assert/strict';
import test from 'node:test';
import { buildParcelTimingRows, createTimingScale, formatTimingMilliseconds, formatTimingTime, layoutTimingMarks,
  parcelTimingNodes, parseTimingTime, timingInterval, timingPosition, validTimingParcelId } from '../src/features/parcels/parcelTimingModel.ts';
import { buildParcelProcessingTimeline } from '../src/features/parcels/parcelProcessingTimeline.ts';
import { timingOverview, timingOverviewSpans } from '../src/features/parcels/parcelTimingPresentation.ts';

const scan = '2026-10-07T10:00:00.0000000';
const parcel = (id = '9223372036854775806', overrides = {}) => ({ id, barCodes: `PKG-${id}`, scannedTime: scan,
  detectedTime: null, dischargeTime: null, completedTime: null, processingRecords: [], apiRequests: [], ...overrides });
const context = (items, anchorId = items[0].id) => ({ items, anchorId, beforeCount: 0, afterCount: items.length - 1 });
const record = (id, time, outcome, overrides = {}) => ({ recordId: id, parcelId: '9223372036854775806',
  sourceInstanceId: 'source-a', sourceRunId: 'run-a', sourceParcelId: '12', stage: 3, occurredAt: time,
  requestAt: null, responseAt: null, attemptNumber: 1, isSuccess: null,
  rawPayload: JSON.stringify({ kind: 'provider-attempt', operation: '请求格口', operationId: 'chute-1', attemptId: 'chute-1-1', attemptNumber: 1, outcome }), ...overrides });

test('业务起点取最早真实检测，补传的Hub创建时间不会进入业务节点或扩大时间轴', () => {
  const detected = '2026-10-07T10:00:00.0500001';
  const created = '2026-10-09T10:00:00.9999999';
  const value = buildParcelTimingRows(context([parcel(undefined, { scannedTime: '2026-10-07T10:00:00.200',
    createdTime: created, detectedTime: '2026-10-07T10:00:00.001', processingRecords: [
      record('detected-later', '2026-10-07T10:00:00.100', '', { stage: 0, rawPayload: null }),
      record('detected-first', detected, '', { stage: 0, rawPayload: null }),
      record('landed', '2026-10-07T10:00:00.900', '', { stage: 6, rawPayload: null }),
    ],
  })]))[0];
  assert.equal(value.detected, parseTimingTime(detected));
  assert.equal(timingOverview(value).detected.start, parseTimingTime(detected));
  assert.equal(timingOverview(value).landing.start, parseTimingTime('2026-10-07T10:00:00.900'));
  assert.equal(value.marks[0].kind, 'detection');
  assert.ok(parcelTimingNodes([value]).every(node => node.ticks !== parseTimingTime(created)));
  assert.ok(createTimingScale([value], 'absolute').max < 1000);
  assert.equal(value.parcel.createdTime, created);
});

test('检测对齐保留每票真实起点，扫码对齐及节点间隔计算维持各自口径', () => {
  const rows = buildParcelTimingRows(context([
    parcel('101', { detectedTime: '2026-10-07T10:00:00.100', scannedTime: '2026-10-07T10:00:00.300' }),
    parcel('102', { detectedTime: '2026-10-07T10:00:01.100', scannedTime: '2026-10-07T10:00:01.400' }),
  ]));
  const aligned = createTimingScale(rows, 'detected');
  assert.equal(timingPosition(rows[0].detected, rows[0], aligned), timingPosition(rows[1].detected, rows[1], aligned));
  assert.ok(timingPosition(rows[0].scan, rows[0], aligned) < timingPosition(rows[1].scan, rows[1], aligned));
  assert.equal(timingInterval(rows[0].detected, rows[1].detected), 1000);
  const scans = createTimingScale(rows, 'scan');
  assert.equal(timingPosition(rows[0].scan, rows[0], scans), timingPosition(rows[1].scan, rows[1], scans));
});

test('缺失检测时不以Hub入库或扫码冒充起点，检测事实无效时仅使用真实检测摘要', () => {
  const without = buildParcelTimingRows(context([parcel(undefined, { createdTime: scan })]))[0];
  assert.equal(without.detected, null);
  assert.equal(timingOverview(without).detected, null);
  const emptyAligned = createTimingScale([without], 'detected');
  assert.ok(Number.isFinite(emptyAligned.min) && emptyAligned.max > emptyAligned.min);
  const fallback = buildParcelTimingRows(context([parcel(undefined, { detectedTime: scan, processingRecords: [
    record('invalid-detection', '0001-01-01T00:00:00', '', { stage: 0, rawPayload: null }),
  ] })]))[0];
  assert.equal(fallback.detected, parseTimingTime(scan));
  assert.equal(fallback.invalidTimeCount, 1);
  assert.equal(fallback.marks.filter(mark => mark.kind === 'detection').length, 1);
});

test('Fusion空响应调用开始与精简元数据沿用详情轨迹，扫描上传、请求格口、回传窗口均保留', () => {
  const time = value => `2026-10-06T10:24:${value}`;
  const full = [];
  const compact = [];
  for (const [operation, category, times, accepted] of [
    ['扫描上传', 'scan-upload', ['31.213', '31.218', '31.222'], { IsAccepted: true }],
    ['目标格口分配', 'assignment', ['31.255', '31.259', '31.264'], { IsAssigned: true }],
    ['落格回传', 'landing', ['32.607', '32.610', '32.622'], {}],
  ]) {
    for (const [index, outcome] of ['started', 'HTTP成功', 'completed'].entries()) {
      const transport = index === 1;
      const detail = { operationId: operation, attemptId: `${operation}-1`, attemptNumber: 1, operation, outcome };
      const metadata = { kind: transport ? 'provider-call' : 'provider-attempt',
        name: transport ? 'EverydayChainHub' : operation, category: transport ? category : 'EverydayChainHub', outcome,
        outcomeLevel: transport ? 'transport' : 'operation' };
      const fact = record(`${operation}-${index}`, time(times[index]), outcome, {
        requestAt: null, responseAt: null, errorMessage: transport ? null : JSON.stringify(detail),
        rawPayload: JSON.stringify({ ...metadata, detail: transport ? '' : JSON.stringify(detail), request: 'body',
          response: index === 0 ? '' : JSON.stringify(transport ? { transportOk: true } : accepted) }),
      });
      full.push(fact);
      compact.push({ ...fact, rawPayload: JSON.stringify({ ...metadata,
        ...(transport ? {} : { detail }), ...(index === 2 ? { response: accepted } : {}) }) });
    }
  }
  const detail = buildParcelProcessingTimeline(full);
  const timing = buildParcelTimingRows(context([parcel(undefined, {
    scannedTime: time('31.109'), processingRecords: compact,
  })]))[0];
  assert.equal(detail.items.length, 3);
  assert.deepEqual(timing.marks.filter(mark => mark.source === '处理事实').map(mark => mark.title),
    ['扫描上传', '请求格口', '落格回传']);
  assert.deepEqual(buildParcelProcessingTimeline(compact).items.map(item => [item.title, item.state, item.events.length]),
    detail.items.map(item => [item.title, item.state, item.events.length]));
  for (const [title, start, end] of [['扫描上传', '31.213', '31.222'], ['请求格口', '31.255', '31.264'], ['落格回传', '32.607', '32.622']]) {
    const mark = timing.marks.find(mark => mark.title === title);
    assert.equal(mark.startLabel, '开始');
    assert.equal(mark.start, parseTimingTime(time(start)));
    assert.equal(mark.end, parseTimingTime(time(end)));
    assert.equal(mark.warning, '');
  }
  const overview = timingOverview(timing);
  assert.equal(overview.scanToRequest, 146);
  assert.equal(timingInterval(overview.request.start, overview.request.end), 9);
  assert.equal(timingOverviewSpans(timing)[0].partial, false);
  assert.ok(overview.milestones.some(mark => mark.title === '请求格口'));
});

test('七位小数秒精确相减，显示遵守全站三位毫秒格式', () => {
  const start = parseTimingTime('2026-10-07T10:00:00.9999998');
  const end = parseTimingTime('2026-10-07T10:00:01.0000001');
  assert.equal(timingInterval(start, end), 0.0003);
  assert.equal(timingInterval(end, start), -0.0003);
  assert.equal(formatTimingTime(start), '2026-10-07 10:00:00.999');
  assert.equal(formatTimingMilliseconds(125.0625), '125.0625 ms');
});

test('时间解析拒绝UTC、偏移、缺省日期与无效日历日期', () => {
  for (const value of [null, '', '0001-01-01T00:00:00', '2026-02-30T10:00:00', '2026-10-07T24:00:00', `${scan}Z`, `${scan}+08:00`]) assert.equal(parseTimingTime(value), null, value);
  assert.notEqual(parseTimingTime('2024-02-29 10:00:00'), null);
});

test('64位编号不会转换成Number，超界、负数和非法链接被拒绝', () => {
  assert.equal(validTimingParcelId('9223372036854775807'), true);
  for (const value of ['9223372036854775808', '0', '-1', '1.1', '001', '', null]) assert.equal(validTimingParcelId(value), false);
});

test('每票在同一真实时间轴上，锚点索引和前后票数位置保持一致', () => {
  const items = Array.from({ length: 11 }, (_, index) => parcel(String(100 + index), { scannedTime: `2026-10-07T10:00:${String(index).padStart(2, '0')}.000` }));
  const rows = buildParcelTimingRows(context(items, '105'));
  assert.deepEqual(rows.map(row => row.relativeIndex), [-5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5]);
  const absolute = createTimingScale(rows, 'absolute');
  assert.ok(timingPosition(rows[0].marks[0].start, rows[0], absolute) < timingPosition(rows[10].marks[0].start, rows[10], absolute));
  const aligned = createTimingScale(rows, 'scan');
  assert.equal(timingPosition(rows[0].marks[0].start, rows[0], aligned), timingPosition(rows[10].marks[0].start, rows[10], aligned));
  const nodes = parcelTimingNodes(rows);
  assert.equal(timingInterval(nodes[0].ticks, nodes[10].ticks), 10000);
});

test('历史接口绘制真实请求窗口，未知响应不通过记录耗时补造', () => {
  const rows = buildParcelTimingRows(context([parcel(undefined, { apiRequests: [
    { apiType: 0, requestStatus: 1, requestTime: '2026-10-07T10:00:00.125', responseTime: '2026-10-07T10:00:00.150', elapsedMilliseconds: 999 },
    { apiType: 0, requestStatus: 0, requestTime: '2026-10-07T10:00:00.200', responseTime: null, elapsedMilliseconds: 999 },
  ] })]));
  const calls = rows[0].marks.filter(mark => mark.title === '请求格口');
  assert.equal(timingInterval(calls[0].start, calls[0].end), 25);
  assert.equal(calls[1].end, null);
  assert.equal(calls[1].warning, '未提供响应时间');
  assert.equal(parcelTimingNodes(rows).filter(node => node.mark === calls[1]).length, 1);
});

test('具备明确尝试身份的调用开始与结束构成一条请求窗口', () => {
  const rows = buildParcelTimingRows(context([parcel(undefined, { processingRecords: [
    record('start', '2026-10-07T10:00:00.100', 'started'), record('end', '2026-10-07T10:00:00.125', 'completed'),
  ] })]));
  const call = rows[0].marks.find(mark => mark.title === '请求格口');
  assert.equal(timingInterval(call.start, call.end), 25);
  assert.equal(call.startLabel, '开始');
  assert.equal(call.endLabel, '结束');
});

test('超时保留真实结束节点，迟到响应单独显示并保持失败状态', () => {
  const rows = buildParcelTimingRows(context([parcel(undefined, { processingRecords: [
    record('start', '2026-10-07T10:00:00.100', 'started'), record('timeout', '2026-10-07T10:00:00.150', 'unknown'),
    record('late', '2026-10-07T10:00:00.200', 'late-completion', { responseAt: '2026-10-07T10:00:00.200' }),
  ] })]));
  const call = rows[0].marks.find(mark => mark.title === '请求格口');
  assert.equal(timingInterval(call.start, call.end), 50);
  assert.equal(call.issue, true);
  assert.equal(call.state, '结果未知');
  assert.ok(rows[0].marks.some(mark => mark.title === '请求格口 · 迟到响应' && mark.end === null));
});

test('重试具有独立窗口，不能按同名请求合并', () => {
  const rows = buildParcelTimingRows(context([parcel(undefined, { processingRecords: [
    record('one', '2026-10-07T10:00:00.100', 'started'), record('one-end', '2026-10-07T10:00:00.150', 'failed'),
    record('two', '2026-10-07T10:00:00.200', 'started', { attemptNumber: 2, rawPayload: JSON.stringify({ kind: 'provider-attempt', operation: '请求格口', operationId: 'chute-1', attemptId: 'chute-1-2', attemptNumber: 2, outcome: 'started' }) }),
  ] })]));
  assert.equal(rows[0].marks.filter(mark => mark.title === '请求格口').length, 2);
});

test('负窗口和时间不可靠的事实明确标注，保留真实端点', () => {
  const rows = buildParcelTimingRows(context([parcel(undefined, { processingRecords: [record('negative', scan, 'completed', {
    requestAt: '2026-10-07T10:00:00.200', responseAt: '2026-10-07T10:00:00.100', hasReliableTimestamp: false,
  })] })]));
  const mark = rows[0].marks.find(mark => mark.title === '请求格口');
  assert.equal(timingInterval(mark.start, mark.end), -100);
  assert.match(mark.warning, /早于/);
  assert.match(mark.warning, /不可靠/);
});

test('空数据与同一时刻的动作仍有有限轴域，标签在独立轨道避让', () => {
  const empty = createTimingScale([], 'absolute');
  assert.ok(Number.isFinite(empty.min) && empty.max > empty.min);
  const rows = buildParcelTimingRows(context([parcel(undefined, { detectedTime: scan, dischargeTime: scan, completedTime: scan })]));
  const scale = createTimingScale(rows, 'absolute');
  const layout = layoutTimingMarks(rows[0], scale, 680);
  assert.equal(new Set(layout.map(mark => mark.lane)).size, 4);
  assert.ok(layout.every(mark => Number.isFinite(mark.startX)));
});
