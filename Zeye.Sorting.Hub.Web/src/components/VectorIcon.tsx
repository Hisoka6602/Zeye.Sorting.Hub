import {
  AuditOutlined, BarChartOutlined, CalendarOutlined, ClockCircleOutlined,
  ClearOutlined, CloudServerOutlined, CodeSandboxOutlined, ControlOutlined, DashboardOutlined, DatabaseOutlined,
  DeleteOutlined, ExclamationCircleOutlined, ExperimentOutlined, FileTextOutlined, HeartOutlined,
  HomeOutlined, InboxOutlined, PlusOutlined, ProfileOutlined,
  SafetyCertificateOutlined, SafetyOutlined, SettingOutlined, ThunderboltOutlined, UnorderedListOutlined,
} from '@ant-design/icons';
import type { CSSProperties } from 'react';

const icons = {
  home: HomeOutlined, parcels: ProfileOutlined, governance: DatabaseOutlined,
  observability: SafetyCertificateOutlined, guide: FileTextOutlined,
  rules: ControlOutlined, system: SettingOutlined, backup: CloudServerOutlined,
  operations: DashboardOutlined, analytics: BarChartOutlined,
  create: PlusOutlined, batch: UnorderedListOutlined, testData: ExperimentOutlined, cleanup: DeleteOutlined,
  audit: AuditOutlined, slow: ThunderboltOutlined, archive: InboxOutlined,
  health: HeartOutlined, errors: ExclamationCircleOutlined,
  time: ClockCircleOutlined, calendar: CalendarOutlined,
};

export type VectorIconName = keyof typeof icons;
type IconSurface = 'help' | 'analytics' | 'navigation';

// Navigation glyphs share rounded outlines, corners and stroke ends.
const navigationIcons = {
  analytics: <><path d="M4 4v14a2 2 0 0 0 2 2h14" /><path d="M8 16v-5m4 5V7m4 9V4" /></>,
  home: <>
    <path d="m3 10 7.7-6.6a2 2 0 0 1 2.6 0L21 10M5 9v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9" />
    <path d="M9 21v-6a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v6" />
  </>,
  parcels: <>
    <rect x="4" y="3" width="16" height="18" rx="2.5" />
    <path d="M11 8h5m-5 4h5m-5 4h5M8 8h.01M8 12h.01M8 16h.01" />
  </>,
  testData: <>
    <path d="M8 3h8M10 3v6.5a2 2 0 0 1-.3 1L4.5 19a1.3 1.3 0 0 0 1.1 2h12.8a1.3 1.3 0 0 0 1.1-2l-5.2-8.5a2 2 0 0 1-.3-1V3" />
    <path d="M7 15.5c2 1.5 3.5 1.5 5 0s3-1.5 5 0M12 12h.01" />
  </>,
  governance: <>
    <rect x="4" y="3" width="16" height="18" rx="2.5" />
    <path d="M4 9h16M4 15h16M8 6h.01M8 12h.01M8 18h.01" />
  </>,
  observability: <>
    <path d="M11.2 3.4a1.5 1.5 0 0 1 1.6 0c1.8 1 3.8 1.7 5.8 2.3a1.5 1.5 0 0 1 1.1 1.4V12c0 4-2.5 6.5-7 8.8a1.5 1.5 0 0 1-1.4 0c-4.5-2.3-7-4.8-7-8.8V7.1a1.5 1.5 0 0 1 1.1-1.4c2-.6 4-1.3 5.8-2.3Z" />
    <path d="m8.5 12 2.5 2.5 4.5-5" />
  </>,
  operations: <>
    <path d="M5.6 20a1.5 1.5 0 0 1-1.1-.5 9.5 9.5 0 1 1 15 0 1.5 1.5 0 0 1-1.1.5Z" />
    <path d="M5 12h1m1-5 .7.7M12 5v1m5 1-.7.7M18 12h1m-7 2 3-3" />
    <circle cx="12" cy="14" r=".7" />
  </>,
  rules: <>
    <rect x="3" y="3" width="18" height="18" rx="2.5" />
    <path d="M8 6v5m0 4v3m8-12v2m0 4v6" />
    <circle cx="8" cy="13" r="2" /><circle cx="16" cy="10" r="2" />
  </>,
  system: <>
    <path d="M10.7 3h2.6q.7 0 .9.7l.4 1.6 1.4.8 1.6-.4q.7-.2 1.1.4l1.3 2.2q.4.6-.1 1.1l-1.2 1.2v2.8l1.2 1.2q.5.5.1 1.1l-1.3 2.2q-.4.6-1.1.4l-1.6-.4-1.4.8-.4 1.6q-.2.7-.9.7h-2.6q-.7 0-.9-.7l-.4-1.6-1.4-.8-1.6.4q-.7.2-1.1-.4l-1.3-2.2q-.4-.6.1-1.1l1.2-1.2v-2.8L4.1 9.4q-.5-.5-.1-1.1l1.3-2.2q.4-.6 1.1-.4l1.6.4 1.4-.8.4-1.6q.2-.7.9-.7Z" />
    <circle cx="12" cy="12" r="3" />
  </>,
  guide: <>
    <path d="M13.2 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9.8a2 2 0 0 0-.6-1.4l-4.8-4.8a2 2 0 0 0-1.4-.6Z" />
    <path d="M13 3v5a2 2 0 0 0 2 2h5M8 14h8m-8 3h5" />
  </>,
};

const surfaceIcons: Partial<Record<IconSurface, Partial<typeof icons>>> = {
  help: {
    parcels: CodeSandboxOutlined, batch: FileTextOutlined, cleanup: ClearOutlined,
    audit: SafetyOutlined, slow: BarChartOutlined,
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
  const Icon = (surface && surfaceIcons[surface]?.[name]) || icons[name];
  const roundedGlyph = surface === 'navigation' && name in navigationIcons
    ? navigationIcons[name as keyof typeof navigationIcons] : undefined;
  return <span className={className} aria-hidden="true" style={{
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    width: size, height: size, flex: 'none', color, background,
    borderRadius: borderRadius ?? (background ? '50%' : undefined), ...style,
  }}>{roundedGlyph ? <svg width={glyphSize} height={glyphSize} viewBox="0 0 24 24"
    fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"
    aria-hidden="true" focusable="false">{roundedGlyph}</svg>
    : <Icon style={{ fontSize: glyphSize, lineHeight: 1 }} />}</span>;
}
