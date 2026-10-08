import { Button, Descriptions, Drawer, Space, Spin, Tag, Typography } from 'antd';
import { useState } from 'react';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { localTime, type PagedResult } from '../../data/api/operationalTypes';
import { cleanupStatusLabel, type ParcelCleanupDetail, type ParcelCleanupRecord } from '../../data/api/parcelCleanupTypes';
import { useApiResource } from '../../data/api/useApiResource';

/** 查询永久操作汇总，详情不再下载或展示逐票包裹清单。 */
export function ParcelCleanupHistory({ revision, selectedId, onSelect }: { revision: number; selectedId: string | null; onSelect: (id: string | null) => void }) {
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(10);
  const history = useApiResource<PagedResult<ParcelCleanupRecord>>(`/api/admin/parcels/cleanup-history?pageNumber=${page}&pageSize=${size}&revision=${revision}`);
  const detail = useApiResource<ParcelCleanupDetail>(selectedId ? `/api/admin/parcels/cleanup-history/${selectedId}?revision=${revision}` : null);
  const record = detail.data?.record;
  const status = (item: ParcelCleanupRecord) => <Tag color={item.status === 'failed' ? 'red' : item.status === 'running' ? 'blue' : item.decision === 'execute' && item.status === 'completed' ? 'green' : 'gold'}>{cleanupStatusLabel(item)}</Tag>;
  return <>
    <div className="toolbar-row cleanup-history-toolbar"><span className="text-muted">永久保留操作人、清理条件、执行数量和结果。</span><Button onClick={history.refresh}>刷新记录</Button></div>
    <ApiFeedback error={history.error} retry={history.refresh} />
    <DataTable<ParcelCleanupRecord> loading={history.loading} dataSource={history.data?.items ?? []} pagination={{ current: page, pageSize: size, total: history.data?.totalCount ?? 0, onChange: (next, nextSize) => { setPage(nextSize !== size ? 1 : next); setSize(nextSize); } }} columns={[
      { title: '操作时间', dataIndex: 'startedAtLocal', width: 180, render: localTime },
      { title: '操作人', width: 180, render: (_, item) => <div>{item.operator.name}<div className="text-muted">{item.operator.account}</div></div> },
      { title: '创建时间早于', dataIndex: 'createdBefore', width: 180, render: localTime },
      { title: '结果', width: 155, render: (_, item) => status(item) },
      { title: '计划票数', dataIndex: 'plannedCount', width: 110 },
      { title: '删除票数', dataIndex: 'executedCount', width: 110 },
      { title: '操作', width: 110, render: (_, item) => <Button type="link" onClick={() => onSelect(item.id)}>查看记录</Button> },
    ]} />
    <Drawer title="清理记录详情" width="min(960px, 100vw)" open={selectedId !== null} onClose={() => onSelect(null)} extra={<Button onClick={detail.refresh}>刷新详情</Button>}>
      {detail.loading && <Spin />}
      <ApiFeedback error={detail.error} retry={detail.refresh} />
      {record && <>
        <Space className="cleanup-detail-summary">{status(record)}<span className="text-muted">永久操作记录</span></Space>
        <InfoAlert type={record.storageFormat === 'operation-summary' ? 'info' : 'warning'} message="清理历史仅展示操作汇总" description={record.storageFormat === 'operation-summary' ? '不复制包裹编号、条码、图片或处理报文，减少清理记录的存储占用。包裹恢复需使用备份。' : '此旧版记录尚未完成存储精简，原身份清单仍保留；服务启动时按清理隔离策略转换。'} closable={false} />
        <Descriptions bordered size="small" column={1} items={[
          { key: 'id', label: '记录编号', children: <Typography.Text copyable>{record.id}</Typography.Text> },
          { key: 'user', label: '操作人', children: `${record.operator.name}（${record.operator.account}）` },
          { key: 'userId', label: '用户编号', children: record.operator.userId },
          { key: 'start', label: '操作时间', children: localTime(record.startedAtLocal) },
          { key: 'end', label: '结束时间', children: localTime(record.completedAtLocal) },
          { key: 'before', label: '创建时间早于', children: localTime(record.createdBefore) },
          { key: 'count', label: '执行数量', children: `计划 ${record.plannedCount.toLocaleString()} 票，实际删除 ${record.executedCount.toLocaleString()} 票，已提交 ${record.batchCount} 批` },
          { key: 'scope', label: '清理范围', children: record.scope },
          { key: 'boundary', label: '恢复说明', children: record.compensationBoundary },
          { key: 'ip', label: '来源地址', children: record.operator.clientIp || '-' },
          { key: 'trace', label: '请求追踪编号', children: record.operator.traceId || '-' },
        ]} />
        {record.errorMessage && <InfoAlert type="warning" message={record.errorMessage} closable={false} />}
      </>}
    </Drawer>
  </>;
}
