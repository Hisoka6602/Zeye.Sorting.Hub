export interface ArchiveTask { id: number; type: string; status: '待生成' | '已完成' | '失败'; retention: number; candidates: number | null; initiator: string; created: string; note?: string }
export const initialArchiveTasks: ArchiveTask[] = [
  { id: 12031, type: 'WebRequestAuditLogHistory', status: '已完成', retention: 180, candidates: 1245678, initiator: '张三', created: '2026-09-25 16:28:40' },
  { id: 12030, type: 'WebRequestAuditLogHistory', status: '待生成', retention: 180, candidates: null, initiator: '李四', created: '2026-09-25 14:12:17' },
  { id: 12029, type: 'WebRequestAuditLogHistory', status: '失败', retention: 180, candidates: 892331, initiator: '王五', created: '2026-09-25 11:03:51' },
  { id: 12028, type: 'WebRequestAuditLogHistory', status: '已完成', retention: 180, candidates: 456120, initiator: '赵六', created: '2026-09-24 18:20:03' },
  { id: 12027, type: 'WebRequestAuditLogHistory', status: '待生成', retention: 180, candidates: null, initiator: '张三', created: '2026-09-24 09:17:22' },
  { id: 12026, type: 'WebRequestAuditLogHistory', status: '已完成', retention: 180, candidates: 2103442, initiator: '李四', created: '2026-09-23 16:45:09' },
  { id: 12025, type: 'WebRequestAuditLogHistory', status: '失败', retention: 180, candidates: 78551, initiator: '王五', created: '2026-09-23 13:08:33' },
];
