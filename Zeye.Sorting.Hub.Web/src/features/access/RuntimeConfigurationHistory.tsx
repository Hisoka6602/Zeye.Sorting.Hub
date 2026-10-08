import { Button, Drawer, Empty, Result, Select, Space, Table, Tag } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useEffect, useMemo, useState } from 'react';
import { ApiFeedback } from '../../components/ApiFeedback';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import type { ConfigurationHistoryEntry, ConfigurationValue } from '../../data/api/configurationTypes';
import { localTime } from '../../data/api/operationalTypes';
import { configurationValueAt } from './configurationModel';

const documentNames: Record<string, string> = { runtime: '运行配置', 'operations-policy': '运维策略', 'rules-parcel': '包裹分类规则', 'rules-exception': '异常规则', 'fusion-ingestion-directory': 'Fusion 接入目录' };
const statuses = { Committed: { label: '已提交', color: 'green' }, Pending: { label: '待确认', color: 'gold' }, Failed: { label: '未提交', color: 'red' }, Unconfirmed: { label: '结果未确认', color: 'orange' } };
function parseHistory(json: string): ConfigurationValue | undefined { try { return JSON.parse(json) as ConfigurationValue; } catch { return undefined; } }
function displayValue(document: ConfigurationValue | undefined, key: string): string {
  const value = document === undefined ? undefined : configurationValueAt(document, key);
  return value === undefined ? '未设置' : typeof value === 'string' ? value || '空字符串' : JSON.stringify(value);
}

/** 仅超级管理员可读取修改前后原值，兼容旧版快照，失败状态不显示为成功。 */
export function RuntimeConfigurationHistory({ allowed, active, refreshToken }: { allowed: boolean; active: boolean; refreshToken: number }) {
  const [limit, setLimit] = useState(100);
  const [documentKey, setDocumentKey] = useState('all');
  const [selected, setSelected] = useState<ConfigurationHistoryEntry>();
  const resource = useApiResource<ConfigurationHistoryEntry[]>(allowed && active ? `/api/operations/configuration/history?limit=${limit}` : null, requestApi, false);
  useEffect(() => { if (active && allowed && refreshToken > 0) resource.refresh(); }, [active, allowed, refreshToken, resource.refresh]);
  const rows = useMemo(() => resource.data?.filter(entry => documentKey === 'all' || entry.documentKey === documentKey) ?? [], [resource.data, documentKey]);
  const before = selected ? parseHistory(selected.beforeJson) : undefined;
  const after = selected ? parseHistory(selected.afterJson) : undefined;
  if (!allowed) return <Result status="403" title="配置变更历史仅限超级管理员查看" />;
  return <>
    <SectionCard title={<div><div className="settings-section-title">配置变更历史</div><div className="settings-section-caption">查看修改前后值及提交状态</div></div>} extra={<Button icon={<ReloadOutlined />} onClick={resource.refresh} loading={resource.loading}>刷新历史</Button>}>
      <div className="runtime-history-toolbar"><Space wrap>
        <Select aria-label="筛选配置类型" value={documentKey} onChange={setDocumentKey} options={[{ value: 'all', label: '全部配置类型' }, ...Object.entries(documentNames).map(([value, label]) => ({ value, label }))]} />
        <Select aria-label="历史读取数量" value={limit} onChange={setLimit} options={[100, 200, 500].map(value => ({ value, label: `最近 ${value} 条` }))} />
      </Space><span className="settings-section-caption">修改记录跨重启保留</span></div>
      {resource.error ? <ApiFeedback error={resource.error} retry={resource.refresh} /> : <Table<ConfigurationHistoryEntry> rowKey="id" dataSource={rows} loading={resource.loading} size="middle" scroll={{ x: 780 }} pagination={{ pageSize: 10, hideOnSinglePage: true, showSizeChanger: false }} locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无配置变更记录" /> }} columns={[
        { title: '修改时间', dataIndex: 'recordedAtLocal', width: 200, render: value => localTime(String(value)) },
        { title: '配置类型', dataIndex: 'documentKey', width: 180, render: value => documentNames[String(value)] ?? String(value) },
        { title: '修改项', dataIndex: 'changedKeys', width: 120, render: (keys: string[]) => `${keys.length} 项` },
        { title: '提交状态', dataIndex: 'status', width: 140, render: (status: ConfigurationHistoryEntry['status']) => <Tag color={statuses[status]?.color}>{statuses[status]?.label ?? status}</Tag> },
        { title: '操作', key: 'actions', width: 130, render: (_, entry) => <Button type="link" onClick={() => setSelected(entry)}>查看差异</Button> },
      ]} />}
    </SectionCard>
    <Drawer className="runtime-history-drawer" title="配置变更详情" placement="right" width={820} open={Boolean(selected)} onClose={() => setSelected(undefined)}>
      {selected && <>
        <div className="runtime-history-meta"><span>{documentNames[selected.documentKey] ?? selected.documentKey}</span><span>{localTime(selected.recordedAtLocal)}</span><Tag color={statuses[selected.status]?.color}>{statuses[selected.status]?.label ?? selected.status}</Tag></div>
        <dl className="runtime-history-revisions"><dt>修改前版本</dt><dd>{selected.previousRevision}</dd><dt>修改后版本</dt><dd>{selected.revision}</dd></dl>
        <Table rowKey="key" size="small" scroll={{ x: 650 }} pagination={false} dataSource={selected.changedKeys.map(key => ({ key, before: displayValue(before, key), after: displayValue(after, key) }))} columns={[
          { title: '配置字段', dataIndex: 'key', width: 260, render: value => <span className="runtime-field-key">{String(value)}</span> },
          { title: '修改前', dataIndex: 'before', width: 200, render: value => <span className="runtime-history-value">{String(value)}</span> },
          { title: '修改后', dataIndex: 'after', width: 200, render: value => <span className="runtime-history-value">{String(value)}</span> },
        ]} />
        <details className="runtime-history-json"><summary>查看配置快照</summary><h4>修改前</h4><pre>{before === undefined ? '快照格式无法解析' : JSON.stringify(before, null, 2)}</pre><h4>修改后</h4><pre>{after === undefined ? '快照格式无法解析' : JSON.stringify(after, null, 2)}</pre></details>
      </>}
    </Drawer>
  </>;
}
