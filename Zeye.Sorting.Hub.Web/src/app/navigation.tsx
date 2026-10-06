import type { MenuProps } from 'antd';
import { VectorIcon, type VectorIconName } from '../components/VectorIcon';

type NavItem = Required<MenuProps>['items'][number];
const icon = (name: VectorIconName) => <VectorIcon name={name} surface="navigation" className="shell-nav-icon" size={20} />;

// One menu tree for every authenticated route. Route changes only select an item.
export const navigationItems: NavItem[] = [
  { key: '/data-overview', className: 'nav-root-data-overview', icon: icon('analytics'), label: '数据概览' },
  { key: '/overview', className: 'nav-root-home', icon: icon('home'), label: '工作台' },
  { key: 'parcels', className: 'nav-root-parcels', icon: icon('parcels'), label: '包裹中心', children: [
    { key: '/parcels', label: '包裹台账' },
  ] },
  { key: 'test-data', className: 'nav-root-test-data', icon: icon('testData'), label: '测试数据', children: [
    { key: '/parcels/new', label: '新建包裹' }, { key: '/parcels/batch', label: '批量入队' },
  ] },
  { key: 'governance', className: 'nav-root-governance', icon: icon('governance'), label: '数据治理', children: [
    { key: '/governance/sharding', label: '分区管理' },
    { key: '/governance/archive-tasks', label: '归档任务' },
    { key: '/governance/parcel-cleanup', label: '过期清理' },
  ] },
  { key: 'observability', className: 'nav-root-observability', icon: icon('observability'), label: '可观测性', children: [
    { key: '/audit/requests', label: '请求审计' }, { key: '/diagnostics/slow-queries', label: '慢查询' }, { key: '/diagnostics/health', label: '健康检查' },
  ] },
  { key: '/operations/live', className: 'nav-root-operations', icon: icon('operations'), label: '实时运行态势' },
  { key: '/rules', className: 'nav-root-rules', icon: icon('rules'), label: '规则管理' },
  { key: '/analytics', className: 'nav-root-analytics', icon: icon('analytics'), label: '分析报表' },
  { key: 'system', className: 'nav-root-system', icon: icon('system'), label: '系统管理', children: [
    { key: '/access', label: '账号与权限' }, { key: '/governance/backup', label: '备份与恢复' }, { key: '/settings', label: '系统配置' }, { key: '/settings/fusion', label: 'Fusion 接入' },
  ] },
  { key: '/help', className: 'nav-root-help', icon: icon('guide'), label: '操作指南' },
];

export const defaultOpenKeys = () => ['parcels', 'test-data', 'governance', 'observability', 'system'];
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
  '/parcels/new': ['测试数据', '/parcels/new', '新建包裹'],
  '/parcels/detection/new': ['测试数据', '/parcels/new', '来源检测登记'],
  '/governance/parcel-cleanup': ['数据治理', '/governance/archive-tasks', '过期包裹清理'],
  '/governance/archive-tasks': ['数据治理', '/governance/archive-tasks', '归档任务'],
  '/audit/requests': ['可观测性', '/audit/requests', '请求审计'],
  '/diagnostics/slow-queries': ['可观测性', '/audit/requests', '慢查询画像'],
  '/diagnostics/health': ['可观测性', '/audit/requests', '健康检查'],
  '/help': ['操作指南', '/help', '使用帮助'],
  '/access': ['系统管理', '/access', '账号与权限'],
  '/settings': ['系统管理', '/access', '系统配置'],
  '/settings/fusion': ['系统管理', '/settings', 'Fusion 接入'],
  '/operations/live': ['运行态势', '/operations/live', '实时运行'],
  '/rules': ['规则管理', '/rules', '规则列表'],
  '/analytics': ['分析报表', '/analytics', '运营概览'],
};

export function crumbsForPath(pathname: string): NavigationCrumb[] {
  if (pathname === '/data-overview') return [{ title: '数据概览' }];
  const home: NavigationCrumb = { title: '数据概览', href: '/data-overview' };
  if (pathname === '/profile') return [home, { title: '个人中心' }];
  if (pathname === '/overview') return [home, { title: '工作台' }];
  if (pathname === '/diagnostics/health') return [{ title: '可观测性', href: '/audit/requests' }, { title: '健康检查' }];
  if (pathname === '/governance/sharding') return [{ title: '数据治理', href: '/governance/archive-tasks' }, { title: '分区管理' }];
  if (pathname === '/governance/backup') return [{ title: '系统管理', href: '/access' }, { title: '备份与恢复' }];
  if (pathname === '/parcels/new') return [home, { title: '测试数据', href: '/parcels/new' }, { title: '新建包裹' }];
  if (pathname === '/parcels/batch') return [home, { title: '测试数据', href: '/parcels/new' }, { title: '批量入队' }];
  if (pathname === '/parcels/detection/new') return [home, { title: '测试数据', href: '/parcels/new' }, { title: '来源检测登记' }];
  if (pathname.startsWith('/parcels/') && !['/parcels/new', '/parcels/batch'].includes(pathname)) {
    return [{ title: '包裹中心', href: '/parcels' }, { title: '包裹台账', href: '/parcels' }, { title: '包裹详情' }];
  }
  if (pathname.startsWith('/audit/requests/')) return [{ title: '可观测性', href: '/audit/requests' }, { title: '请求审计', href: '/audit/requests' }, { title: '审计详情' }];
  if (pathname.startsWith('/diagnostics/slow-queries/')) return [{ title: '可观测性', href: '/audit/requests' }, { title: '慢查询', href: '/diagnostics/slow-queries' }, { title: '慢查询详情' }];
  const entry = crumbMap[pathname];
  if (!entry) return [home, { title: '页面不存在' }];
  // A category opens its existing landing page; some landing pages are also
  // the current child route, so the label remains a navigable category link.
  return [{ title: entry[0], href: entry[1] }, { title: entry[2] }];
}
