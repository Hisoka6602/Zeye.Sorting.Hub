import test from 'node:test';
import assert from 'node:assert/strict';
import { configurationDraft, configurationFields, configurationKeyMatches, configurationPatch, configurationRetentionPolicyRows, configurationStringList, configurationTemporalKind, configurationValueAt, isValidConfigurationTemporalValue, isHotConfigurationField } from '../src/features/access/configurationModel.ts';

test('配置编辑保留类型、未知旧字段和数组整体替换；独立入口不会重复修改', () => {
  const saved = { WebRequestAuditLog: { Enabled: true, ExcludedPathPrefixes: ['/first', '/second'] }, ResourceThresholds: { SampleIntervalSeconds: 60 }, LegacyExtra: { Flag: false }, FusionIngestion: { Sources: [] }, Kestrel: { Port: 5078 }, ConfigurationStorage: { LiteDbPath: 'test.db' } };
  assert.equal(configurationFields(saved).length, 4);
  const draft = configurationDraft(saved);
  assert.deepEqual(configurationPatch(saved, draft).changedKeys, []);
  draft['WebRequestAuditLog:ExcludedPathPrefixes'] = '["/new"]';
  draft['LegacyExtra:Flag'] = true;
  const patch = JSON.parse(JSON.stringify(configurationPatch(saved, draft).changes));
  assert.deepEqual(patch, { WebRequestAuditLog: { ExcludedPathPrefixes: ['/new'] }, LegacyExtra: { Flag: true } });
  assert.deepEqual(saved.WebRequestAuditLog.ExcludedPathPrefixes, ['/first', '/second']);
});
test('管理员凭据使用原值，未修改不提交，可修改或清空；空数字和非法 JSON 阻止保存', () => {
  const saved = { ConnectionStrings: { MySql: 'Server=localhost;Database=qa;User=qa;Password=qa-original;' }, ResourceThresholds: { SampleIntervalSeconds: 60 }, WebRequestAuditLog: { ExcludedPathPrefixes: [] } };
  const draft = configurationDraft(saved);
  assert.equal(draft['ConnectionStrings:MySql'], saved.ConnectionStrings.MySql);
  assert.deepEqual(configurationPatch(saved, draft).changedKeys, []);
  draft['ConnectionStrings:MySql'] = '********';
  assert.equal(configurationPatch(saved, draft).changes.ConnectionStrings.MySql, '********');
  draft['ConnectionStrings:MySql'] = '';
  assert.equal(configurationPatch(saved, draft).changes.ConnectionStrings.MySql, '');
  draft['ResourceThresholds:SampleIntervalSeconds'] = null;
  assert.throws(() => configurationPatch(saved, draft), /有效数字/);
  draft['ResourceThresholds:SampleIntervalSeconds'] = 60;
  draft['WebRequestAuditLog:ExcludedPathPrefixes'] = '{"value":1}';
  assert.throws(() => configurationPatch(saved, draft), /JSON 数组/);
  draft['WebRequestAuditLog:ExcludedPathPrefixes'] = '[';
  assert.throws(() => configurationPatch(saved, draft), /格式无效/);
});
test('字段能力由后端返回，数组、大小写和环境覆盖路径按段匹配', () => {
  const fields = configurationFields({ WebRequestAuditLog: { ExcludedPathPrefixes: ['/health'], BackgroundQueueCapacity: 4096 } });
  const snapshot = { hotReloadKeys: ['webrequestauditlog:excludedpathprefixes:0'] };
  assert.equal(isHotConfigurationField(fields[0], snapshot), true);
  assert.equal(isHotConfigurationField(fields[1], snapshot), false);
  assert.equal(configurationKeyMatches('Access:EnforceAuthorization', 'Access'), true);
  assert.equal(configurationKeyMatches('Accessibility:Flag', 'Access'), false);
  assert.equal(configurationValueAt({ Root: { Fields: ['unchanged', 'changed'] } }, 'root:fields:1'), 'changed');
});

test('保留策略编辑保持数组整体替换、属性大小写、顺序与历史扩展字段', () => {
  const saved = { Persistence: { Retention: { Policies: [{ name: 'InboxMessage', retentionDays: 30, LegacyTag: 'keep' }, { Name: 'ArchiveTask', RetentionDays: 90 }] } } };
  const draft = configurationDraft(saved);
  const rows = configurationRetentionPolicyRows(draft['Persistence:Retention:Policies']);
  assert.equal(rows[0].nameKey, 'name');
  assert.equal(rows[0].daysKey, 'retentionDays');
  rows[0].value = { ...rows[0].value, [rows[0].nameKey]: 'SlowQueryProfile', [rows[0].daysKey]: 7 };
  draft['Persistence:Retention:Policies'] = JSON.stringify(rows.map(row => row.value));
  const patch = configurationPatch(saved, draft);
  assert.deepEqual(patch.changedKeys, ['Persistence:Retention:Policies']);
  assert.deepEqual(patch.changes.Persistence.Retention.Policies, [{ name: 'SlowQueryProfile', retentionDays: 7, LegacyTag: 'keep' }, { Name: 'ArchiveTask', RetentionDays: 90 }]);
  assert.equal(saved.Persistence.Retention.Policies[0].name, 'InboxMessage');
  draft['Persistence:Retention:Policies'] = '[]';
  assert.deepEqual(configurationPatch(saved, draft).changes.Persistence.Retention.Policies, []);
});

test('保留策略的空天数、越界、非整数和非法名称阻止提交；未修改历史配置保持兼容', () => {
  const saved = { Persistence: { Retention: { Policies: [{ Name: 'OldPolicy', RetentionDays: 30, Extension: true }] } }, Access: { EnforceAuthorization: false } };
  const draft = configurationDraft(saved);
  draft['Access:EnforceAuthorization'] = true;
  assert.deepEqual(configurationPatch(saved, draft).changedKeys, ['Access:EnforceAuthorization']);
  for (const days of [null, 0, -1, 3651, 1.5, '30']) {
    draft['Persistence:Retention:Policies'] = JSON.stringify([{ Name: 'InboxMessage', RetentionDays: days }]);
    assert.throws(() => configurationPatch(saved, draft), /1～3650/);
  }
  draft['Persistence:Retention:Policies'] = JSON.stringify([{ Name: 'Unsupported', RetentionDays: 30 }]);
  assert.throws(() => configurationPatch(saved, draft), /保留对象/);
  assert.equal(configurationRetentionPolicyRows('[{"LegacyName":"old"}]'), undefined);
  assert.equal(configurationRetentionPolicyRows('['), undefined);
});

test('本地时间选择支持分钟旧格式、秒及闰年；无效日历和时区偏移不会自动修复', () => {
  assert.equal(configurationTemporalKind('Persistence:Sharding:ParcelStartTime'), 'datetime');
  assert.equal(configurationTemporalKind('persistence:autotuning:dailyreportlocaltime'), 'time');
  assert.equal(configurationTemporalKind('Legacy:StartTime'), undefined);
  for (const value of ['00:00:00', '23:59:59', ' 01:02 ']) assert.equal(isValidConfigurationTemporalValue('time', value), true, value);
  for (const value of ['24:00:00', '12:60:00', '12:30:60', '12:30:00.500', '12:30:00+08:00', '']) assert.equal(isValidConfigurationTemporalValue('time', value), false, value);
  for (const value of ['2024-02-29T12:30:00', '2000-02-29 01:02', '2026-10-07T23:59:59.1234567', '0001-01-01T00:00:00']) assert.equal(isValidConfigurationTemporalValue('datetime', value), true, value);
  for (const value of ['2025-02-29T12:30:00', '1900-02-29T12:30:00', '2026-04-31T12:30:00', '2026-00-01T12:30:00', '2026-10-07T24:00:00', '0000-01-01T00:00:00', '2026-10-07T12:30:00+08:00']) assert.equal(isValidConfigurationTemporalValue('datetime', value), false, value);
});

test('未修改的旧时间和自定义小数保留原值；选择后的时间按本地字符串提交', () => {
  const saved = { Persistence: { AutoTuning: { DailyReportLocalTime: 'old-value' }, Sharding: { ParcelStartTime: '2026-01-01T00:00:00.1234567' } }, WebRequestAuditLog: { SampleRate: 0.005 }, Access: { EnforceAuthorization: false } };
  const draft = configurationDraft(saved);
  assert.deepEqual(configurationPatch(saved, draft).changedKeys, []);
  draft['Access:EnforceAuthorization'] = true;
  assert.deepEqual(configurationPatch(saved, draft).changedKeys, ['Access:EnforceAuthorization']);
  draft['Persistence:AutoTuning:DailyReportLocalTime'] = '12:15:30';
  assert.equal(configurationPatch(saved, draft).changes.Persistence.AutoTuning.DailyReportLocalTime, '12:15:30');
  assert.equal(draft['WebRequestAuditLog:SampleRate'], 0.005);
  draft['Persistence:AutoTuning:DailyReportLocalTime'] = '25:00:00';
  assert.throws(() => configurationPatch(saved, draft), /有效的本地时间/);
});

test('逐项列表保留自定义内容、重复项与顺序；空列表有效，空项阻止保存', () => {
  const saved = { Persistence: { Sharding: { HashSharding: { ExpansionPlan: { Stages: ['16→32', 'warmup', 'warmup'] } } } } };
  const draft = configurationDraft(saved);
  assert.deepEqual(configurationStringList(draft['Persistence:Sharding:HashSharding:ExpansionPlan:Stages']), ['16→32', 'warmup', 'warmup']);
  assert.equal(configurationStringList('[{"Legacy":"keep"}]'), undefined);
  assert.equal(configurationStringList('['), undefined);
  const key = 'Persistence:Sharding:HashSharding:ExpansionPlan:Stages';
  draft[key] = JSON.stringify(['warmup', '16→32', 'warmup', '自定义阶段']);
  assert.deepEqual(configurationPatch(saved, draft).changes.Persistence.Sharding.HashSharding.ExpansionPlan.Stages, ['warmup', '16→32', 'warmup', '自定义阶段']);
  draft[key] = '[]';
  assert.deepEqual(configurationPatch(saved, draft).changes.Persistence.Sharding.HashSharding.ExpansionPlan.Stages, []);
  for (const value of [[''], [' '], [1]]) { draft[key] = JSON.stringify(value); assert.throws(() => configurationPatch(saved, draft), /每个列表项/); }
});
