import { Button, Result, Spin } from 'antd';
import { lazy, Suspense } from 'react';
import { Navigate, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router';
import { AppShell, LoginShell } from './Shell';
import { PageErrorBoundary } from './PageErrorBoundary';

const OverviewPage = lazy(() => import('../features/parcels/OverviewPage').then(module => ({ default: module.OverviewPage })));
const DataOverviewPage = lazy(() => import('../features/parcels/DataOverviewPage').then(module => ({ default: module.DataOverviewPage })));
const ParcelListPage = lazy(() => import('../features/parcels/ParcelListPage').then(module => ({ default: module.ParcelListPage })));
const ParcelTimingPage = lazy(() => import('../features/parcels/ParcelTimingPage').then(module => ({ default: module.ParcelTimingPage })));
const ParcelComparisonPage = lazy(() => import('../features/parcels/ParcelComparisonPage').then(module => ({ default: module.ParcelComparisonPage })));
const ParcelExceptionsPage = lazy(() => import('../features/parcels/ParcelAnalysisPage').then(module => ({ default: module.ParcelExceptionsPage })));
const ParcelDurationPage = lazy(() => import('../features/parcels/ParcelAnalysisPage').then(module => ({ default: module.ParcelDurationPage })));
const ParcelChutesPage = lazy(() => import('../features/parcels/ParcelAnalysisPage').then(module => ({ default: module.ParcelChutesPage })));
const ParcelDwsConsistencyPage = lazy(() => import('../features/parcels/ParcelDwsConsistencyPage').then(module => ({ default: module.ParcelDwsConsistencyPage })));
const ParcelDetailPage = lazy(() => import('../features/parcels/ParcelDetailPage').then(module => ({ default: module.ParcelDetailPage })));
const ParcelCreatePage = lazy(() => import('../features/parcels/ParcelCreatePage').then(module => ({ default: module.ParcelCreatePage })));
const ParcelDetectionPage = lazy(() => import('../features/parcels/ParcelDetectionPage').then(module => ({ default: module.ParcelDetectionPage })));
const ParcelBatchPage = lazy(() => import('../features/parcels/ParcelBatchPage').then(module => ({ default: module.ParcelBatchPage })));
const ParcelCleanupPage = lazy(() => import('../features/parcels/ParcelCleanupPage').then(module => ({ default: module.ParcelCleanupPage })));
const AuditListPage = lazy(() => import('../features/observability/AuditListPage').then(module => ({ default: module.AuditListPage })));
const AuditDetailPage = lazy(() => import('../features/observability/AuditDetailPage').then(module => ({ default: module.AuditDetailPage })));
const SlowQueryListPage = lazy(() => import('../features/observability/SlowQueryListPage').then(module => ({ default: module.SlowQueryListPage })));
const SlowQueryDetailPage = lazy(() => import('../features/observability/SlowQueryDetailPage').then(module => ({ default: module.SlowQueryDetailPage })));
const HealthPage = lazy(() => import('../features/observability/HealthPage').then(module => ({ default: module.HealthPage })));
const ArchiveTasksPage = lazy(() => import('../features/governance/ArchiveTasksPage').then(module => ({ default: module.ArchiveTasksPage })));
const BackupPage = lazy(() => import('../features/plannedGovernance/BackupPage').then(module => ({ default: module.BackupPage })));
const PartitionPage = lazy(() => import('../features/plannedGovernance/PartitionPage').then(module => ({ default: module.PartitionPage })));
const LiveOperationsPage = lazy(() => import('../features/operations/LiveOperationsRealPage').then(module => ({ default: module.LiveOperationsPage })));
const RulesPage = lazy(() => import('../features/operations/RulesPage').then(module => ({ default: module.RulesPage })));
const AnalyticsPage = lazy(() => import('../features/operations/AnalyticsPage').then(module => ({ default: module.AnalyticsPage })));
const AccessPage = lazy(() => import('../features/access/AccessPage').then(module => ({ default: module.AccessPage })));
const ProfilePage = lazy(() => import('../features/access/ProfilePage').then(module => ({ default: module.ProfilePage })));
const LoginPage = lazy(() => import('../features/access/LoginPage').then(module => ({ default: module.LoginPage })));
const SettingsPage = lazy(() => import('../features/access/SettingsPage').then(module => ({ default: module.SettingsPage })));
const FusionSettingsPage = lazy(() => import('../features/access/FusionSettingsPage').then(module => ({ default: module.FusionSettingsPage })));
const HelpPage = lazy(() => import('../features/help').then(module => ({ default: module.HelpPage })));

/** 页面模块等待期间显示加载状态。 */
function RouteLoading() { return <div className="route-loading" role="status" aria-label="页面加载中"><Spin size="large" /></div>; }
/** 所有页面共用加载和错误边界，失败不会移除外壳，导航后允许渲染新的页面。 */
function RouteContent() { const location = useLocation(); return <PageErrorBoundary resetKey={location.pathname}><Suspense fallback={<RouteLoading />}><Outlet /></Suspense></PageErrorBoundary>; }
/** 业务页面的外壳与可恢复路由内容。 */
function MainLayout() { return <AppShell><RouteContent /></AppShell>; }
/** 登录页面使用相同的路由恢复能力。 */
function LoginLayout() { return <LoginShell><RouteContent /></LoginShell>; }
/** 未登记的页面提供现有导航入口。 */
function NotFound() { const navigate = useNavigate(); return <Result status="404" title="页面不存在" subTitle="请从左侧导航选择页面。" extra={<Button type="primary" onClick={() => navigate('/data-overview')}>返回数据概览</Button>} />; }

/** 保留页面按需加载和现有访问控制。 */
export default function App() {
  return <Routes>
    <Route path="/" element={<Navigate to="/data-overview" replace />} />
    <Route element={<LoginLayout />}><Route path="/access/login" element={<LoginPage />} /></Route>
    <Route element={<MainLayout />}>
      <Route path="/data-overview" element={<DataOverviewPage />} />
      <Route path="/overview" element={<OverviewPage />} />
      <Route path="/parcels" element={<ParcelListPage />} />
      <Route path="/parcels/statistics" element={<AnalyticsPage parcelCenter />} />
      <Route path="/parcels/exceptions" element={<ParcelExceptionsPage />} />
      <Route path="/parcels/duration" element={<ParcelDurationPage />} />
      <Route path="/parcels/chutes" element={<ParcelChutesPage />} />
      <Route path="/parcels/dws-consistency" element={<ParcelDwsConsistencyPage />} />
      <Route path="/parcels/timing" element={<ParcelTimingPage />} />
      <Route path="/parcels/compare" element={<ParcelComparisonPage />} />
      <Route path="/parcels/new" element={<ParcelCreatePage />} />
      <Route path="/parcels/detection/new" element={<ParcelDetectionPage />} />
      <Route path="/parcels/batch" element={<ParcelBatchPage />} />
      <Route path="/parcels/:id" element={<ParcelDetailPage />} />
      <Route path="/governance/parcel-cleanup" element={<ParcelCleanupPage />} />
      <Route path="/audit/requests" element={<AuditListPage />} />
      <Route path="/audit/requests/:id" element={<AuditDetailPage />} />
      <Route path="/diagnostics/slow-queries" element={<SlowQueryListPage />} />
      <Route path="/diagnostics/slow-queries/:fingerprint" element={<SlowQueryDetailPage />} />
      <Route path="/governance/archive-tasks" element={<ArchiveTasksPage />} />
      <Route path="/diagnostics/health" element={<HealthPage />} />
      <Route path="/help" element={<HelpPage />} />
      <Route path="/access" element={<AccessPage />} />
      <Route path="/profile" element={<ProfilePage />} />
      <Route path="/operations/live" element={<LiveOperationsPage />} />
      <Route path="/rules" element={<RulesPage />} />
      <Route path="/analytics" element={<AnalyticsPage />} />
      <Route path="/governance/backup" element={<BackupPage />} />
      <Route path="/governance/sharding" element={<PartitionPage />} />
      <Route path="/settings" element={<SettingsPage />} />
      <Route path="/settings/fusion" element={<FusionSettingsPage />} />
      <Route path="*" element={<NotFound />} />
    </Route>
  </Routes>;
}
