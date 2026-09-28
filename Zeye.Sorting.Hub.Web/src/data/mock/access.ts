export interface Role { id: number; name: string; description: string; members: number; modified: string; builtIn?: boolean; permissions: string[] }
export const initialRoles: Role[] = [
  { id: 1, name: '超级管理员', description: '拥有所有权限，系统初始化账号使用。', members: 3, modified: '2026-09-20 14:22:11', builtIn: true, permissions: ['查看包裹台账', '查看包裹详情', '新建包裹', '批量入队', '查看操作日志', '查看请求审计', '执行归档任务'] },
  { id: 2, name: '运营管理员', description: '负责日常包裹管理与运营操作。', members: 12, modified: '2026-09-24 10:18:45', permissions: ['查看包裹台账', '查看包裹详情', '新建包裹', '批量入队', '数据治理'] },
  { id: 3, name: '仓库操作员', description: '负责包裹入库、分拣等基础操作。', members: 28, modified: '2026-09-22 16:03:20', permissions: ['查看包裹台账', '查看包裹详情', '批量入队'] },
  { id: 4, name: '数据分析师', description: '可查看报表与数据，无法执行写入操作。', members: 6, modified: '2026-09-18 11:54:03', permissions: ['查看包裹台账', '查看包裹详情'] },
  { id: 5, name: '审计专员', description: '负责审计日志与操作追踪。', members: 4, modified: '2026-09-17 09:31:12', permissions: ['查看请求审计', '查看操作日志'] },
  { id: 6, name: '系统维护', description: '负责系统配置与基础运维。', members: 5, modified: '2026-09-21 13:07:56', permissions: ['查看操作日志', '执行归档任务'] },
];
