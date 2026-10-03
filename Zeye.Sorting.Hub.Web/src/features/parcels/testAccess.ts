import type { AccessSession } from '../../data/api/accessTypes';

/** 手工生成包裹的页面始终属于管理员测试流程。 */
export function isParcelTestPath(path: string): boolean {
  return ['/parcels/new', '/parcels/batch', '/parcels/detection/new'].includes(path.toLowerCase().replace(/\/+$/, ''));
}

export function canManageParcelTests(session?: Pick<AccessSession, 'authenticated' | 'permissions'>): boolean {
  return session?.authenticated === true && session.permissions.includes('access.manage');
}
