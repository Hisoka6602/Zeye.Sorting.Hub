import { Button, Descriptions, Drawer, Input, Space, Spin, Tag, Typography } from 'antd';
import { useState } from 'react';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { localTime, type PagedResult } from '../../data/api/operationalTypes';
import { cleanupStatusLabel, type ParcelCleanupDetail, type ParcelCleanupRecord, type ParcelCleanupDeletedItem } from '../../data/api/parcelCleanupTypes';
import { useApiResource } from '../../data/api/useApiResource';

export function ParcelCleanupHistory({ revision, selectedId, onSelect }: { revision: number; selectedId: string | null; onSelect: (id: string | null) => void }) {
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(10);
  const [detailPage, setDetailPage] = useState(1);
  const [detailSize, setDetailSize] = useState(10);
  const [search, setSearch] = useState('');
  const history = useApiResource<PagedResult<ParcelCleanupRecord>>(`/api/admin/parcels/cleanup-history?pageNumber=${page}&pageSize=${size}&revision=${revision}`);
  const detail = useApiResource<ParcelCleanupDetail>(selectedId ? `/api/admin/parcels/cleanup-history/${selectedId}?pageNumber=${detailPage}&pageSize=${detailSize}&search=${encodeURIComponent(search)}&revision=${revision}` : null);
  const record = detail.data?.record;
  const status = (item: ParcelCleanupRecord) => <Tag color={item.status === 'failed' ? 'red' : item.status === 'running' ? 'blue' : item.decision === 'execute' && item.status === 'completed' ? 'green' : 'gold'}>{cleanupStatusLabel(item)}</Tag>;
  const openDetail = (id: string) => { setDetailPage(1); setSearch(''); onSelect(id); };
  return <>
    <div className="toolbar-row cleanup-history-toolbar"><span className="text-muted">记录永久保留，可查看操作人、执行结果及已删除包裹清单。</span><Button onClick={history.refresh}>刷新记录</Button></div>
    <ApiFeedback error={history.error} retry={history.refresh} />
    <DataTable<ParcelCleanupRecord> loading={history.loading} dataSource={history.data?.items ?? []} pagination={{ current: page, pageSize: size, total: history.data?.totalCount ?? 0, onChange: (next, nextSize) => { setPage(nextSize !== size ? 1 : next); setSize(nextSize); } }} columns={[
      { title: '操作时间', dataIndex: 'startedAtLocal', width: 180, render: localTime },
      { title: '操作人', width: 180, render: (_, item) => <div>{item.operator.name}<div className="text-muted">{item.operator.account}</div></div> },
      { title: '创建时间早于', dataIndex: 'createdBefore', width: 180, render: localTime },
      { title: '结果', width: 155, render: (_, item) => status(item) },
      { title: '计划处理数', dataIndex: 'plannedCount', width: 110 },
      { title: '实际删除数', dataIndex: 'executedCount', width: 110 },
      { title: '操作', width: 110, render: (_, item) => <Button type="link" onClick={() => openDetail(item.id)}>查看记录</Button> },
    ]} />
    <Drawer title="清理记录详情" width="min(960px, 100vw)" open={selectedId !== null} onClose={() => onSelect(null)} extra={<Button onClick={detail.refresh}>刷新详情</Button>}>
      {detail.loading && <Spin />}
      <ApiFeedback error={detail.error} retry={detail.refresh} />
      {record && <>
        <Space className="cleanup-detail-summary">{status(record)}<span className="text-muted">永久操作记录</span></Space>
        <Descriptions bordered size="small" column={1} items={[
          { key: 'id', label: '记录编号', children: <Typography.Text copyable>{record.id}</Typography.Text> },
          { key: 'user', label: '操作人', children: `${record.operator.name}（${record.operator.account}）` },
          { key: 'userId', label: '用户编号', children: record.operator.userId },
          { key: 'start', label: '操作时间', children: localTime(record.startedAtLocal) },
          { key: 'end', label: '结束时间', children: localTime(record.completedAtLocal) },
          { key: 'before', label: '创建时间早于', children: localTime(record.createdBefore) },
          { key: 'count', label: '执行数量', children: `计划 ${record.plannedCount.toLocaleString()} 条，实际删除 ${record.executedCount.toLocaleString()} 条，已提交 ${record.batchCount} 批` },
          { key: 'scope', label: '清理范围', children: record.scope },
          { key: 'boundary', label: '恢复说明', children: record.compensationBoundary },
          { key: 'ip', label: '来源地址', children: record.operator.clientIp || '-' },
          { key: 'trace', label: '请求追踪编号', children: record.operator.traceId || '-' },
        ]} />
        {record.errorMessage && <InfoAlert type="warning" message={record.errorMessage} closable={false} />}
        <div className="cleanup-deleted-heading"><h3>已删除包裹清单</h3><span className="text-muted">保留删除前的身份快照，便于按编号和条码追溯。</span></div>
        <Input.Search aria-label="检索已删除包裹" placeholder="包裹编号 / 条码 / 工作台" maxLength={128} allowClear onSearch={value => { setSearch(value.trim()); setDetailPage(1); }} className="cleanup-history-search" />
        <DataTable<ParcelCleanupDeletedItem> loading={detail.loading} dataSource={detail.data?.items ?? []} pagination={{ current: detailPage, pageSize: detailSize, total: detail.data?.totalCount ?? 0, onChange: (next, nextSize) => { setDetailPage(nextSize !== detailSize ? 1 : next); setDetailSize(nextSize); } }} locale={{ emptyText: record.executedCount === 0 ? '本次操作没有删除包裹' : '没有匹配的包裹' }} columns={[
          { title: '包裹编号', dataIndex: 'id', width: 185 }, { title: '主条码', dataIndex: 'barCodes', width: 240 },
          { title: '工作台', dataIndex: 'workstationName', width: 145 }, { title: '创建时间', dataIndex: 'createdTime', width: 180, render: localTime },
          { title: '来源实例', dataIndex: 'sourceInstanceId', width: 200 }, { title: '来源会话', dataIndex: 'sourceRunId', width: 170 }, { title: '来源包裹编号', dataIndex: 'sourceParcelId', width: 150 },
        ]} />
      </>}
    </Drawer>
  </>;
}
