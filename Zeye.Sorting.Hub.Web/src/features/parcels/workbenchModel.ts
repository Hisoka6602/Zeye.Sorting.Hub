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
