import assert from 'node:assert/strict';
import test from 'node:test';
import { inspectTimeDisplay, assertTimeDisplay } from '../scripts/time-display-guard.mjs';

/** 测试通过真实导入确认共享格式化函数，覆盖别名调用。 */
const shared = "import { localTime, localDateTimeFormat } from '../data/api/operationalTypes';\n";

test('原始时间、可选链、模板字符串、描述项及悬停提示均被拦截', () => {
  for (const source of ['const page = <time>{record.occurredAt}</time>;', 'const page = <span>{record?.createdTime}</span>;', 'const page = <span title={record.recordedAt}>详情</span>;', 'const items = [{ children: record.startedAt }];', 'const page = <span>{`记录 ${record.generatedAt}`}</span>;', 'const page = <>{record.lastLogin}</>;', 'const page = <Input value={record.createdTime} />;']) {
    const issues = inspectTimeDisplay(shared + source);
    assert.ok(issues.length > 0, source);
    assert.equal(issues[0].line, 2);
    assert.ok(issues[0].column > 0);
  }
});

test('变量别名、解构别名、机械替换和类型断言不能绕过显示守卫', () => {
  for (const source of ['const stamp = record.occurredAt; const page = <span>{stamp}</span>;', 'const { occurredAt: stamp } = record; const page = <span>{stamp}</span>;', "const page = <span>{record.occurredAt.replace('T', ' ')}</span>;", 'const page = <span>{record.occurredAt as string}</span>;', 'const page = <span>{new Date().toISOString()}</span>;', 'const page = <span>{new Date().toLocaleString()}</span>;']) assert.ok(inspectTimeDisplay(shared + source).length > 0, source);
});

test('时间列遗漏 render 或直接返回列值时编译失败，包含 JSX 及提示属性', () => {
  for (const source of ["const columns = [{ dataIndex: 'createdTime' }];", "const columns = [{ dataIndex: ['record', 'occurredAt'] }];", "const columns = [{ dataIndex: 'recordedAt', render: value => value }];", "const columns = [{ dataIndex: 'scannedTime', render: value => <span title={value}>{localTime(value)}</span> }];", "const columns = [{ dataIndex: 'startedAt', render: value => <span>{value}</span> }];"]) assert.ok(inspectTimeDisplay(shared + source).length > 0, source);
});

test('控件、行内格式化及可见字面量的超长毫秒格式被拦截', () => {
  for (const source of ['const page = <DatePicker showTime />;', 'const page = <TimePicker format="HH:mm:ss.SSSSSSS" />;', "const page = <TimePicker format={'HH:mm:ss.SSSSSSS'} />;", "const page = <span>{dayjs().format('YYYY-MM-DD HH:mm:ss.SSSSSSS')}</span>;", 'const page = <span>2026-10-06 10:24:31.0612630</span>;', 'const page = <span title="2026-10-06T10:24:31.0612630">详情</span>;']) assert.ok(inspectTimeDisplay(shared + source).length > 0, source);
});

test('静态格式别名和共享常量修改在编译时受到同一约束', () => {
  for (const source of ["const format = 'YYYY-MM-DD HH:mm:ss.SSSSSSS'; const page = <TimePicker format={format} />;", "const format = 'YYYY-MM-DD HH:mm:ss.SSSSSSS'; const page = <time>{dayjs().format(format)}</time>;", "export const localDateTimeFormat = 'YYYY-MM-DD HH:mm:ss';"]) assert.ok(inspectTimeDisplay(source).length > 0, source);
  assert.deepEqual(inspectTimeDisplay(shared + 'const page = <DatePicker showTime format={\'YYYY-MM-DD HH:mm:ss.SSS\'} />;'), []);
});

test('共享函数、字段展示、统一控件格式和原始机器时间属性通过', () => {
  const source = shared + "import { parcelFactValue } from '../features/parcels/ParcelFacts'; const display = (key, value) => missing ? '—' : parcelFactValue(key, value); const page = <time dateTime={record.occurredAt} title={localTime(record.occurredAt)}>{display('occurredAt', record.occurredAt)}</time>; const columns = [{ dataIndex: 'startedAt', render: value => <span>{localTime(value)}</span> }]; const picker = <DatePicker.RangePicker showTime format={localDateTimeFormat} />;";
  assert.deepEqual(inspectTimeDisplay(source), []);
  assert.deepEqual(inspectTimeDisplay("import { localTime as showTime } from '../data/api/operationalTypes'; const columns = [{ dataIndex: 'recordedAt', render: showTime }]; const page = <time>{showTime(record.occurredAt)}</time>;"), []);
  assert.deepEqual(inspectTimeDisplay("import { localTime } from '../data/api/operationalTypes.ts'; const page = <time>{localTime(record.occurredAt)}</time>;"), []);
});

test('守卫不改变接口精度，不误报排序、纯日期、耗时、报文及注释', () => {
  const source = shared + "// 示例报文 2026-10-06T10:24:31.0612630\nconst payload = { occurredAt: '2026-10-06T10:24:31.0612630' }; const columns = [{dataIndex:'occurredAt', render: localTime, sorter: (a,b) => a.occurredAt.localeCompare(b.occurredAt)}]; const page = <><span>{row.date}</span><span>{row.durationMs} ms</span><pre>{payload.occurredAt}</pre><span>{dayjs().format('YYYY-MM-DD')}</span></>;";
  assert.deepEqual(inspectTimeDisplay(source), []);
  assert.deepEqual(inspectTimeDisplay('const page = <><time dateTime="2026-10-06T10:24:31.0612630">2026-10-06 10:24:31.061</time><pre>2026-10-06T10:24:31.0612630<code><span>{record.occurredAt}</span></code></pre></>;'), []);
});

test('当前全部前端源码符合时间显示规范', () => { assert.doesNotThrow(() => assertTimeDisplay()); });
