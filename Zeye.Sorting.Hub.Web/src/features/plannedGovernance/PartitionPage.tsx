import { App, Button, Descriptions, Input, Popconfirm, Space, Spin, Tag } from 'antd';
import { useState } from 'react';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { useApiResource } from '../../data/api/useApiResource';
import { requestApi } from '../../data/api/client';
import { localTime, type PartitionStatus } from '../../data/api/operationalTypes';
export function PartitionPage() {
  const { message } = App.useApp();
  const [busy, setBusy] = useState(false);
  const resource = useApiResource<PartitionStatus>('/api/operations/partitions');
  const data = resource.data;
  const [query, setQuery] = useState('');
  const prebuild = async () => {
    setBusy(true);
    try { const result = await requestApi<{ createdSuffixes: string[] }>('/api/operations/partitions/prebuild', undefined, { method: 'POST', body: '{}' }); resource.refresh(); message.success(result.createdSuffixes.length ? `已创建分区：${result.createdSuffixes.join('、')}` : '计划分区均已存在'); }
    catch (error) { message.error(error instanceof Error ? error.message : '预建失败'); } finally { setBusy(false); }
  };
  return <>
    <PageIntro title="分区管理" description="查看物理分表目录，并预建当前及未来周期的包裹分区。" action={<Space><Button loading={resource.loading} onClick={resource.refresh}>刷新目录</Button><Popconfirm title="预建包裹分区？" description="创建预建窗口内的主表及关联表。已登记的分区会自动跳过。" okText="预建" cancelText="取消" onConfirm={prebuild}><Button type="primary" loading={busy} disabled={!data?.allowTableCreation || data.creationDryRun}>预建分区</Button></Popconfirm></Space>} />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    <InfoAlert message="预建使用服务器当前粒度和运维策略，只创建新的包裹分区。" description="需要服务器允许建表并关闭建表预演。目录最多展示最近 200 个分区；下方总计划还包含审计日表规划，包裹预建不执行该审计规划。" closable={false} />
    {resource.loading ? <Spin /> : data && <>
      <div className="two-cols partition-top"><SectionCard title="服务器分表策略"><Descriptions column={1} items={[
        { key: 'provider', label: '数据库提供程序', children: data.provider }, { key: 'granularity', label: '分表粒度', children: data.granularity },
        { key: 'suffix', label: '当前物理表后缀', children: data.currentSuffix }, { key: 'allow', label: '允许创建物理分表', children: data.allowTableCreation ? '是' : '否' },
        { key: 'dryrun', label: '创建模式', children: data.creationDryRun ? '预演' : '实际创建' },
      ]} /></SectionCard><SectionCard title="最近预建计划">
        {data.prebuild ? <><Tag>{data.prebuild.isEnabled ? '预建计划' : '关闭'}</Tag><p>{data.prebuild.message}</p><p>计划生成时间：{localTime(data.prebuild.generatedAtLocal)}</p><p>计划表数：{data.prebuild.plannedPhysicalTables.length}　待创建表数：{data.prebuild.missingPhysicalTables.length}</p><pre className="json-block">{data.prebuild.plannedPhysicalTables.join('\n') || '无计划表'}</pre></> : <p>尚无预建计划记录</p>}
      </SectionCard></div>
      <SectionCard title="物理分表目录" extra={<Input aria-label="筛选分表后缀" placeholder="筛选分表后缀" value={query} onChange={event => setQuery(event.target.value)} allowClear />}><DataTable rowKey="suffix" dataSource={data.entries.filter(item => item.suffix.includes(query))} columns={[
        { title: '物理表后缀', dataIndex: 'suffix' }, { title: '开始时间', dataIndex: 'start', render: localTime }, { title: '结束边界（不含）', dataIndex: 'end', render: localTime },
        { title: '登记时间', dataIndex: 'createdTime', render: localTime }, { title: '数据库', render: () => data.provider },
      ]} /></SectionCard>
    </>}
  </>;
}
