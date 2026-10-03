export interface LiveEvent { id: number; time: string; line: string; device: string; type: string; parcel: string; status: '待处理' | '已处理'; description: string }
export const initialLiveEvents: LiveEvent[] = [
  { id: 1, time: '2026-09-25 16:28:40', line: '产线 1', device: 'SC-101', type: '设备恢复', parcel: '-', status: '已处理', description: '设备恢复连接，运行状态正常。' },
  { id: 2, time: '2026-09-25 16:27:31', line: '产线 2', device: 'DV-203', type: '包裹卡滞', parcel: 'YT9988776655CN', status: '待处理', description: '设备检测到包裹在分拣口位置卡滞，触发告警。请及时处理并确认设备状态。' },
  { id: 3, time: '2026-09-25 16:26:18', line: '产线 3', device: 'SC-305', type: '设备离线', parcel: '-', status: '待处理', description: '设备连接中断，请检查网络与设备供电。' },
  { id: 4, time: '2026-09-25 16:24:05', line: '产线 1', device: 'XR-102', type: '识别异常', parcel: 'SF3124567890CN', status: '已处理', description: '条码识别结果置信度较低，已经人工核实。' },
  { id: 5, time: '2026-09-25 16:21:33', line: '产线 3', device: 'DV-308', type: '包裹异常', parcel: 'JD7331982746CN', status: '已处理', description: '包裹尺寸超出当前分拣口规格。' },
  { id: 6, time: '2026-09-25 16:19:17', line: '产线 2', device: 'SC-207', type: '设备重启', parcel: '-', status: '已处理', description: '设备维护重启完成。' },
  { id: 7, time: '2026-09-25 16:18:02', line: '产线 1', device: 'DV-108', type: '包裹卡滞', parcel: 'ZTO5566778899CN', status: '待处理', description: '分拣口包裹卡滞，请检查传送带。' },
  { id: 8, time: '2026-09-25 16:15:49', line: '产线 2', device: 'SC-201', type: '传感器异常', parcel: '-', status: '已处理', description: '传感器信号异常，已恢复。' },
  { id: 9, time: '2026-09-25 16:13:26', line: '产线 3', device: 'XR-309', type: '设备恢复', parcel: '-', status: '已处理', description: '设备已恢复正常工作。' },
  { id: 10, time: '2026-09-25 16:10:11', line: '产线 1', device: 'DV-101', type: '包裹异常', parcel: 'SF0987654321CN', status: '已处理', description: '包裹流向已人工修正。' },
];

export interface RuleCondition {
  field: string;
  operator: string;
  value?: string;
  values?: string[];
  unit?: string;
}
export interface Rule {
  id: number;
  name: string;
  version: string;
  status: '已发布' | '草稿';
  scope: string;
  modified: string;
  editor: string;
  description: string;
  note?: string;
  // Optional so existing parcel-rule drafts remain readable.
  targetType?: string;
  exceptionType?: number;
  parcelType?: number;
  matchMode?: 'all' | 'any';
  conditions?: RuleCondition[];
  actions?: string[];
  systemRule?: 'sorter-protocol' | 'unknown-fallback';
}
const parcelRuleSeeds: Rule[] = [
  { id: 1, name: '大件包裹分流规则', version: 'v1.2.0', status: '已发布', scope: 'B01-06', modified: '2026-09-25 14:23:12', editor: '张三', description: '用于识别大件包裹并分流至大件专用分拣口。', note: '识别重量或体积超阈值的包裹。' },
  { id: 2, name: '易碎品识别规则', version: 'v0.3.1', status: '草稿', scope: 'A01-01 / A01-02', modified: '2026-09-24 18:11:03', editor: '李四', description: '识别易碎品标记并优先处理。' },
  { id: 3, name: '目的地城市分拣规则', version: 'v2.0.0', status: '已发布', scope: '全国', modified: '2026-09-23 10:05:21', editor: '王五', description: '按目的地城市分拣。' },
  { id: 4, name: '超尺寸包裹规则', version: 'v0.1.0', status: '草稿', scope: 'C03-12', modified: '2026-09-22 16:44:18', editor: '赵六', description: '识别超尺寸包裹。' },
  { id: 5, name: '生鲜包裹优先规则', version: 'v1.0.1', status: '已发布', scope: 'B02-03 / B02-04', modified: '2026-09-21 11:20:36', editor: '周七', description: '生鲜包裹优先转运。' },
  { id: 6, name: '异常地址拦截规则', version: 'v0.2.0', status: '草稿', scope: '全国', modified: '2026-09-20 09:17:54', editor: '吴八', description: '拦截缺失或异常收件地址。' },
  { id: 7, name: '轻小件合单规则', version: 'v1.1.0', status: '已发布', scope: 'A02-07', modified: '2026-09-19 15:36:27', editor: '郑九', description: '合并同目的地轻小件。' },
  { id: 8, name: '高价值包裹复核规则', version: 'v0.5.0', status: '草稿', scope: 'D04-01', modified: '2026-09-18 13:02:11', editor: '孙十', description: '高价值包裹人工复核。' },
  { id: 9, name: '禁运品拦截规则', version: 'v1.3.0', status: '已发布', scope: '全国', modified: '2026-09-17 17:28:09', editor: '张三', description: '拦截禁运物品。' },
  { id: 10, name: '到付包裹标记规则', version: 'v0.4.0', status: '草稿', scope: 'B01-06', modified: '2026-09-16 10:11:45', editor: '李四', description: '标记到付包裹。' },
];

const parcelRuleDefinitions: Record<number, Pick<Rule, 'targetType' | 'matchMode' | 'conditions' | 'actions'>> = {
  1: { targetType: '大件包裹', matchMode: 'any', conditions: [{ field: '包裹重量', operator: '大于', value: '20 kg' }, { field: '包裹体积（长×宽×高）', operator: '大于', value: '100000 cm³' }, { field: '包裹类型', operator: '包含', value: '大件' }], actions: ['分流至专用分拣口'] },
  2: { targetType: '易碎品', conditions: [{ field: '包裹类型', operator: '包含', value: '易碎' }], actions: ['标记包裹类型', '转人工复核'] },
  3: { targetType: '目的地包裹', conditions: [{ field: '目的地城市', operator: '不为空' }], actions: ['分流至专用分拣口'] },
  4: { targetType: '超尺寸包裹', conditions: [{ field: '包裹长度', operator: '大于', value: '100 cm' }], actions: ['转人工复核'] },
  5: { targetType: '生鲜包裹', conditions: [{ field: '包裹类型', operator: '包含', value: '生鲜' }], actions: ['优先分拣'] },
  6: { targetType: '异常地址包裹', conditions: [{ field: '收件地址', operator: '为空' }], actions: ['拦截包裹', '转人工复核'] },
  7: { targetType: '轻小件', matchMode: 'all', conditions: [{ field: '包裹重量', operator: '小于', value: '1 kg' }, { field: '包裹体积（长×宽×高）', operator: '小于', value: '1000 cm³' }], actions: ['标记包裹类型'] },
  8: { targetType: '高价值包裹', conditions: [{ field: '申报价值', operator: '大于', value: '5000 元' }], actions: ['转人工复核'] },
  9: { targetType: '禁运品', conditions: [{ field: '包裹类型', operator: '包含', value: '禁运品' }], actions: ['拦截包裹'] },
  10: { targetType: '到付包裹', conditions: [{ field: '付款方式', operator: '等于', value: '到付' }], actions: ['标记包裹类型'] },
};
export const initialRules = parcelRuleSeeds.map(rule => ({ ...rule, ...parcelRuleDefinitions[rule.id] }));

// Codes and labels follow Domain.Enums.ParcelExceptionType.
export const exceptionTypeOptions = [
  { value: 0, label: '未知异常' },
  { value: 1, label: '接口响应异常' }, { value: 2, label: '等待DWS数据超时' },
  { value: 3, label: '等待目标格口超时' }, { value: 4, label: '无效目标格口' },
  { value: 5, label: '速度不匹配' }, { value: 6, label: '锁格' },
  { value: 7, label: '叠包' }, { value: 8, label: '灰度仪响应异常' },
  { value: 9, label: '位置检测异常' }, { value: 10, label: '包裹丢失' },
  { value: 11, label: '机械故障' }, { value: 12, label: '飘格' },
  { value: 13, label: '包裹间距违规' }, { value: 14, label: '目标格口分配被拒绝' },
  { value: 15, label: '来源设备异常' },
];

// Retained only to migrate untouched examples without removing user-edited drafts.
export const legacyExceptionRules: Rule[] = [
  { id: 101, name: 'DWS数据超时分类规则', targetType: '等待DWS数据超时', exceptionType: 2, version: 'v1.0.0', status: '已发布', scope: '全部产线', modified: '2026-09-25 14:20:00', editor: '张三', description: '识别等待DWS数据超时的来源异常，统一归入数据超时类型。', matchMode: 'any', conditions: [{ field: '来源异常代码', operator: '等于', value: 'DWS_TIMEOUT' }, { field: '异常信息', operator: '包含', value: '等待DWS数据超时' }], actions: ['标记分拣异常', '分流至异常处理口'] },
  { id: 102, name: '目标格口超时分类规则', targetType: '等待目标格口超时', exceptionType: 3, version: 'v0.2.0', status: '草稿', scope: '全部产线', modified: '2026-09-24 15:18:00', editor: '李四', description: '将目标格口等待超时的处理信息归入统一异常类型。', matchMode: 'any', conditions: [{ field: '来源异常代码', operator: '等于', value: 'TARGET_CHUTE_TIMEOUT' }, { field: '异常信息', operator: '包含', value: '等待目标格口超时' }], actions: ['标记分拣异常', '通知人工复核'] },
  { id: 103, name: '无效目标格口分类规则', targetType: '无效目标格口', exceptionType: 4, version: 'v1.1.0', status: '已发布', scope: '全部产线', modified: '2026-09-23 10:05:00', editor: '王五', description: '识别不存在或不可用的目标格口，归入无效目标格口类型。', conditions: [{ field: '来源异常代码', operator: '等于', value: 'INVALID_TARGET_CHUTE' }], actions: ['标记分拣异常', '分流至异常处理口'] },
  { id: 104, name: '锁格异常分类规则', targetType: '锁格', exceptionType: 6, version: 'v0.3.0', status: '草稿', scope: '产线 1 / 产线 2', modified: '2026-09-22 16:44:00', editor: '赵六', description: '将格口锁定造成的分拣失败归入锁格异常。', conditions: [{ field: '来源异常代码', operator: '等于', value: 'CHUTE_LOCKED' }], actions: ['标记分拣异常', '通知人工复核'] },
  { id: 105, name: '机械故障分类规则', targetType: '机械故障', exceptionType: 11, version: 'v1.0.0', status: '已发布', scope: '全部产线', modified: '2026-09-21 11:20:00', editor: '周七', description: '识别分拣设备的机械故障信息并归入机械故障类型。', conditions: [{ field: '来源异常代码', operator: '等于', value: 'MECHANICAL_FAILURE' }], actions: ['标记分拣异常', '发送异常告警'] },
  { id: 106, name: '来源设备异常分类规则', targetType: '来源设备异常', exceptionType: 15, version: 'v0.1.0', status: '草稿', scope: '全部产线', modified: '2026-09-20 09:17:00', editor: '吴八', description: '对其他来源设备异常建立映射，保留原始来源异常代码以便追踪。', conditions: [{ field: '来源异常代码', operator: '包含', value: 'DEVICE_' }], actions: ['标记分拣异常', '记录异常原因'] },
];

// Confirmed by SortingFusionService's SorterEventParser/ParcelSessionStore and protocol tests.
export const sorterExceptionDefinitions = [
  { code: 'ParcelSpacingViolation', type: 13, label: '包裹间距违规', description: '分拣机检测到包裹间距小于允许的最小间隔。' },
  { code: 'TargetChuteAssignmentRejected', type: 14, label: '目标格口分配被拒绝', description: '分拣机拒绝当前包裹的目标格口分配。' },
  { code: 'RoutingTimeout', type: 3, label: '等待目标格口超时', description: '分拣机等待路由或目标格口决策超时。' },
] as const;

export const unknownExceptionRule: Rule = {
  id: -100, name: '未知异常', targetType: '未知异常', exceptionType: 0,
  version: 'v1.0.0', status: '已发布', scope: '全部产线', modified: '—', editor: '系统',
  description: '所有已发布异常分类规则均未匹配时，统一归类到未知异常，并保留来源异常代码和原始报文。',
  systemRule: 'unknown-fallback', conditions: [], actions: ['标记分拣异常', '记录异常原因'],
};

export const initialExceptionRules: Rule[] = [
  ...sorterExceptionDefinitions.map((definition, index): Rule => ({
    id: -101 - index, name: `${definition.label}分类规则`, targetType: definition.label, exceptionType: definition.type,
    version: 'v1.0.0', status: '已发布', scope: '全部产线', modified: '—', editor: '系统',
    description: definition.description, note: `来源：Fusion 分拣机 ParcelException 报文，ExceptionType=${definition.code}。`,
    systemRule: 'sorter-protocol', matchMode: 'all',
    conditions: [{ field: '来源异常代码', operator: '等于', value: definition.code }],
    actions: ['标记分拣异常', '记录异常原因'],
  })),
  unknownExceptionRule,
];
