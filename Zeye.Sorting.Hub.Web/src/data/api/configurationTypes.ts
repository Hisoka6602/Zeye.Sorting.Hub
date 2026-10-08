/** 当前配置使用原始 JSON 类型；历史版本使用字符串，避免精度和比较问题。 */
export type ConfigurationValue = string | number | boolean | null | ConfigurationValue[] | ConfigurationObject;
export interface ConfigurationObject { [key: string]: ConfigurationValue }
export interface RuntimeConfigurationSnapshot {
  configuration: ConfigurationObject;
  effectiveConfiguration: Record<string, string | null>;
  revision: string;
  storagePath: string;
  overriddenKeys: string[];
  restartRequiredKeys: string[];
  hotReloadKeys: string[];
  lastReloadError: string | null;
}
export interface RuntimeConfigurationSaved {
  result: { revision: string; changedKeys: string[]; restartRequiredKeys: string[] };
  snapshot: RuntimeConfigurationSnapshot;
}
/** 启动网页与业务就绪独立；本机访问码只保存在受保护文件中。 */
export interface DatabaseStartupStatus {
  ready: boolean;
  requiresConfiguration: boolean;
  localSetupAllowed: boolean;
  setupKeyPath: string | null;
}
export interface ConfigurationHistoryEntry {
  id: string; documentKey: string; previousRevision: string; revision: string;
  beforeJson: string; afterJson: string; changedKeys: string[]; recordedAtLocal: string;
  status: 'Pending' | 'Committed' | 'Failed' | 'Unconfirmed';
}
