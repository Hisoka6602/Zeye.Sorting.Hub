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

export interface OutboxMessage { id: number; event: string; status: '成功' | '待处理' | '派发中' | '失败' | '死信'; retries: number; created: string; attempted: string; error: string; payload: Record<string, unknown> }
export const initialOutboxMessages: OutboxMessage[] = [
  { id: 1000013487, event: 'PACKAGE_CREATED', status: '成功', retries: 0, created: '2026-09-25 09:12:34', attempted: '2026-09-25 09:12:35', error: '-', payload: { eventType: 'PACKAGE_CREATED', parcelId: 2509250001 } },
  { id: 1000013488, event: 'ROUTE_ASSIGNED', status: '待处理', retries: 0, created: '2026-09-25 09:18:27', attempted: '-', error: '-', payload: { eventType: 'ROUTE_ASSIGNED', parcelId: 2509250002 } },
  { id: 1000013489, event: 'INVENTORY_SYNC', status: '派发中', retries: 1, created: '2026-09-25 10:03:51', attempted: '2026-09-25 10:05:12', error: '-', payload: { eventType: 'INVENTORY_SYNC', parcelId: 2509250003 } },
  { id: 1000013490, event: 'LABEL_PRINTED', status: '失败', retries: 3, created: '2026-09-25 11:20:17', attempted: '2026-09-25 12:03:45', error: '打印服务超时', payload: { eventType: 'LABEL_PRINTED', messageId: '1000013490', occurTime: '2026-09-25T11:20:17+08:00', data: { packageId: '2509250004', trackingNo: 'ZTO5566778899CN', template: 'A02-07', copies: 1, operator: 'system' } } },
  { id: 1000013491, event: 'STATUS_CHANGED', status: '成功', retries: 0, created: '2026-09-25 13:45:06', attempted: '2026-09-25 13:45:07', error: '-', payload: { eventType: 'STATUS_CHANGED', parcelId: 2509250005 } },
  { id: 1000013492, event: 'NOTIFY_WAREHOUSE', status: '死信', retries: 5, created: '2026-09-25 15:02:19', attempted: '2026-09-25 16:10:33', error: '目标服务不可用', payload: { eventType: 'NOTIFY_WAREHOUSE', parcelId: 2509250006 } },
  { id: 1000013493, event: 'BILLING_SYNC', status: '待处理', retries: 0, created: '2026-09-25 16:28:40', attempted: '-', error: '-', payload: { eventType: 'BILLING_SYNC', parcelId: 2509250007 } },
];
