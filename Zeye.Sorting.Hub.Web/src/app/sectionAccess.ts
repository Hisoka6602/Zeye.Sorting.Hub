import type { AccessSession } from '../data/api/accessTypes';

/** 采用服务端的固定角色判断，通用管理权限和角色显示名称不能授予敏感版块访问权。 */
export function canAccessRestrictedSections(session?: Pick<AccessSession, 'authenticated' | 'isSuperAdministrator'>): boolean {
  return session?.authenticated === true && session.isSuperAdministrator === true;
}

/** 菜单、直接路由和快捷入口共用敏感版块边界，兼容编码、大小写及尾部斜线。 */
export function isRestrictedSection(pathOrKey: string): boolean {
  let path: string;
  try { path = decodeURIComponent(pathOrKey).toLowerCase().replace(/\/+$/, ''); }
  catch { return true; }
  return ['test-data', 'governance', 'observability', '/parcels/new', '/parcels/batch', '/parcels/detection/new'].includes(path)
    || ['/governance/sharding', '/governance/archive-tasks', '/governance/parcel-cleanup', '/audit', '/diagnostics']
      .some(root => path === root || path.startsWith(root + '/'));
}
