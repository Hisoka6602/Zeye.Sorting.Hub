export interface BackupJob { id: number; name: string; source: 'MySQL' | 'SQL Server'; kind: '全量备份' | '增量备份'; lastRun: string; status: '已完成' | '执行中' | '失败'; retention: number }
export const initialBackupJobs: BackupJob[] = [
  { id: 1, name: 'daily-mysql-prod', source: 'MySQL', kind: '增量备份', lastRun: '2026-09-25 02:03:12', status: '已完成', retention: 30 },
  { id: 2, name: 'weekly-mysql-prod', source: 'MySQL', kind: '全量备份', lastRun: '2026-09-21 02:00:08', status: '已完成', retention: 30 },
  { id: 3, name: 'daily-sqlserver-ops', source: 'SQL Server', kind: '增量备份', lastRun: '2026-09-25 02:15:27', status: '执行中', retention: 30 },
  { id: 4, name: 'weekly-sqlserver-ops', source: 'SQL Server', kind: '全量备份', lastRun: '2026-09-21 02:10:03', status: '已完成', retention: 30 },
  { id: 5, name: 'daily-mysql-analytics', source: 'MySQL', kind: '增量备份', lastRun: '2026-09-24 02:00:11', status: '失败', retention: 30 },
  { id: 6, name: 'weekly-sqlserver-report', source: 'SQL Server', kind: '全量备份', lastRun: '2026-09-14 02:00:05', status: '已完成', retention: 30 },
  { id: 7, name: 'daily-mysql-staging', source: 'MySQL', kind: '增量备份', lastRun: '2026-09-25 02:01:33', status: '已完成', retention: 14 },
  { id: 8, name: 'weekly-sqlserver-staging', source: 'SQL Server', kind: '全量备份', lastRun: '2026-09-21 02:05:18', status: '已完成', retention: 14 },
];

export interface Partition { id: number; month: string; source: 'MySQL' | 'SQL Server'; table: string; rows: number; size: string; status: '正常' | '待审核'; retention: string }
export const initialPartitions: Partition[] = [
  { id: 1, month: '2026-09', source: 'MySQL', table: 'parcel_202609', rows: 11843221, size: '16.8 GB', status: '正常', retention: '2028-09-30' },
  { id: 2, month: '2026-09', source: 'SQL Server', table: 'parcel_202609', rows: 11791002, size: '17.1 GB', status: '正常', retention: '2028-09-30' },
  { id: 3, month: '2026-08', source: 'MySQL', table: 'parcel_202608', rows: 10552318, size: '15.2 GB', status: '正常', retention: '2028-08-31' },
  { id: 4, month: '2026-08', source: 'SQL Server', table: 'parcel_202608', rows: 10498771, size: '15.6 GB', status: '正常', retention: '2028-08-31' },
  { id: 5, month: '2026-07', source: 'MySQL', table: 'parcel_202607', rows: 12015664, size: '17.8 GB', status: '正常', retention: '2028-07-31' },
  { id: 6, month: '2026-07', source: 'SQL Server', table: 'parcel_202607', rows: 11962441, size: '17.5 GB', status: '正常', retention: '2028-07-31' },
];
