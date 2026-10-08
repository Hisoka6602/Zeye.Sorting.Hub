/** 完整字段按业务归属排序，新增字段仍可通过其他字段查看。 */
const contractGroups = [
  { id: 'identity', title: '来源与身份', description: '包裹标识、来源工作台与外部任务', keys: ['id', 'barCodes', 'workstationName', 'sourceInstanceId', 'sourceRunId', 'sourceParcelId', 'parcelTimestamp', 'taskCode'] },
  { id: 'routing', title: '分拣与路由', description: '分拣状态、格口分配与异常信息', keys: ['status', 'type', 'requestStatus', 'targetChuteCode', 'actualChuteCode', 'targetChuteId', 'actualChuteId', 'isFallbackChuteAssigned', 'isRoutingBlocked', 'isSticking', 'noReadType', 'exceptionType', 'sourceExceptionCode'] },
  { id: 'measurement', title: '重量与尺寸', description: '称重、物理尺寸与体积数据', keys: ['weight', 'volumetricWeightGrams', 'length', 'width', 'height', 'volume'] },
  { id: 'times', title: '时间记录', description: '检测、量测、落格与记录更新时间', keys: ['detectedTime', 'scannedTime', 'measurementTime', 'dischargeTime', 'completedTime', 'lifecycleMilliseconds', 'createdTime', 'modifyTime'] },
  { id: 'attachments', title: '设备与附件', description: '小车、集包、坐标与图像信息', keys: ['sorterCarrierId', 'bagCode', 'segmentCodes', 'coordinate', 'hasImages', 'hasVideos', 'modifyIp'] },
] as const;

/** 业务分组的只读展示模型，不包含包裹值或派生业务状态。 */
export interface ParcelContractFieldGroup {
  readonly id: string;
  readonly title: string;
  readonly description: string;
  readonly keys: string[];
}

/** 每个输入字段只展示一次；未知字段保持输入顺序且不被遗漏。 */
export function groupParcelContractFields(keys: readonly string[]): ParcelContractFieldGroup[] {
  const remaining = new Set(keys);
  const groups: ParcelContractFieldGroup[] = [];
  for (const group of contractGroups) {
    const groupKeys = group.keys.filter(key => remaining.delete(key));
    if (groupKeys.length) groups.push({ ...group, keys: groupKeys });
  }
  if (remaining.size) groups.push({ id: 'other', title: '其他字段', description: '其余包裹信息', keys: [...remaining] });
  return groups;
}
