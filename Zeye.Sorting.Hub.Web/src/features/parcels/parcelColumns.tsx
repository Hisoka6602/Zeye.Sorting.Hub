import { formatNumber } from '../../data/formatNumber';
import { Button, type TableColumnsType } from 'antd';
import { StatusTag } from '../../components/StatusTag';
import type { Parcel } from '../../data/mock/parcels';
import { localTime } from '../../data/api/operationalTypes';

export const parcelColumns = (navigate: (path: string) => void): TableColumnsType<Parcel> => [
  { title: '扫码时间', dataIndex: 'scanTime', width: 215, render: localTime, sorter: (a, b) => a.scanTime.localeCompare(b.scanTime) },
  { title: '包裹 ID', dataIndex: 'id', width: 120 },
  { title: '主条码', dataIndex: 'barcode', width: 170 },
  { title: '状态', dataIndex: 'status', width: 110, render: (value: string) => <StatusTag value={value} /> },
  { title: '目标 / 实际格口', width: 162, render: (_, record) => `${record.target} / ${record.actual}` },
  { title: '工作台', dataIndex: 'workstation', width: 108 },
  { title: '重量', dataIndex: 'weight', width: 100, render: (value: number) => `${formatNumber(value)} kg` },
  { title: '操作', key: 'actions', width: 85, render: (_, record) => <Button className="table-link" type="link" onClick={() => navigate(`/parcels/${record.id}`)}>查看</Button> },
];
