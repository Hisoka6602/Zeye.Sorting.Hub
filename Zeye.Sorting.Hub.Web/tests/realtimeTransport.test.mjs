import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { stripTypeScriptTypes } from 'node:module';
import test from 'node:test';
import { ApiError } from '../src/data/api/apiResponse.ts';

const source = await readFile(new URL('../src/data/api/realtimeTransport.ts', import.meta.url), 'utf8');
let harnessSequence = 0;
const tick = () => new Promise(resolve => setImmediate(resolve));
const response = version => ({ statusCode: 200, json: JSON.stringify({ version }) });

/** 使用受控Promise和流回调执行原始传输实现，不建立网络连接或使用真实定时等待。 */
async function harness() {
  const globalKey = `__zeyeRealtimeTransportTest${++harnessSequence}`;
  const connections = [], unsubscribes = [];
  const createConnection = () => {
    const callbacks = {};
    const active = { state: 'Disconnected', observers: [], requests: [], starts: 0,
      async start() { active.starts++; active.state = 'Connected'; },
      async stop() { active.state = 'Disconnected'; callbacks.close?.(); },
      disconnect() { active.state = 'Disconnected'; callbacks.close?.(); },
      onreconnecting(callback) { callbacks.reconnecting = callback; },
      onreconnected(callback) { callbacks.reconnected = callback; },
      onclose(callback) { callbacks.close = callback; },
      stream() { return { subscribe(observer) { active.observers.push(observer); return { dispose() {} }; } }; },
      invoke(method, ...args) { return new Promise((resolve, reject) => { active.requests.push({ method, args, resolve, reject }); }); },
    };
    connections.push(active); globalThis[globalKey] = active; return active;
  };
  const first = createConnection();
  const client = `export const HttpTransportType={WebSockets:1,LongPolling:4};export const LogLevel={None:6};
    export function HubConnectionBuilder(){return{withUrl(){return this},withAutomaticReconnect(){return this},
    configureLogging(){return this},build(){return globalThis[${JSON.stringify(globalKey)}]}}}`;
  const clientUrl = 'data:text/javascript;base64,' + Buffer.from(client).toString('base64');
  const compiled = stripTypeScriptTypes(source)
    .replace("'./apiResponse.ts'", JSON.stringify(new URL('../src/data/api/apiResponse.ts', import.meta.url).href))
    .replace("'@microsoft/signalr'", JSON.stringify(clientUrl));
  const transport = await import('data:text/javascript;base64,' + Buffer.from(compiled).toString('base64'));
  return { transport, first, createConnection,
    watch(next, error = () => {}, refresh = false) {
      const off = transport.watchRealtimeResource('/api/parcels', next, error, refresh);
      unsubscribes.push(off); return off;
    },
    async cleanup() {
      unsubscribes.forEach(off => off()); transport.resetRealtimeConnection();
      for (const connection of connections) for (const pending of connection.requests) pending.reject(new Error('test cleanup'));
      await tick(); delete globalThis[globalKey];
    },
  };
}

test('更新的Watch快照使旧刷新Read失效，刷新中不把旧缓存冒充为刷新结果', async () => {
  const audit = await harness();
  try {
    const seen = [], refreshed = [];
    audit.watch(value => seen.push(JSON.parse(value.json).version)); await tick();
    audit.first.observers[0].next(response(1));
    audit.watch(value => refreshed.push(JSON.parse(value.json).version), undefined, true); await tick();
    assert.deepEqual(refreshed, []);
    audit.first.observers[0].next(response(3));
    audit.first.requests[0].resolve(response(2)); await tick();
    assert.deepEqual(seen, [1, 3]); assert.deepEqual(refreshed, [3]);
    assert.equal(audit.first.starts, 1); assert.equal(audit.first.observers.length, 1);
  } finally { await audit.cleanup(); }
});

test('同资源的较早刷新成功或401失败均不能覆盖后发刷新的结果', async () => {
  for (const staleError of [false, true]) {
    const audit = await harness();
    try {
      const seen = [], errors = [];
      audit.watch(value => seen.push(JSON.parse(value.json).version), error => errors.push(error)); await tick();
      audit.first.observers[0].next(response(1));
      audit.watch(() => {}, error => errors.push(error), true); await tick();
      audit.watch(() => {}, error => errors.push(error), true); await tick();
      audit.first.requests[1].resolve(response(3)); await tick();
      if (staleError) audit.first.requests[0].reject(new ApiError(401, 'old login'));
      else audit.first.requests[0].resolve(response(2));
      await tick(); assert.deepEqual(seen, [1, 3]); assert.deepEqual(errors, []);
    } finally { await audit.cleanup(); }
  }
});

test('会话重置后的新快照不接收旧会话Read成功、401或旧流终止回调', async () => {
  for (const staleError of [false, true]) {
    const audit = await harness();
    try {
      const seen = [], errors = [];
      audit.watch(value => seen.push(JSON.parse(value.json).version), error => errors.push(error)); await tick();
      audit.first.observers[0].next(response(1));
      audit.watch(() => {}, error => errors.push(error), true); await tick();
      const next = audit.createConnection(); audit.transport.resetRealtimeConnection();
      audit.watch(() => {}, error => errors.push(error)); await tick();
      next.observers[0].next(response(3));
      if (staleError) audit.first.requests[0].reject(new ApiError(401, 'old login'));
      else audit.first.requests[0].resolve(response(2));
      audit.first.observers[0].error(new ApiError(401, 'old stream'));
      audit.first.observers[0].complete(); await tick();
      audit.watch(() => {}, error => errors.push(error)); await tick();
      assert.deepEqual(seen, [1, 3]); assert.deepEqual(errors, []);
      assert.equal(next.observers.length, 1);
    } finally { await audit.cleanup(); }
  }
});

test('一次性读取在会话切换后取消旧成功和旧401，不向调用方暴露旧会话结果', async () => {
  for (const staleError of [false, true]) {
    const audit = await harness();
    try {
      const pending = audit.transport.readRealtimeResource('/api/parcels');
      const rejected = assert.rejects(pending, error => error.name === 'AbortError'); await tick();
      audit.createConnection(); audit.transport.resetRealtimeConnection();
      if (staleError) audit.first.requests[0].reject(new ApiError(401, 'old login'));
      else audit.first.requests[0].resolve(response(2));
      await rejected;
    } finally { await audit.cleanup(); }
  }
});

test('会话切换前尚未完成的握手401不能通知新登录的共享订阅', async () => {
  const audit = await harness();
  try {
    let rejectStart;
    audit.first.start = () => new Promise((resolve, reject) => { rejectStart = reject; });
    const seen = [], errors = [];
    audit.watch(value => seen.push(JSON.parse(value.json).version), error => errors.push(error)); await tick();
    const next = audit.createConnection(); audit.transport.resetRealtimeConnection();
    audit.watch(() => {}, error => errors.push(error)); await tick();
    next.observers[0].next(response(3)); rejectStart({ statusCode: 401 }); await tick();
    assert.deepEqual(seen, [3]); assert.deepEqual(errors, []);
  } finally { await audit.cleanup(); }
});

const command = { method: 'UpdateParcelStatus', arguments: ['123', '{"status":2}'] };

test('命令发送前会话切换则取消，不向旧连接或新连接派发命令', async () => {
  for (const pendingHandshake of [false, true]) {
    const audit = await harness();
    try {
      let rejectStart;
      if (pendingHandshake) audit.first.start = () => new Promise((_resolve, reject) => { rejectStart = reject; });
      else { audit.watch(() => {}); await tick(); }
      const pending = audit.transport.submitRealtimeCommand(command);
      const rejected = assert.rejects(pending, error => error.name === 'AbortError');
      if (pendingHandshake) await tick();
      const next = audit.createConnection(); audit.transport.resetRealtimeConnection();
      if (pendingHandshake) rejectStart({ statusCode: 401 });
      await rejected;
      assert.equal(audit.first.requests.length, 0); assert.equal(next.requests.length, 0);
    } finally { await audit.cleanup(); }
  }
});

test('已发送的命令跨会话成功或401均提示结果未确认，且不自动重放', async () => {
  for (const statusCode of [200, 401]) {
    const audit = await harness();
    try {
      const pending = audit.transport.submitRealtimeCommand(command);
      const rejected = assert.rejects(pending, error => error.name !== 'AbortError' && /结果尚未确认/.test(error.message));
      await tick(); assert.equal(audit.first.requests[0].method, command.method);
      assert.deepEqual(audit.first.requests[0].args, command.arguments);
      const next = audit.createConnection(); audit.transport.resetRealtimeConnection();
      audit.watch(() => {}); await tick();
      audit.first.requests[0].resolve({ statusCode, json: '{}' }); await rejected;
      assert.equal(audit.first.requests.length, 1); assert.equal(next.requests.length, 0);
    } finally { await audit.cleanup(); }
  }
});

test('已发送的命令在连接替换后不能发布旧结果，取消已发送命令也保留结果未确认语义', async () => {
  for (const abort of [false, true]) {
    const audit = await harness();
    try {
      const controller = new AbortController();
      const pending = audit.transport.submitRealtimeCommand(command, controller.signal);
      const rejected = assert.rejects(pending, error => /结果尚未确认/.test(error.message)); await tick();
      const next = audit.createConnection();
      if (abort) controller.abort();
      else { audit.first.disconnect(); audit.watch(() => {}); await tick(); }
      audit.first.requests[0].resolve(response(1)); await rejected;
      assert.equal(audit.first.requests.length, 1); assert.equal(next.requests.length, 0);
    } finally { await audit.cleanup(); }
  }
});

test('同会话命令正常返回原始结果并只发送一次', async () => {
  const audit = await harness();
  try {
    const pending = audit.transport.submitRealtimeCommand(command); await tick();
    const expected = response(1); audit.first.requests[0].resolve(expected);
    assert.deepEqual(await pending, expected); assert.equal(audit.first.requests.length, 1);
  } finally { await audit.cleanup(); }
});
