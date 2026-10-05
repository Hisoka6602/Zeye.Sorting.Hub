/** 来源实例优先于工作台名称；编号会话不代表新的工作台。 */
export interface WorkbenchParcel {
  sourceInstanceId: string | null;
  workstationName: string;
  status: number;
  createdTime: string;
}

export interface WorkstationObservation {
  key: string;
  name: string;
  sourceInstanceId: string | null;
  parcelCount: number;
  pendingCount: number;
  completedCount: number;
  exceptionCount: number;
  otherCount: number;
  lastParcelAt: string | null;
  presence?: FusionSourcePresence;
}

/** 来自经过机器认证的 Fusion 心跳，不包含凭据或推测的设备连接状态。 */
export interface FusionSourcePresence {
  sourceInstanceId: string;
  workstationName: string;
  lineId: string;
  siteCode: string | null;
  deviceCode: string | null;
  journalId: string | null;
  isOnline: boolean;
  lastSeenAt: string | null;
  pendingFacts: number;
  rejectedFacts: number;
  pendingImages: number;
  droppedUnacknowledgedFacts: number;
  droppedUnacknowledgedImages: number;
}

/** 将登记工作台与观察窗口的包裹统计合并，无包裹的登记来源仍展示。 */
export function mergeWorkstationSources(observations: ReturnType<typeof observeWorkstations>, sources: readonly FusionSourcePresence[]) {
  const workstations = new Map(observations.workstations.map(station => [station.key, station]));
  for (const source of sources) {
    const key = JSON.stringify(['instance', source.sourceInstanceId]);
    const station = workstations.get(key) ?? { key, name: source.workstationName, sourceInstanceId: source.sourceInstanceId,
      parcelCount: 0, pendingCount: 0, completedCount: 0, exceptionCount: 0, otherCount: 0, lastParcelAt: null };
    workstations.set(key, { ...station, name: source.workstationName || station.name, presence: source });
  }
  return { ...observations, workstations: [...workstations.values()] };
}

export function workstationKey(parcel: WorkbenchParcel): string | null {
  const instance = parcel.sourceInstanceId?.trim();
  const name = parcel.workstationName?.trim();
  return instance ? JSON.stringify(['instance', instance]) : name ? JSON.stringify(['name', name]) : null;
}

function timestamp(value: string | null): number {
  const time = value ? Date.parse(value.replace(' ', 'T')) : NaN;
  return Number.isFinite(time) ? time : -Infinity;
}

/** 只汇总当前查询窗口中的处理事实，不将收包时间解释为在线心跳。 */
export function observeWorkstations(parcels: readonly WorkbenchParcel[]) {
  const groups = new Map<string, WorkstationObservation>();
  let unassignedCount = 0;
  for (const parcel of parcels) {
    const key = workstationKey(parcel);
    if (!key) { unassignedCount++; continue; }
    const name = parcel.workstationName?.trim();
    let group = groups.get(key);
    if (!group) {
      group = { key, name: name || '未命名工作台', sourceInstanceId: parcel.sourceInstanceId?.trim() || null,
        parcelCount: 0, pendingCount: 0, completedCount: 0, exceptionCount: 0, otherCount: 0, lastParcelAt: null };
      groups.set(key, group);
    }
    group.parcelCount++;
    if (parcel.status === 0) group.pendingCount++;
    else if (parcel.status === 1) group.completedCount++;
    else if (parcel.status === 2) group.exceptionCount++;
    else group.otherCount++;
    if (timestamp(parcel.createdTime) > timestamp(group.lastParcelAt)) {
      group.lastParcelAt = parcel.createdTime;
      if (name) group.name = name;
    }
  }
  return { workstations: [...groups.values()].sort((a, b) => {
    const first = timestamp(a.lastParcelAt), second = timestamp(b.lastParcelAt);
    return first !== second ? first > second ? -1 : 1 : a.name.localeCompare(b.name, 'zh-CN') || a.key.localeCompare(b.key);
  }), unassignedCount };
}
