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

export interface Rule { id: number; name: string; version: string; status: '已发布' | '草稿'; scope: string; modified: string; editor: string; description: string; note?: string }
export const initialRules: Rule[] = [
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
