import { Button, Result, Spin } from 'antd';
import { lazy, Suspense } from 'react';
import { Navigate, Outlet, Route, Routes, useNavigate } from 'react-router';
import { AppShell, LoginShell } from './Shell';

const OverviewPage = lazy(() => import('../features/parcels/OverviewPage').then(module => ({ default: module.OverviewPage })));
const DataOverviewPage = lazy(() => import('../features/parcels/DataOverviewPage').then(module => ({ default: module.DataOverviewPage })));
const ParcelListPage = lazy(() => import('../features/parcels/ParcelListPage').then(module => ({ default: module.ParcelListPage })));
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

function RouteLoading() { return <div className="route-loading" role="status" aria-label="页面加载中"><Spin size="large" /></div>; }
function MainLayout() { return <AppShell><Suspense fallback={<RouteLoading />}><Outlet /></Suspense></AppShell>; }
function LoginLayout() { return <LoginShell><Suspense fallback={<RouteLoading />}><Outlet /></Suspense></LoginShell>; }
function NotFound() { const navigate = useNavigate(); return <Result status="404" title="页面不存在" subTitle="请从左侧导航选择页面。" extra={<Button type="primary" onClick={() => navigate('/data-overview')}>返回数据概览</Button>} />; }

export default function App() {
  return <Routes>
    <Route path="/" element={<Navigate to="/data-overview" replace />} />
    <Route element={<LoginLayout />}><Route path="/access/login" element={<LoginPage />} /></Route>
    <Route element={<MainLayout />}>
      <Route path="/data-overview" element={<DataOverviewPage />} />
      <Route path="/overview" element={<OverviewPage />} />
      <Route path="/parcels" element={<ParcelListPage />} />
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
