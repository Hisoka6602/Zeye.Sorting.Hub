export interface ParcelCleanupRecord {
  id: string;
  operator: { userId: string; account: string; name: string; clientIp: string; traceId: string };
  createdBefore: string;
  startedAtLocal: string;
  completedAtLocal?: string;
  decision: string;
  status: string;
  plannedCount: number;
  executedCount: number;
  batchCount: number;
  storageFormat?: string;
  scope: string;
  compensationBoundary: string;
  errorMessage?: string;
}
export interface ParcelCleanupResponse { cleanupRecordId?: string; decision: string; plannedCount: number; executedCount: number; compensationBoundary: string }
export interface ParcelCleanupDetail { record: ParcelCleanupRecord }
export const cleanupDecisionLabels: Record<string, string> = { blocked: '已阻止', 'dry-run': '仅演练', execute: '已执行' };
export function cleanupStatusLabel(record: ParcelCleanupRecord) {
  if (record.status === 'completed') return cleanupDecisionLabels[record.decision] ?? '已完成';
  return ({ running: '执行中', failed: record.executedCount > 0 ? '部分执行后失败' : '执行失败', cancelled: '已取消' } as Record<string, string>)[record.status] ?? record.status;
}
