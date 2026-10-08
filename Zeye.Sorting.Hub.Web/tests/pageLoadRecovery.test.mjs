import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8');
const recoveryScript = html.match(/<script id="zeye-page-load-recovery">([\s\S]*?)<\/script>/)?.[1];
assert.ok(recoveryScript, '入口脚本执行前必须安装恢复处理');

/** 在不加载 React 的情况下执行同一入口脚本，模拟失败、存储限制和不同部署版本。 */
function browser({ entrySource = '/assets/index-first.js', storage = new Map(), storageBlocked = false, mounted = false, parsing = false } = {}) {
  const listeners = new Map();
  const log = [];
  const feedback = { role: 'status', /** 记录无障碍角色变化。 */ setAttribute(name, value) { this[name] = value; } };
  const description = { textContent: '页面加载中…' };
  const retry = { hidden: true, /** 保存重试按钮监听，允许模拟真实点击。 */ addEventListener(name, handler) { this[name] = handler; } };
  const nodes = { 'startup-feedback': feedback, 'startup-description': description, 'startup-retry': retry };
  let installed = false;
  let parsed = !parsing;
  let reloads = 0;
  const location = { pathname: '/parcels/batch', /** 记录刷新次数以识别循环。 */ reload() { reloads++; } };
  const window = { location, /** 注册资源失败事件。 */ addEventListener(name, handler) { listeners.set(name, handler); } };
  const document = {
    readyState: parsing ? 'loading' : 'complete',
    /** 模拟生产入口脚本的版本标识。 */
    querySelector() { return { /** 返回当前版本地址。 */ getAttribute() { return entrySource; } }; },
    /** 静态元素在解析完成前或 React 挂载后均不可读取。 */
    getElementById(id) { return !parsed || mounted && installed ? null : nodes[id]; },
    /** 保存解析完成监听。 */
    addEventListener(name, handler) { listeners.set(name, handler); },
  };
  const sessionStorage = {
    /** 读取跨重新载入保留的恢复标记。 */
    getItem(key) { if (storageBlocked) throw new Error('storage blocked'); return storage.get(key); },
    /** 保存标记或模拟浏览器存储限制。 */
    setItem(key, value) { if (storageBlocked) throw new Error('storage blocked'); storage.set(key, value); },
    /** 仅移除显式重试指定的版本标记。 */
    removeItem(key) { if (storageBlocked) throw new Error('storage blocked'); storage.delete(key); },
  };
  const console = { /** 记录原始失败诊断。 */ error(...values) { log.push(values); }, /** 记录恢复限制诊断。 */ warn(...values) { log.push(values); } };
  runInNewContext(recoveryScript, { document, window, sessionStorage, location, console });
  installed = true;
  return { recovery: window.zeyePageLoadRecovery, listeners, storage, feedback, description, retry, log, reloads: () => reloads,
    /** 模拟 HTML 完成解析，执行延迟的静态反馈初始化。 */
    finishParsing() { parsed = true; listeners.get('DOMContentLoaded')?.(); },
  };
}

/** 模拟 Vite 的可取消预加载失败事件。 */
function preloadFailure(instance, message = 'Failed to fetch dynamically imported module: /assets/page-old.js') {
  const event = { payload: new Error(message), prevented: false, /** 保留 Vite 是否应继续抛出模块错误。 */ preventDefault() { this.prevented = true; } };
  instance.listeners.get('vite:preloadError')(event);
  return event;
}

test('旧页面分块失败自动恢复一次；持续失败不能无限刷新，原错误可进入错误边界', () => {
  const instance = browser();
  assert.equal(preloadFailure(instance).prevented, true);
  assert.equal(instance.reloads(), 1);
  assert.equal(preloadFailure(instance).prevented, false);
  assert.equal(instance.reloads(), 1);
  assert.equal(instance.feedback.role, 'alert');
  assert.equal(instance.retry.hidden, false);
  assert.match(instance.description.textContent, /资源暂时无法加载/);
});

test('入口模块本身失败仍能恢复；重复失败提供不依赖 React 的重试按钮', () => {
  const instance = browser();
  const event = { target: { tagName: 'SCRIPT', type: 'module', src: '/assets/index-old.js' } };
  instance.listeners.get('error')(event);
  instance.listeners.get('error')(event);
  assert.equal(instance.reloads(), 1);
  assert.equal(instance.retry.hidden, false);
  assert.equal(instance.feedback.role, 'alert');
});

test('入口在 HTML 解析期间失败时也能捕获，解析完成后显示静态重试反馈', () => {
  const instance = browser({ parsing: true, storage: new Map([['zeye-page-load-recovery:/assets/index-first.js', 'attempted']]) });
  instance.listeners.get('error')({ target: { tagName: 'SCRIPT', type: 'module', src: '/assets/index-first.js' } });
  assert.equal(instance.reloads(), 0);
  assert.equal(instance.retry.hidden, true);
  instance.finishParsing();
  assert.equal(instance.feedback.role, 'alert');
  assert.equal(instance.retry.hidden, false);
  instance.retry.click();
  assert.equal(instance.reloads(), 1);
});

test('业务异常和图片、普通脚本故障不会导致整页自动刷新', () => {
  const instance = browser();
  for (const error of [new Error('接口返回 401'), new Error('业务处理失败'), '普通组件异常', null])
    assert.equal(instance.recovery.recover(error), false);
  for (const target of [{ tagName: 'IMG' }, { tagName: 'SCRIPT', type: 'text/javascript' }, null])
    instance.listeners.get('error')({ target });
  assert.equal(instance.reloads(), 0);
  assert.equal(instance.log.length, 0);
});

test('入口执行异常和启动时未处理的 Promise 不会自动重放业务，静态反馈可以重试', () => {
  for (const [name, field] of [['error', 'error'], ['unhandledrejection', 'reason']]) {
    const instance = browser();
    instance.listeners.get(name)({ [field]: new TypeError('startup failure') });
    assert.equal(instance.reloads(), 0);
    assert.equal(instance.feedback.role, 'alert');
    assert.equal(instance.retry.hidden, false);
    assert.match(instance.description.textContent, /启动时发生异常/);
    assert.equal(instance.log.length, 1);
  }
});

test('已挂载页面的普通异步异常不会改写 DOM 或触发刷新', () => {
  const instance = browser({ mounted: true });
  instance.listeners.get('unhandledrejection')({ reason: new Error('business failure') });
  instance.listeners.get('error')({ error: new Error('render failure') });
  assert.equal(instance.reloads(), 0);
  assert.equal(instance.retry.hidden, true);
  assert.equal(instance.log.length, 0);
});

test('不同浏览器的模块和 CSS 分块错误均可识别，普通 TypeError 不属于资源故障', () => {
  const instance = browser();
  for (const message of ['ChunkLoadError: Loading chunk 5 failed', 'Importing a module script failed.', 'error loading dynamically imported module', 'Failed to load module script', 'Unable to preload CSS for /assets/page.css', 'Loading CSS chunk 3 failed'])
    assert.equal(instance.recovery.isAssetError(new Error(message)), true, message);
  assert.equal(instance.recovery.isAssetError(new TypeError('Cannot read properties of undefined')), false);
});

test('存储被浏览器禁用时不进入无保护刷新循环，仍提供可操作的失败提示', () => {
  const instance = browser({ storageBlocked: true });
  assert.equal(preloadFailure(instance).prevented, false);
  assert.equal(instance.reloads(), 0);
  assert.equal(instance.retry.hidden, false);
  assert.ok(instance.log.length >= 2);
});

test('恢复上限跨重新载入保留，新入口版本可再次恢复', () => {
  const storage = new Map();
  preloadFailure(browser({ storage }));
  const sameBuild = browser({ storage });
  assert.equal(preloadFailure(sameBuild).prevented, false);
  assert.equal(sameBuild.reloads(), 0);
  const newBuild = browser({ storage, entrySource: '/assets/index-next.js' });
  assert.equal(preloadFailure(newBuild).prevented, true);
  assert.equal(newBuild.reloads(), 1);
});

test('显式重试重置当前版本的次数并重新加载，其他版本的恢复记录不变', () => {
  const instance = browser();
  preloadFailure(instance);
  instance.storage.set('zeye-page-load-recovery:/assets/previous.js', 'attempted');
  instance.retry.click();
  assert.equal(instance.reloads(), 2);
  assert.equal(instance.storage.has('zeye-page-load-recovery:/assets/index-first.js'), false);
  assert.equal(instance.storage.has('zeye-page-load-recovery:/assets/previous.js'), true);
});

test('React 已挂载后不覆盖现有页面 DOM，持续失败由页面错误边界处理', () => {
  const instance = browser({ mounted: true });
  preloadFailure(instance);
  assert.equal(preloadFailure(instance).prevented, false);
  assert.equal(instance.feedback.role, 'status');
  assert.equal(instance.retry.hidden, true);
  assert.equal(instance.reloads(), 1);
});

test('仅在模块通道确认失败时处理无法识别的导入异常', () => {
  const instance = browser();
  assert.equal(instance.recovery.recover(new SyntaxError('Unexpected token')), false);
  assert.equal(preloadFailure(instance, 'Unexpected token').prevented, true);
  assert.equal(instance.reloads(), 1);
});
