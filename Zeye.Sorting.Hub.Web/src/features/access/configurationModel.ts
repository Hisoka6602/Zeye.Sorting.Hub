import type { ConfigurationObject, ConfigurationValue, RuntimeConfigurationSnapshot } from '../../data/api/configurationTypes.ts';

/** 分类按用途组织，剩余旧版字段仍可从“其他配置”维护。 */
export const configurationCategories = [
  { key: 'online', name: '在线参数', description: '支持热更新的配置' },
  { key: 'audit', name: '请求审计', description: '采样、正文与后台队列' },
  { key: 'logs', name: '日志与资源', description: '日志保留与资源阈值' },
  { key: 'access', name: '访问保护', description: '授权开关与接口凭据' },
  { key: 'hosting', name: '服务运行', description: '监听地址与运行环境' },
  { key: 'database', name: '数据库连接', description: '连接、只读副本与预热' },
  { key: 'governance', name: '数据治理', description: '备份、保留与迁移策略' },
  { key: 'sharding', name: '分表策略', description: '分片、预建与巡检' },
  { key: 'tuning', name: '数据库调优', description: '性能参数与自动调优' },
  { key: 'storage', name: '对象存储', description: '图片与文件存储' },
  { key: 'other', name: '其他配置', description: '兼容导入的扩展字段' },
] as const;
export type ConfigurationCategory = typeof configurationCategories[number]['key'];
export interface ConfigurationField { key: string; path: string[]; value: ConfigurationValue; category: ConfigurationCategory }
export type ConfigurationDraft = Record<string, string | number | boolean | null>;

/** 数据保留策略数组的唯一配置入口，与后端 DataRetentionOptions 一致。 */
export const retentionPolicyConfigurationKey = 'Persistence:Retention:Policies';
/** 后端 DataRetentionPolicy 支持的保留对象。 */
export const retentionPolicyNames = ['WebRequestAuditLog', 'InboxMessage', 'IdempotencyRecord', 'ArchiveTask', 'DeadLetterWriteEntry', 'SlowQueryProfile'] as const;
/** 编辑时记录原始属性名和完整对象，保留旧配置大小写与扩展属性。 */
export interface RetentionPolicyDraftRow { value: ConfigurationObject; nameKey: string; daysKey: string }

/** 时间配置均使用本地格式；其他旧版或扩展字段不会误用日期控件。 */
export function configurationTemporalKind(key: string): 'time' | 'datetime' | undefined {
  const normalized = key.toLowerCase();
  if (normalized === 'persistence:sharding:parcelstarttime') return 'datetime';
  return ['persistence:autotuning:dailyreportlocaltime', 'persistence:autotuning:autonomous:execution:peakstartlocaltime', 'persistence:autotuning:autonomous:execution:peakendlocaltime'].includes(normalized) ? 'time' : undefined;
}

/** 校验本地日历与时间，不通过时区转换或日期自动进位修复无效输入。 */
export function isValidConfigurationTemporalValue(kind: 'time' | 'datetime', value: ConfigurationValue): boolean {
  if (typeof value !== 'string') return false;
  const time = kind === 'time' ? value.trim() : value.trim().split(/[T ]/)[1];
  if (!time || !/^\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?$/.test(time)) return false;
  const [hour, minute, second = 0] = time.split(':').map(Number);
  if (hour > 23 || minute > 59 || second >= 60 || (kind === 'time' && time.includes('.'))) return false;
  if (kind === 'time') return true;
  const date = /^(\d{4})-(\d{2})-(\d{2})[T ]\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?$/.exec(value.trim());
  if (!date) return false;
  const [, year, month, day] = date.map(Number);
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  return year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= days[month - 1];
}

/** 字符串列表逐项编辑，保留顺序与重复项；复杂旧数组继续使用原有 JSON 编辑。 */
export function configurationStringList(input: ConfigurationValue): string[] | undefined {
  let value = input;
  if (typeof input === 'string') { try { value = JSON.parse(input) as ConfigurationValue; } catch { return undefined; } }
  return Array.isArray(value) && value.every(item => typeof item === 'string') ? value as string[] : undefined;
}

/** 结构化编辑仅接管可识别的策略数组；其他历史格式继续使用 JSON 编辑。 */
export function configurationRetentionPolicyRows(input: ConfigurationValue): RetentionPolicyDraftRow[] | undefined {
  let value = input;
  if (typeof input === 'string') {
    try { value = JSON.parse(input) as ConfigurationValue; } catch { return undefined; }
  }
  if (!Array.isArray(value)) return undefined;
  const rows: RetentionPolicyDraftRow[] = [];
  for (const item of value) {
    if (!item || typeof item !== 'object' || Array.isArray(item)) return undefined;
    const nameKey = Object.keys(item).find(key => key.toLowerCase() === 'name');
    const daysKey = Object.keys(item).find(key => key.toLowerCase() === 'retentiondays');
    if (!nameKey || !daysKey || typeof item[nameKey] !== 'string' || (item[daysKey] !== null && typeof item[daysKey] !== 'number')) return undefined;
    rows.push({ value: item, nameKey, daysKey });
  }
  return rows;
}

/** 匹配字段与数组子项，大小写规则与后端配置源保持一致。 */
export function configurationKeyMatches(key: string, prefix: string): boolean {
  const candidate = key.toLowerCase(); const expected = prefix.toLowerCase();
  return candidate === expected || candidate.startsWith(expected + ':');
}
function categoryFor(key: string): ConfigurationCategory {
  const matches = (...prefixes: string[]) => prefixes.some(prefix => configurationKeyMatches(key, prefix));
  if (matches('WebRequestAuditLog', 'AuditReadOnlyApi')) return 'audit';
  if (matches('LogCleanup', 'Logging', 'ResourceThresholds')) return 'logs';
  if (matches('Access')) return 'access';
  if (matches('Hosting', 'AllowedHosts', 'Urls')) return 'hosting';
  if (matches('ConnectionStrings', 'Persistence:Provider', 'Persistence:MySql', 'Persistence:ReadOnlyDatabase', 'Persistence:DatabaseBootstrap', 'Persistence:ConnectionPool', 'Persistence:ConnectionWarmup')) return 'database';
  if (matches('Persistence:Backup', 'Persistence:DataRetention', 'Persistence:Retention', 'Persistence:Archive', 'Persistence:Archiving', 'Persistence:Migration', 'Persistence:MigrationGovernance', 'Persistence:BaselineData', 'Persistence:RepositoryDangerousActions')) return 'governance';
  if (/^persistence:.*sharding/i.test(key)) return 'sharding';
  if (matches('Persistence')) return 'tuning';
  if (matches('ObjectStorage')) return 'storage';
  return 'other';
}

/** 数组作为完整字段编辑，启动引导和 Fusion 目录由各自入口维护。 */
export function configurationFields(configuration: ConfigurationObject): ConfigurationField[] {
  const fields: ConfigurationField[] = [];
  const visit = (value: ConfigurationValue, path: string[]) => {
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      for (const [key, child] of Object.entries(value)) visit(child, [...path, key]);
    } else {
      const key = path.join(':'); fields.push({ key, path, value, category: categoryFor(key) });
    }
  };
  for (const [key, value] of Object.entries(configuration)) {
    if (!['configurationstorage', 'kestrel', 'fusioningestion'].includes(key.toLowerCase())) visit(value, [key]);
  }
  return fields;
}
export function configurationDraft(configuration: ConfigurationObject): ConfigurationDraft {
  return Object.fromEntries(configurationFields(configuration).map(field => [field.key, Array.isArray(field.value) ? JSON.stringify(field.value, null, 2) : field.value])) as ConfigurationDraft;
}
export function isHotConfigurationField(field: ConfigurationField, snapshot: RuntimeConfigurationSnapshot): boolean {
  return snapshot.hotReloadKeys.some(key => configurationKeyMatches(key, field.key));
}

/** 只提交实际修改，先验证原始类型和数组，未修改的凭据不回传。 */
export function configurationPatch(configuration: ConfigurationObject, draft: ConfigurationDraft): { changes: ConfigurationObject; changedKeys: string[] } {
  const changes: ConfigurationObject = Object.create(null) as ConfigurationObject;
  const changedKeys: string[] = [];
  for (const field of configurationFields(configuration)) {
    if (!Object.hasOwn(draft, field.key)) continue;
    const input = draft[field.key];
    let next: ConfigurationValue = input;
    if (Array.isArray(field.value)) {
      if (typeof input !== 'string') throw new Error(`${field.key} 必须填写 JSON 数组`);
      try { next = JSON.parse(input) as ConfigurationValue; } catch { throw new Error(`${field.key} 的 JSON 数组格式无效`); }
      if (!Array.isArray(next)) throw new Error(`${field.key} 必须填写 JSON 数组`);
    } else if (typeof field.value === 'number') {
      if (typeof input !== 'number' || !Number.isFinite(input)) throw new Error(`${field.key} 必须填写有效数字`);
    } else if (typeof field.value === 'boolean' && typeof input !== 'boolean') throw new Error(`${field.key} 必须为开关值`);
    else if (typeof field.value === 'string' && typeof input !== 'string') throw new Error(`${field.key} 必须填写文本`);
    if (JSON.stringify(field.value) === JSON.stringify(next)) continue;
    const temporal = configurationTemporalKind(field.key);
    if (temporal && next !== '' && !isValidConfigurationTemporalValue(temporal, next)) throw new Error(`${field.key} 必须选择有效的本地${temporal === 'time' ? '时间' : '日期和时间'}`);
    if (['webrequestauditlog:excludedpathprefixes', 'persistence:sharding:hashsharding:expansionplan:stages', 'persistence:autotuning:autonomous:execution:whitelistedtables'].includes(field.key.toLowerCase())
      && (!Array.isArray(next) || next.some(item => typeof item !== 'string' || !item.trim()))) throw new Error(`${field.key} 的每个列表项都必须填写内容；可移除空项或清空列表`);
    if (field.key.toLowerCase() === retentionPolicyConfigurationKey.toLowerCase()) {
      const rows = configurationRetentionPolicyRows(next);
      if (!rows || rows.some(row => !(retentionPolicyNames as readonly string[]).includes(String(row.value[row.nameKey]).trim())
        || typeof row.value[row.daysKey] !== 'number' || !Number.isInteger(row.value[row.daysKey])
        || Number(row.value[row.daysKey]) < 1 || Number(row.value[row.daysKey]) > 3650)) {
        throw new Error('数据保留策略必须选择支持的保留对象，并填写 1～3650 的整数天数');
      }
    }
    let parent = changes;
    for (const segment of field.path.slice(0, -1)) {
      if (!Object.hasOwn(parent, segment)) parent[segment] = Object.create(null) as ConfigurationObject;
      parent = parent[segment] as ConfigurationObject;
    }
    parent[field.path.at(-1)!] = next; changedKeys.push(field.key);
  }
  return { changes, changedKeys };
}

/** 历史值按路径读取，兼容独立配置文档的数组根。 */
export function configurationValueAt(document: ConfigurationValue, key: string): ConfigurationValue | undefined {
  let current: ConfigurationValue | undefined = document;
  for (const segment of key.split(':')) {
    if (current === null || typeof current !== 'object') return undefined;
    current = Array.isArray(current) ? current[Number(segment)] : Object.entries(current).find(([name]) => name.toLowerCase() === segment.toLowerCase())?.[1];
  }
  return current;
}
