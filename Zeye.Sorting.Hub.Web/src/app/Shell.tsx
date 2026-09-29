import { Avatar, Breadcrumb, Button, Dropdown, Layout, Menu, Tooltip, type MenuProps } from 'antd';
import Icon, { BellOutlined, DownOutlined, MenuFoldOutlined, MenuUnfoldOutlined } from '@ant-design/icons';
import { useEffect, useState, type ReactNode } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { crumbsForPath, defaultOpenKeys, getSelected, navigationItems } from './navigation';
import { BrandMark } from '../components/BrandMark';
import { HeaderBrand } from './HeaderBrand';
import { contentFrameForPath } from './contentFrame';

const { Header, Sider, Content } = Layout;

function AccountGlyph() {
  return <svg width="1em" height="1em" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
    <circle cx="12" cy="7" r="4" />
    <path d="M4 20.5c0-4.2 3.2-7 8-7s8 2.8 8 7Z" />
  </svg>;
}

export function AppShell({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();
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
    { key: 'profile', label: '张三 · 演示账号', disabled: true },
    { key: 'access', label: '账号与权限', onClick: () => navigate('/access') },
    { key: 'login', label: '登录页（规划稿）', onClick: () => navigate('/access/login') },
    { key: 'planned', label: '其他设计页面', children: [
      { key: 'live', label: '实时运行态势', onClick: () => navigate('/operations/live') },
      { key: 'rules', label: '规则管理', onClick: () => navigate('/rules') },
      { key: 'analytics', label: '分析报表', onClick: () => navigate('/analytics') },
      { key: 'backup', label: '备份与恢复', onClick: () => navigate('/governance/backup') },
      { key: 'partition', label: '分区管理', onClick: () => navigate('/governance/sharding') },
      { key: 'settings', label: '系统配置', onClick: () => navigate('/settings') },
    ] },
  ];
  return <Layout className="app-layout shared-shell">
    {mobileOpen && <div className="mobile-scrim" onClick={() => setMobileOpen(false)} />}
    <Sider width={246} collapsedWidth={72} collapsed={siderCollapsed} className={`app-sider ${mobileOpen ? 'mobile-open' : ''}`} trigger={null} theme="light">
      <div className="brand" role="button" aria-label="Zeye Sorting Hub" tabIndex={0} onClick={() => navigate('/overview')} onKeyDown={event => event.key === 'Enter' && navigate('/overview')}>
        {siderCollapsed ? <BrandMark /> : <HeaderBrand />}
      </div>
      <Menu mode="inline" theme="light" items={navigationItems} selectedKeys={[getSelected(location.pathname)]} openKeys={siderCollapsed ? [] : openKeys} onOpenChange={keys => setOpenKeys(keys)} onClick={({ key }) => key.startsWith('/') && navigate(key)} className="side-menu" inlineIndent={20} />
      <Tooltip title={collapsed ? '展开导航' : '收起导航'}><Button className="collapse-button" aria-label={collapsed ? '展开导航' : '收起导航'} type="text" icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />} onClick={() => setCollapsed(!collapsed)} /></Tooltip>
    </Sider>
    <Layout className="body-layout">
      <Header className="app-header">
        <Button className="mobile-menu-button" aria-label="打开导航" type="text" icon={<MenuUnfoldOutlined />} onClick={() => { setOpenKeys(keys => keys.length ? keys : defaultOpenKeys()); setMobileOpen(true); }} />
        <div className="header-spacer" />
        <Tooltip title="本地演示：暂无通知"><Button className="header-icon" aria-label="通知" type="text" icon={<BellOutlined />} /></Tooltip>
        <Dropdown menu={{ items: accountItems }} trigger={['click']}><Button type="text" aria-label="张三，账号菜单" className="account-button"><Avatar shape="circle" size={32} style={{ fontSize: 23, color: '#fff', backgroundColor: '#609bf5' }} icon={<Icon component={AccountGlyph} />} /><span>张三</span><DownOutlined className="account-chevron" /></Button></Dropdown>
      </Header>
      <Content className="app-content">
        <Breadcrumb aria-label="面包屑导航" items={crumbs.map(({ title, href }, index) => ({ title: index === crumbs.length - 1
          ? <span aria-current="page">{title}</span>
          : href ? <Link to={href}>{title}</Link> : <span>{title}</span> }))} className="page-breadcrumb" />
        <div className="page-body" style={contentFrameForPath(location.pathname)}>{children}</div>
      </Content>
    </Layout>
  </Layout>;
}

export function LoginShell({ children }: { children: ReactNode }) {
  return <div className="login-shell"><div className="login-content">{children}</div></div>;
}
