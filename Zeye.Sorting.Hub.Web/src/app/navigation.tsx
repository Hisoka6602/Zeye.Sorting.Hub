import type { MenuProps } from 'antd';
import { VectorIcon, type VectorIconName } from '../components/VectorIcon';

type NavItem = Required<MenuProps>['items'][number];
const icon = (name: VectorIconName) => <VectorIcon name={name} className="shell-nav-icon" size={20} />;

// One menu tree for every authenticated route. Route changes only select an item.
export const navigationItems: NavItem[] = [
  { key: '/overview', icon: icon('home'), label: '工作台' },
  { key: 'parcels', icon: icon('parcels'), label: '包裹中心', children: [
    { key: '/parcels', label: '包裹台账' }, { key: '/parcels/new', label: '新建包裹' }, { key: '/parcels/batch', label: '批量入队' },
  ] },
  { key: 'governance', icon: icon('governance'), label: '数据治理', children: [
    { key: '/governance/archive-tasks', label: '归档任务' }, { key: '/governance/outbox', label: 'Outbox 消息' },
    { key: '/governance/parcel-cleanup', label: '过期清理' }, { key: '/governance/backup', label: '备份与恢复' }, { key: '/governance/sharding', label: '分区管理' },
  ] },
  { key: 'observability', icon: icon('observability'), label: '可观测性', children: [
    { key: '/audit/requests', label: '请求审计' }, { key: '/diagnostics/slow-queries', label: '慢查询' }, { key: '/diagnostics/health', label: '健康检查' },
  ] },
  { key: '/operations/live', icon: icon('operations'), label: '运行态势' },
  { key: '/rules', icon: icon('rules'), label: '规则管理' },
  { key: '/analytics', icon: icon('analytics'), label: '分析报表' },
  { key: 'system', icon: icon('system'), label: '系统管理', children: [
    { key: '/access', label: '账号与权限' }, { key: '/settings', label: '系统配置' },
  ] },
  { key: '/help', icon: icon('guide'), label: '操作指南' },
];

export const defaultOpenKeys = () => ['parcels', 'governance', 'observability', 'system'];
export const getSelected = (pathname: string) => {
  if (pathname === '/parcels/detection/new') return '/parcels/new';
  if (pathname.startsWith('/parcels/') && !['/parcels/new', '/parcels/batch'].includes(pathname)) return '/parcels';
  if (pathname.startsWith('/audit/requests/')) return '/audit/requests';
  if (pathname.startsWith('/diagnostics/slow-queries/')) return '/diagnostics/slow-queries';
  return pathname;
};

export interface NavigationCrumb { title: string; href?: string }
const crumbMap: Record<string, [string, string, string]> = {
  '/parcels': ['包裹中心', '/parcels', '包裹台账'],
  '/parcels/new': ['包裹中心', '/parcels', '新建包裹'],
  '/parcels/detection/new': ['包裹中心', '/parcels', '来源检测登记'],
  '/parcels/batch': ['包裹中心', '/parcels', '批量入队'],
  '/governance/parcel-cleanup': ['数据治理', '/governance/archive-tasks', '过期包裹清理'],
  '/governance/archive-tasks': ['数据治理', '/governance/archive-tasks', '归档任务'],
  '/governance/outbox': ['数据治理', '/governance/archive-tasks', 'Outbox 消息'],
  '/governance/backup': ['数据治理', '/governance/archive-tasks', '备份与恢复'],
  '/audit/requests': ['可观测性', '/audit/requests', '请求审计'],
  '/diagnostics/slow-queries': ['可观测性', '/audit/requests', '慢查询画像'],
  '/diagnostics/health': ['可观测性', '/audit/requests', '健康检查'],
  '/help': ['操作指南', '/help', '使用帮助'],
  '/access': ['系统管理', '/access', '账号与权限'],
  '/settings': ['系统管理', '/access', '系统配置'],
  '/operations/live': ['运行态势', '/operations/live', '实时运行'],
  '/rules': ['规则管理', '/rules', '规则列表'],
  '/analytics': ['分析报表', '/analytics', '运营概览'],
};

export function crumbsForPath(pathname: string): NavigationCrumb[] {
  if (pathname === '/overview') return [{ title: '工作台' }];
  const home: NavigationCrumb = { title: '工作台', href: '/overview' };
  if (pathname === '/governance/sharding') return [{ title: '包裹中心', href: '/parcels' }, { title: '分区管理' }];
  if (pathname === '/parcels/detection/new') return [home, { title: '包裹中心', href: '/parcels' }, { title: '来源检测登记' }];
  if (pathname.startsWith('/parcels/') && !['/parcels/new', '/parcels/batch'].includes(pathname)) {
    return [home, { title: '包裹中心', href: '/parcels' }, { title: '包裹台账', href: '/parcels' }, { title: '包裹详情' }];
  }
  if (pathname.startsWith('/audit/requests/')) return [home, { title: '可观测性', href: '/audit/requests' }, { title: '请求审计', href: '/audit/requests' }, { title: '审计详情' }];
  if (pathname.startsWith('/diagnostics/slow-queries/')) return [home, { title: '可观测性', href: '/audit/requests' }, { title: '慢查询', href: '/diagnostics/slow-queries' }, { title: '慢查询详情' }];
  const entry = crumbMap[pathname];
  // A category's default page has no separate category route. Do not render a
  // breadcrumb link that merely reloads the page the user is already viewing.
  return entry ? [home, { title: entry[0], href: entry[1] === pathname ? undefined : entry[1] }, { title: entry[2] }] : [home, { title: '页面不存在' }];
}
