import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { stripTypeScriptTypes } from 'node:module';
import test from 'node:test';
import { ApiError } from '../src/data/api/apiResponse.ts';

const source = await readFile(new URL('../src/data/api/useApiResource.ts', import.meta.url), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
const response = (statusCode, data) => ({ statusCode, json: JSON.stringify(data) });
let sequence = 0;

/** 仿真React的状态、依赖和effect清理，执行真实Hook；只替换网络及模块加载边界。 */
async function harness(options = {}) {
  const key = `__zeyeApiResourceTest${++sequence}`;
  const slots = [], requests = [], watches = [], events = [], authChanges = [];
  let cursor = 0, dirty = false, effects = [], result;
  const same = (left, right) => left?.length === right.length && right.every((item, index) => Object.is(item, left[index]));
  const state = { authenticated: true, importError: undefined, watchError: undefined,
    loader(path, signal) { return new Promise((resolve, reject) => requests.push({ path, signal, resolve, reject })); },
    watch(path, next, error, refresh) {
      if (state.watchError) throw state.watchError;
      const watch = { path, next, error, refresh, disposed: false }; watches.push(watch);
      return () => { watch.disposed = true; };
    },
    async loadTransport() { if (state.importError) throw state.importError; return { watchRealtimeResource: state.watch }; },
    useState(initial) {
      const index = cursor++;
      if (!(index in slots)) slots[index] = typeof initial === 'function' ? initial() : initial;
      return [slots[index], next => {
        const value = typeof next === 'function' ? next(slots[index]) : next;
        if (!Object.is(value, slots[index])) { slots[index] = value; dirty = true; }
      }];
    },
    useRef(initial) { const index = cursor++; return slots[index] ??= { current: initial }; },
    useCallback(value, deps) {
      const index = cursor++;
      if (!slots[index] || !same(slots[index].deps, deps)) slots[index] = { value, deps };
      return slots[index].value;
    },
    useEffect(effect, deps) {
      const index = cursor++, previous = slots[index];
      if (previous && same(previous.deps, deps)) return;
      const current = slots[index] = { deps };
      effects.push(() => { previous?.cleanup?.(); current.cleanup = effect(); });
    },
    useSyncExternalStore(_subscribe, getSnapshot) { cursor++; return getSnapshot(); },
  };
  globalThis[key] = state;
  const moduleUrl = text => 'data:text/javascript;base64,' + Buffer.from(text).toString('base64');
  const react = moduleUrl(['useState', 'useRef', 'useEffect', 'useCallback', 'useSyncExternalStore']
    .map(name => `export const ${name}=(...args)=>globalThis[${JSON.stringify(key)}].${name}(...args);`).join(''));
  const client = moduleUrl(`export const requestApi=(...args)=>globalThis[${JSON.stringify(key)}].loader(...args);`);
  const policy = moduleUrl(`const state=globalThis[${JSON.stringify(key)}];
    export const getRealtimeAuthenticated=()=>state.authenticated, subscribeRealtimeAuthenticated=()=>()=>{},
    usesRealtimeTransport=()=>state.authenticated, isRealtimeResource=()=>true,
    setRealtimeAuthenticated=value=>{state.authenticated=value;state.authChanges.push(value)};`);
  state.authChanges = authChanges;
  const compiled = stripTypeScriptTypes(source)
    .replace("'react'", JSON.stringify(react)).replace("'./client'", JSON.stringify(client))
    .replace("'./apiResponse'", JSON.stringify(new URL('../src/data/api/apiResponse.ts', import.meta.url).href))
    .replace("'./realtimePolicy'", JSON.stringify(policy))
    .replace("import('./realtimeTransport')", `globalThis[${JSON.stringify(key)}].loadTransport()`);
  const { useApiResource } = await import(moduleUrl(compiled));
  const previousWindow = globalThis.window;
  globalThis.window = { dispatchEvent(event) { events.push(event.type); return true; } };
  let settings = { path: '/api/parcels?a', realtime: true, retainOnError: false, ...options };
  const view = (next = {}) => {
    settings = { ...settings, ...next };
    for (let count = 0; count < 10; count++) {
      cursor = 0; effects = []; dirty = false;
      result = useApiResource(settings.path, state.loader, settings.realtime, settings.retainOnError);
      effects.forEach(effect => effect());
      if (!dirty) return result;
    }
    throw new Error('Hook未稳定');
  };
  view();
  return { runtime: state, watches, requests, events, authChanges, view,
    get state() { return slots[0]; },
    async cleanup() {
      slots.forEach(slot => slot?.cleanup?.());
      requests.forEach(request => request.reject(new Error('test cleanup')));
      await tick(); delete globalThis[key]; globalThis.window = previousWindow;
    },
  };
}

test('HTTP失败按retainOnError与同资源判断保留数据，权限错误总是清理', async () => {
  for (const retainOnError of [false, true]) for (const status of [500, 401, 403]) {
    const audit = await harness({ realtime: false, retainOnError });
    try {
      audit.requests[0].resolve({ version: 1 }); await tick();
      audit.view().refresh(); audit.view();
      audit.requests[1].reject(new ApiError(status, 'failed')); await tick();
      assert.deepEqual(audit.state.data, retainOnError && status === 500 ? { version: 1 } : undefined);
      assert.equal(audit.state.loading, false); assert.equal(audit.state.error.status, status);
      audit.view({ path: '/api/parcels?b' });
      audit.requests[2].reject(new Error('other resource')); await tick();
      assert.equal(audit.state.data, undefined);
    } finally { await audit.cleanup(); }
  }
});

test('实时响应、流错误、动态加载及握手失败均使用相同的数据清理和权限规则', async () => {
  for (const failure of ['response', 'stream', 'import', 'subscribe']) {
    for (const retainOnError of [false, true]) for (const status of [500, 401, 403]) {
      const audit = await harness({ retainOnError });
      try {
        await tick(); audit.watches[0].next(response(200, { version: 1 }));
        if (failure === 'import') audit.runtime.importError = new ApiError(status, 'module failed');
        if (failure === 'subscribe') audit.runtime.watchError = new ApiError(status, 'handshake failed');
        audit.view().refresh(); audit.view(); await tick();
        if (failure === 'response') audit.watches[1].next(response(status, { detail: 'response failed' }));
        if (failure === 'stream') audit.watches[1].error(new ApiError(status, 'stream failed'));
        await tick();
        assert.deepEqual(audit.state.data, retainOnError && status === 500 ? { version: 1 } : undefined, `${failure}/${retainOnError}/${status}`);
        assert.equal(audit.state.loading, false); assert.equal(audit.state.error.status, status);
        assert.deepEqual(audit.events, status === 500 ? [] : ['zeye-session-changed']);
        assert.deepEqual(audit.authChanges, status === 401 ? [false] : []);
      } finally { await audit.cleanup(); }
    }
  }
});

test('普通失败归一成Error，换资源后即使retainOnError也不保留旧数据', async () => {
  const audit = await harness({ retainOnError: true });
  try {
    await tick(); audit.watches[0].next(response(200, { version: 1 }));
    audit.view({ path: '/api/parcels?b' }); await tick();
    audit.watches[1].error('network failed');
    assert.equal(audit.state.data, undefined);
    assert.ok(audit.state.error instanceof Error); assert.equal(audit.state.error.message, 'network failed');
  } finally { await audit.cleanup(); }
});

test('清理后的旧响应、旧401/403及旧模块加载错误不改变新资源或会话', async () => {
  const audit = await harness({ retainOnError: true });
  try {
    await tick(); const old = audit.watches[0]; old.next(response(200, { version: 1 }));
    let rejectImport;
    audit.runtime.loadTransport = () => new Promise((_resolve, reject) => { rejectImport = reject; });
    audit.view().refresh(); audit.view();
    audit.runtime.loadTransport = async () => ({ watchRealtimeResource: audit.runtime.watch });
    audit.view({ path: '/api/parcels?b' }); await tick();
    audit.watches[1].next(response(200, { version: 2 }));
    old.next(response(401, { detail: 'old session' })); old.error(new ApiError(403, 'old permission'));
    rejectImport(new ApiError(401, 'old module')); await tick();
    assert.deepEqual(audit.state, { data: { version: 2 }, loading: false });
    assert.deepEqual(audit.events, []); assert.deepEqual(audit.authChanges, []);
    assert.equal(old.disposed, true);
  } finally { await audit.cleanup(); }
});

test('手动刷新只作用于当前同资源，后续依赖、筛选及会话变化不强制额外Read', async () => {
  const audit = await harness();
  try {
    await tick(); audit.watches[0].next(response(200, { version: 1 }));
    audit.view().refresh(); audit.view(); await tick();
    audit.view({ retainOnError: true }); await tick();
    audit.view({ path: '/api/parcels?b' }); await tick();
    audit.runtime.authenticated = false; audit.view();
    audit.runtime.authenticated = true; audit.view(); await tick();
    assert.deepEqual(audit.watches.map(item => item.refresh), [false, true, false, false, false]);
    assert.equal(audit.watches.length, 5);
  } finally { await audit.cleanup(); }
});
