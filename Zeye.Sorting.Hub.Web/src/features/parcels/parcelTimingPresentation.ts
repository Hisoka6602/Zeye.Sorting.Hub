import { timingInterval, timingPosition, type TimingMark, type TimingNode, type TimingRow, type TimingScale } from './parcelTimingModel.ts';

export interface TimingOverview {
  detected: TimingMark | null;
  scan: TimingMark | null;
  request: TimingMark | null;
  chute: TimingMark | null;
  landing: TimingMark | null;
  scanToRequest: number | null;
  milestones: TimingMark[];
  diagnosticCount: number;
  hasIssue: boolean;
}
export interface TimingMilestone {
  mark: TimingMark;
  startX: number;
  endX: number | null;
  label: string;
  labelX: number;
  labelLane: number | null;
}
export interface TimingDiagnosticGroup {
  marks: TimingMark[];
  x: number;
  endX: number;
  issue: boolean;
}

export interface TimingOverviewSpan {
  kind: 'detection' | 'wait' | 'chute-stage';
  start: TimingMark;
  end: TimingMark;
  label: string;
  partial: boolean;
}

/** 概览只提取已记录的关键节点；首次请求包含失败尝试，不用成功重试覆盖等待时间。 */
export function timingOverview(row: TimingRow): TimingOverview {
  const first = (title: string) => row.marks.find(mark => mark.title === title) ?? null;
  const detected = first('分拣机检测');
  const scan = first('扫码');
  const request = row.marks.find(mark => mark.title === '请求格口' && (mark.startLabel === '请求' || mark.startLabel === '开始')) ?? null;
  const chute = request ?? first('格口分配') ?? first('请求格口');
  const landing = first('实际落格') ?? first('处理完成');
  const report = row.marks.filter(mark => mark.title === '落格回传' || mark.title === '落格报告').at(-1) ?? null;
  const milestones = [detected, scan, chute, first('分拣指令'), landing, report]
    .filter((mark): mark is TimingMark => mark !== null);
  return {
    detected, scan, request, chute, landing, milestones,
    scanToRequest: scan && request ? timingInterval(scan.start, request.start) : null,
    diagnosticCount: row.marks.filter(mark => mark.issue || mark.warning).length,
    hasIssue: row.marks.some(mark => mark.issue),
  };
}

/** 色带只连接已记录端点；缺少请求起止时区分格口阶段，不冒充接口调用窗口。 */
export function timingOverviewSpans(row: TimingRow): TimingOverviewSpan[] {
  const overview = timingOverview(row);
  const dispatch = row.marks.find(mark => mark.title === '分拣指令') ?? null;
  const end = overview.chute ?? dispatch;
  const spans: TimingOverviewSpan[] = [];
  if (overview.detected && overview.scan) spans.push({
    kind: 'detection', start: overview.detected, end: overview.scan,
    label: '首次检测 → 扫码', partial: false,
  });
  if (overview.scan && end) spans.push({
    kind: 'wait', start: overview.scan, end,
    label: overview.request ? '扫码 → 首次请求' : end.title === '格口分配' ? '扫码 → 格口分配'
      : end.title === '请求格口' ? end.startLabel === '响应' ? '扫码 → 格口响应' : '扫码 → 格口记录' : '扫码 → 分拣指令',
    partial: overview.request === null,
  });
  if (!overview.request && overview.chute?.title === '格口分配' && dispatch) spans.push({
    kind: 'chute-stage', start: overview.chute, end: dispatch,
    label: '格口分配 → 分拣指令', partial: true,
  });
  return spans;
}

function shortTitle(mark: TimingMark): string {
  if (mark.title === '请求格口' && mark.startLabel === '响应') return '格口响应';
  const titles: Record<string, string> = { '分拣机检测': '首次检测', '扫码': '扫码', '请求格口': '请求格口', '格口分配': '分配格口', '分拣指令': '分拣', '实际落格': '落格', '处理完成': '完成', '落格回传': '回传', '落格报告': '回传' };
  return titles[mark.title] ?? mark.title;
}

/** 标签在上下两层避让，点和窗口不移动；放不下的次要标签在完整动作视图中显示。 */
export function layoutTimingOverview(row: TimingRow, scale: TimingScale, width: number, selected: (TimingNode | null)[]): TimingMilestone[] {
  const overview = timingOverview(row);
  const marks = [...overview.milestones];
  for (const node of selected) {
    if (node?.mark.parcelId === row.parcel.id && !marks.some(mark => mark.key === node.mark.key)) marks.push(node.mark);
  }
  const occupied: { left: number; right: number }[][] = [[], []];
  return marks.map(mark => {
    const startX = timingPosition(mark.start, row, scale) * width;
    const endX = mark.end === null ? null : timingPosition(mark.end, row, scale) * width;
    const label = shortTitle(mark);
    const labelWidth = Math.min(138, label.length * 12 + 12);
    const labelX = Math.max(8 + labelWidth / 2, Math.min(width - 8 - labelWidth / 2, startX));
    const left = labelX - labelWidth / 2;
    const right = labelX + labelWidth / 2;
    const preferred = mark === overview.chute || mark.kind === 'landing' ? 1 : 0;
    const labelLane = [preferred, 1 - preferred].find(lane => occupied[lane].every(item => right + 6 < item.left || left > item.right + 6)) ?? null;
    if (labelLane !== null) occupied[labelLane].push({ left, right });
    return { mark, startX, endX, label, labelX, labelLane };
  });
}

/** 仅在概览中聚合近邻诊断标记，锚在组内第一个真实时间；原始动作及每次尝试全部保留。 */
export function groupTimingDiagnostics(row: TimingRow, scale: TimingScale, width: number): TimingDiagnosticGroup[] {
  const groups: TimingDiagnosticGroup[] = [];
  const marks = row.marks.filter(mark => mark.issue || mark.warning)
    .map(mark => ({ mark, x: timingPosition(mark.start, row, scale) * width }))
    .sort((a, b) => a.x - b.x || a.mark.key.localeCompare(b.mark.key));
  for (const { mark, x } of marks) {
    const previous = groups.at(-1);
    if (previous && x - previous.endX < 28) {
      previous.marks.push(mark);
      previous.endX = x;
      previous.issue ||= mark.issue;
    } else groups.push({ marks: [mark], x, endX: x, issue: mark.issue });
  }
  return groups;
}

/** 1/2/5 间距与容器宽度共同决定刻度，绝对轴对齐本地整刻度，扫码轴始终保留零点。 */
export function timingAxisTicks(scale: TimingScale, width: number): number[] {
  const count = Math.max(2, Math.min(10, Math.floor(width / 125)));
  const raw = (scale.max - scale.min) / count;
  const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
  const ratio = raw / magnitude;
  const step = Math.max(0.0001, (ratio <= 1 ? 1 : ratio <= 2 ? 2 : ratio <= 5 ? 5 : 10) * magnitude);
  const stepTicks = BigInt(Math.max(1, Math.round(step * 10000)));
  const base = scale.axis === 'absolute' ? -Number(scale.origin % stepTicks) / 10000 : 0;
  const first = Math.ceil((scale.min - base) / step) * step + base;
  const ticks: number[] = [];
  for (let index = 0; index < 20; index++) {
    const value = first + index * step;
    if (value > scale.max + step * 0.000001) break;
    ticks.push(value);
  }
  return ticks;
}
