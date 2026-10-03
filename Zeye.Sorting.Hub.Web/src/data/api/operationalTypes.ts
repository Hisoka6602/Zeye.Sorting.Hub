/** 运维接口合同中的编号始终保留为字符串。 */
export interface PagedResult<T> { items: T[]; pageNumber: number; pageSize: number; totalCount: number }
export interface AuditItem { id: string; traceId: string; correlationId: string; requestMethod: string; requestPath: string; statusCode: number; isSuccess: boolean; startedAt: string; durationMs: number }
export interface AuditDetail extends AuditItem {
  endedAt: string; requestRouteTemplate: string; userName: string; isAuthenticated: boolean; requestHost: string; spanId: string;
  requestSizeBytes: number; responseSizeBytes: number; requestHeadersJson: string; responseHeadersJson: string;
  requestBody: string; responseBody: string; curlCommand: string; errorMessage: string; exceptionType: string; exceptionStackTrace: string;
  databaseOperationSummary: string; databaseAccessCount: number; databaseDurationMs: number; requestUrl: string;
}
export interface SlowQuery {
  fingerprint: string; normalizedSql: string; sampleSql: string; callCount: number; averageElapsedMilliseconds: number;
  p95Milliseconds: number; p99Milliseconds: number; maxMilliseconds: number; timeoutCount: number; errorCount: number; deadlockCount: number;
  lastOccurredAtLocal: string; windowStartedAtLocal: string; windowEndedAtLocal: string;
}
export interface SlowQuerySnapshot { generatedAtLocal: string; totalFingerprintCount: number; items: SlowQuery[] }
export interface HealthReport { status: string; generatedAt: string; entries: Record<string, { status: string; description?: string; durationMs?: number }> }
export interface ArchiveTask {
  id: string; taskType: string; status: string; isDryRun: boolean; retentionDays: number; plannedItemCount: number; processedItemCount: number;
  requestedBy: string; remark: string; planSummary: string; failureMessage: string; retryCount: number; createdAt: string; updatedAt: string;
}
export interface BackupStatus {
  status: string; summary: string; provider?: string; database?: string; recordedAtLocal?: string; verifiedBackupAtLocal?: string;
  hasBackupFile: boolean; isBackupFileFresh: boolean; isEnabled: boolean; isDryRun: boolean; pollIntervalMinutes: number; maxAllowedBackupAgeHours: number;
  automaticBackups: boolean; backupIntervalMinutes: number;
}
export interface PartitionStatus {
  provider: string; granularity: string; currentSuffix: string; allowTableCreation: boolean; creationDryRun: boolean;
  prebuild?: { generatedAtLocal: string; isEnabled: boolean; isDryRun: boolean; message: string; plannedPhysicalTables: string[]; missingPhysicalTables: string[] };
  entries: { suffix: string; start: string; end: string; createdTime: string }[];
}
export interface ConfigurationSnapshot { environment: string; settings: { category: string; name: string; value: string }[] }
export interface OperationalPolicy { revision: number; automaticBackups: boolean; backupIntervalMinutes: number; prebuildAheadHours: number }
export interface BackupArtifact { id: string; database: string; createdAtLocal: string; requestedBy: string; sizeBytes: number; sha256: string; tableRows: Record<string, number>; restoredDatabase?: string; verifiedAtLocal?: string }
export interface BackupArtifacts { isSupported: boolean; artifacts: BackupArtifact[] }
export function localTime(value?: string | null) { return value ? value.replace('T', ' ').replace(/\.\d+(?:[+-]\d{2}:\d{2})?$|[+-]\d{2}:\d{2}$/, '') : '-'; }
export const archiveStatusLabels: Record<string, string> = { Pending: '待生成', Running: '执行中', Completed: '已完成', Failed: '失败' };
