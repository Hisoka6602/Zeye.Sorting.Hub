import {
  AuditOutlined, BarChartOutlined, CalendarOutlined, ClockCircleOutlined,
  ClearOutlined, CloudServerOutlined, CodeSandboxOutlined, ControlOutlined, DashboardOutlined, DatabaseOutlined,
  DeleteOutlined, ExclamationCircleOutlined, FileTextOutlined, HeartOutlined,
  HomeOutlined, InboxOutlined, MailOutlined, PlusOutlined, ProfileOutlined,
  SafetyCertificateOutlined, SafetyOutlined, SettingOutlined, StockOutlined, ThunderboltOutlined, UnorderedListOutlined,
} from '@ant-design/icons';
import type { CSSProperties } from 'react';

const icons = {
  home: HomeOutlined, parcels: ProfileOutlined, governance: DatabaseOutlined,
  observability: SafetyCertificateOutlined, guide: FileTextOutlined,
  rules: ControlOutlined, system: SettingOutlined, backup: CloudServerOutlined,
  operations: DashboardOutlined, analytics: BarChartOutlined,
  create: PlusOutlined, batch: UnorderedListOutlined, cleanup: DeleteOutlined,
  audit: AuditOutlined, slow: ThunderboltOutlined, archive: InboxOutlined,
  outbox: MailOutlined, health: HeartOutlined, errors: ExclamationCircleOutlined,
  time: ClockCircleOutlined, calendar: CalendarOutlined,
};

export type VectorIconName = keyof typeof icons;
type IconSurface = 'help' | 'analytics';

const surfaceIcons: Record<IconSurface, Partial<typeof icons>> = {
  help: {
    parcels: CodeSandboxOutlined, batch: FileTextOutlined, cleanup: ClearOutlined,
    audit: SafetyOutlined, slow: BarChartOutlined, outbox: StockOutlined,
  },
  analytics: { parcels: CodeSandboxOutlined },
};

export function VectorIcon({ name, surface, className = '', size = 20, glyphSize = size, color = 'currentColor', background, borderRadius, style }: {
  name: VectorIconName;
  surface?: IconSurface;
  className?: string;
  size?: number;
  glyphSize?: number;
  color?: string;
  background?: string;
  borderRadius?: CSSProperties['borderRadius'];
  style?: CSSProperties;
}) {
  const Icon = (surface && surfaceIcons[surface][name]) || icons[name];
  return <span className={className} aria-hidden="true" style={{
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    width: size, height: size, flex: 'none', color, background,
    borderRadius: borderRadius ?? (background ? '50%' : undefined), ...style,
  }}><Icon style={{ fontSize: glyphSize, lineHeight: 1 }} /></span>;
}
