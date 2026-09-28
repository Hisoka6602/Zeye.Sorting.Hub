export type ParcelStatus = '已完成' | '待分拣' | '分拣异常';

export interface Parcel {
  id: number;
  barcode: string;
  status: ParcelStatus;
  scanTime: string;
  landedTime?: string;
  target: string;
  actual: string;
  workstation: string;
  weight: number;
  bag: string;
  kind: string;
  length: number;
  width: number;
  height: number;
  requestStatus: string;
}

export const initialParcels: Parcel[] = [
  { id: 2509250001, barcode: 'SF3124567890CN', status: '已完成', scanTime: '2026-09-25 08:12:17', landedTime: '2026-09-25 08:19:28', target: 'A01-01', actual: 'A01-01', workstation: '工作台 1', weight: 1.2, bag: 'A01-01', kind: '标准包裹', length: 320, width: 240, height: 180, requestStatus: '成功' },
  { id: 2509250002, barcode: 'JD7331982746CN', status: '待分拣', scanTime: '2026-09-25 09:37:54', target: 'B02-03', actual: '-', workstation: '工作台 2', weight: 0.56, bag: 'B02-03', kind: '标准包裹', length: 210, width: 160, height: 120, requestStatus: '待发送' },
  { id: 2509250003, barcode: 'YT9988776655CN', status: '已完成', scanTime: '2026-09-25 10:05:26', landedTime: '2026-09-25 10:12:06', target: 'C03-12', actual: 'C03-12', workstation: '工作台 1', weight: 2.35, bag: 'C03-12', kind: '标准包裹', length: 450, width: 300, height: 230, requestStatus: '成功' },
  { id: 2509250004, barcode: 'ZTO5566778899CN', status: '分拣异常', scanTime: '2026-09-25 11:22:11', target: 'A02-07', actual: '-', workstation: '工作台 3', weight: 0.8, bag: 'A02-07', kind: '标准包裹', length: 260, width: 200, height: 130, requestStatus: '失败' },
  { id: 2509250005, barcode: 'SF0987654321CN', status: '待分拣', scanTime: '2026-09-25 13:48:37', target: 'D04-01', actual: '-', workstation: '工作台 2', weight: 1.05, bag: 'D04-01', kind: '标准包裹', length: 320, width: 240, height: 180, requestStatus: '待发送' },
  { id: 2509250006, barcode: 'JD1122334455CN', status: '已完成', scanTime: '2026-09-25 15:16:09', landedTime: '2026-09-25 15:24:48', target: 'B01-06', actual: 'B01-06', workstation: '工作台 1', weight: 3.6, bag: 'B01-06', kind: '大件', length: 560, width: 420, height: 350, requestStatus: '成功' },
  { id: 2509250007, barcode: 'YT6677889900CN', status: '待分拣', scanTime: '2026-09-25 16:27:33', target: 'C02-11', actual: '-', workstation: '工作台 3', weight: 0.44, bag: 'C02-11', kind: '标准包裹', length: 190, width: 140, height: 110, requestStatus: '待发送' },
];
