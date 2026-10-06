import { App, Breadcrumb, Button, Dropdown, Layout, Menu, Result, Spin, Tooltip, type MenuProps } from 'antd';
import { BellOutlined, DownOutlined, MenuFoldOutlined, MenuUnfoldOutlined } from '@ant-design/icons';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { crumbsForPath, defaultOpenKeys, getSelected, navigationItems } from './navigation';
import { BrandMark } from '../components/BrandMark';
import { HeaderBrand } from './HeaderBrand';
import { contentFrameForPath } from './contentFrame';
import { useAccessSession, sessionChanged } from '../data/api/useAccessSession';
import { requestApi } from '../data/api/client';
import { ApiFeedback } from '../components/ApiFeedback';
import { AccountAvatar } from '../components/AccountAvatar';
import { canAccessRestrictedSections, isRestrictedSection } from './sectionAccess';

const { Header, Sider, Content } = Layout;

export function AppShell({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
  const session = useAccessSession();
  const { message } = App.useApp();
  const isRestrictedPage = isRestrictedSection(location.pathname);
  const canUseRestrictedSections = canAccessRestrictedSections(session.data);
  const needsAuthentication = session.data?.enforceAuthorization || location.pathname === '/profile' || isRestrictedPage;
  useEffect(() => {
    if (session.data && needsAuthentication && !session.data.authenticated)
      navigate(location.pathname === '/profile' ? '/access/login?returnTo=%2Fprofile' : '/access/login', { replace: true });
  }, [session.data, needsAuthentication, location.pathname, navigate]);
  const accountName = session.data?.authenticated ? session.data.name || '已登录' : '未登录';
  const logout = async () => {
    try {
      await requestApi('/api/access/logout', undefined, { method: 'POST' });
      sessionChanged(); navigate('/access/login');
    } catch (error) {
      message.error(error instanceof Error ? error.message : '退出登录失败，请重试');
    }
  };
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [openKeys, setOpenKeys] = useState<string[]>(defaultOpenKeys);
  const siderCollapsed = collapsed && !mobileOpen;
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);
  useEffect(() => {
    const desktop = window.matchMedia('(min-width: 821px)');
    const closeMobileMenu = () => {
      if (desktop.matches) setMobileOpen(false);
    };
    desktop.addEventListener('change', closeMobileMenu);
    return () => desktop.removeEventListener('change', closeMobileMenu);
  }, []);
  const crumbs = crumbsForPath(location.pathname);
  const accountItems: MenuProps['items'] = [
    { key: 'account', label: accountName, disabled: true },
    { key: 'profile', label: '个人中心', disabled: !session.data?.authenticated, onClick: () => navigate('/profile') },
    { key: 'access', label: '账号与权限', onClick: () => navigate('/access') },
    { key: 'login', label: session.data?.authenticated ? '退出登录' : '登录', onClick: () => session.data?.authenticated ? void logout() : navigate('/access/login') },
    { key: 'planned', label: '功能页面', children: [
      { key: 'live', label: '实时运行态势', onClick: () => navigate('/operations/live') },
      { key: 'rules', label: '规则管理', onClick: () => navigate('/rules') },
      { key: 'analytics', label: '分析报表', onClick: () => navigate('/analytics') },
      { key: 'backup', label: '备份与恢复', onClick: () => navigate('/governance/backup') },
      ...(canUseRestrictedSections ? [{ key: 'partition', label: '分区管理', onClick: () => navigate('/governance/sharding') }] : []),
      { key: 'settings', label: '系统配置', onClick: () => navigate('/settings') },
    ] },
  ];
  return <Layout className="app-layout shared-shell">
    {mobileOpen && <div className="mobile-scrim" onClick={() => setMobileOpen(false)} />}
    <Sider width={246} collapsedWidth={72} collapsed={siderCollapsed} className={`app-sider ${mobileOpen ? 'mobile-open' : ''}`} trigger={null} theme="light">
      <div className="brand" role="button" aria-label="Zeye Sorting Hub" tabIndex={0} onClick={() => navigate('/data-overview')} onKeyDown={event => event.key === 'Enter' && navigate('/data-overview')}>
        {siderCollapsed ? <BrandMark /> : <HeaderBrand />}
      </div>
      <Menu mode="inline" theme="light" items={navigationItems.filter(item => canUseRestrictedSections || !isRestrictedSection(String(item?.key ?? '')))} selectedKeys={[getSelected(location.pathname)]} openKeys={siderCollapsed ? [] : openKeys} onOpenChange={keys => setOpenKeys(keys)} onClick={({ key }) => key.startsWith('/') && navigate(key)} className="side-menu" inlineIndent={20} />
      <Tooltip title={collapsed ? '展开导航' : '收起导航'}><Button className="collapse-button" aria-label={collapsed ? '展开导航' : '收起导航'} type="text" icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />} onClick={() => setCollapsed(!collapsed)} /></Tooltip>
    </Sider>
    <Layout className="body-layout">
      <Header className="app-header">
        <Button className="mobile-menu-button" aria-label="打开导航" type="text" icon={<MenuUnfoldOutlined />} onClick={() => { setOpenKeys(keys => keys.length ? keys : defaultOpenKeys()); setMobileOpen(true); }} />
        <div className="header-spacer" />
        {canUseRestrictedSections && <Tooltip title="查看健康检查"><Button className="header-icon" aria-label="查看健康检查" type="text" icon={<BellOutlined />} onClick={() => navigate('/diagnostics/health')} /></Tooltip>}
        <Dropdown menu={{ items: accountItems }} trigger={['click']}><Button type="text" aria-label={accountName + '，账号菜单'} className="account-button"><AccountAvatar src={session.data?.authenticated ? session.data.avatarUrl : undefined} name={session.data?.name} /><span>{accountName}</span><DownOutlined className="account-chevron" /></Button></Dropdown>
      </Header>
      <Content className="app-content">
        <Breadcrumb aria-label="面包屑导航" items={crumbs.map(({ title, href }, index) => ({ title: index === crumbs.length - 1
          ? <span aria-current="page">{title}</span>
          : href && (canUseRestrictedSections || !isRestrictedSection(href)) ? <Link to={href}>{title}</Link> : <span>{title}</span> }))} className="page-breadcrumb" />
        <div className="page-body" style={contentFrameForPath(location.pathname)}>
          {session.loading || needsAuthentication && !session.data?.authenticated
            ? <Spin aria-label="正在验证登录状态" />
            : session.error ? <ApiFeedback error={session.error} retry={session.refresh} />
              : isRestrictedPage && !canUseRestrictedSections ? <Result status="403" title="仅限超级管理员访问" subTitle="测试数据、数据治理和可观测性仅对超级管理员及内置超级用户开放。" extra={<Button type="primary" onClick={() => navigate('/data-overview')}>返回数据概览</Button>} /> : children}
        </div>
      </Content>
    </Layout>
  </Layout>;
}

export function LoginShell({ children }: { children: ReactNode }) {
  return <div className="login-shell"><div className="login-content">{children}</div></div>;
}
