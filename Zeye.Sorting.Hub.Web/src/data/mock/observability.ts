export interface AuditRecord {
  id: number;
  time: string;
  method: string;
  path: string;
  status: number;
  duration: number;
  trace: string;
  correlation: string;
  service: string;
  error?: string;
}

export const auditRecords: AuditRecord[] = [
  { id: 1001, time: '2026-09-25 09:12:34', method: 'GET', path: '/api/packages/2509250001', status: 200, duration: 120, trace: 'c3f9a1e2b7d84f10', correlation: '9a8b7c6d5e4f3a21', service: 'parcel-service' },
  { id: 1002, time: '2026-09-25 09:18:27', method: 'POST', path: '/api/packages', status: 200, duration: 350, trace: 'ad12b3c4d5e6f789', correlation: 'f0e1d2c3b4a59687', service: 'parcel-service' },
  { id: 1003, time: '2026-09-25 10:03:51', method: 'GET', path: '/api/workstations', status: 500, duration: 1200, trace: '5f6e7d8c9b0a1d2e', correlation: '1a2b3c4d5e6f7g80', service: 'sorting-service', error: '上游服务暂时不可用' },
  { id: 1004, time: '2026-09-25 11:20:17', method: 'PUT', path: '/api/packages/2509250003', status: 200, duration: 280, trace: '9b8a7c6d5e4f3a21', correlation: '2b3c4d5e6f708190', service: 'parcel-service' },
  { id: 1005, time: '2026-09-25 13:45:06', method: 'DELETE', path: '/api/packages/2509250004', status: 400, duration: 95, trace: 'af1e2d3c4b5a6978', correlation: '3c4d5e6f708192a1', service: 'parcel-service', error: '包裹状态不允许删除' },
  { id: 1006, time: '2026-09-25 15:02:19', method: 'GET', path: '/api/labels/generate', status: 200, duration: 410, trace: 'd4c3b2a1908f7e6d', correlation: '4d5e6f708192a3c4', service: 'label-service' },
  { id: 1007, time: '2026-09-25 16:28:40', method: 'POST', path: '/api/sort/dispatch', status: 500, duration: 980, trace: '7e6d5c4b3a2910f8', correlation: '5e6f708192a3c4d5', service: 'sorting-service', error: '设备应答超时' },
];

export interface SlowQuery {
  fingerprint: string;
  calls: number;
  average: number;
  p95: number;
  p99: number;
  max: number;
  timeout: number;
  errors: number;
  deadlocks: number;
  lastSeen: string;
  sql: string;
  sample: string;
}

const parcelSql = `SELECT\n    p.id,\n    p.tracking_no,\n    p.status,\n    p.create_time,\n    p.update_time\nFROM package p\nLEFT JOIN package_event e ON e.package_id = p.id\nLEFT JOIN package_route r ON r.package_id = p.id\nWHERE p.status IN ('IN_TRANSIT', 'ARRIVED')\n   AND p.create_time >= ?\n   AND p.create_time < ?\nORDER BY p.update_time DESC\nLIMIT 100;`;

export const slowQueries: SlowQuery[] = [
  { fingerprint: 'f3a9b72e9c4d1a8b', calls: 12458, average: 320.5, p95: 980.2, p99: 1562.3, max: 3421.7, timeout: 12, errors: 3, deadlocks: 0, lastSeen: '2026-09-25 16:18:41', sql: parcelSql, sample: parcelSql.replaceAll('?', "'2026-09-25 00:00:00'") },
  { fingerprint: 'c8d1e4f7b2a9c003', calls: 8932, average: 186.4, p95: 620.1, p99: 1203.6, max: 2888.5, timeout: 5, errors: 1, deadlocks: 0, lastSeen: '2026-09-25 16:17:03', sql: 'UPDATE sorting_task SET status = ? WHERE task_id = ?;', sample: "UPDATE sorting_task SET status = 'DONE' WHERE task_id = 21039;" },
  { fingerprint: '9e2d7c4b1f8a6e11', calls: 26315, average: 75.2, p95: 310.4, p99: 689.7, max: 1954.1, timeout: 2, errors: 0, deadlocks: 0, lastSeen: '2026-09-25 16:19:27', sql: 'SELECT COUNT(1) FROM package WHERE status = ?;', sample: "SELECT COUNT(1) FROM package WHERE status = 'IN_TRANSIT';" },
  { fingerprint: 'd1f7a3c9e5b6d842', calls: 4308, average: 412.8, p95: 1125.6, p99: 2301.9, max: 4987.3, timeout: 18, errors: 4, deadlocks: 1, lastSeen: '2026-09-25 16:16:02', sql: 'INSERT INTO sorting_event (parcel_id, event_type) VALUES (?, ?);', sample: "INSERT INTO sorting_event (parcel_id, event_type) VALUES (2509250004, 'ERROR');" },
  { fingerprint: '6b9c2e1a4d8f7b33', calls: 19672, average: 133.6, p95: 498.2, p99: 1020.5, max: 2441.9, timeout: 4, errors: 0, deadlocks: 0, lastSeen: '2026-09-25 16:19:11', sql: 'SELECT id, status FROM package WHERE barcode = ?;', sample: "SELECT id, status FROM package WHERE barcode = 'SF3124567890CN';" },
  { fingerprint: 'a7e4d9c2b6f1a0e5', calls: 3941, average: 521.7, p95: 1430.8, p99: 2985.1, max: 6208.4, timeout: 22, errors: 6, deadlocks: 2, lastSeen: '2026-09-25 16:14:38', sql: 'DELETE FROM package WHERE create_time < ?;', sample: "DELETE FROM package WHERE create_time < '2025-09-25';" },
  { fingerprint: 'b4c8e6d1f9a2c773', calls: 11203, average: 98.3, p95: 356.9, p99: 812.5, max: 2003.6, timeout: 3, errors: 1, deadlocks: 0, lastSeen: '2026-09-25 16:18:09', sql: 'SELECT * FROM archive_task WHERE status = ?;', sample: "SELECT * FROM archive_task WHERE status = 'PENDING';" },
];
