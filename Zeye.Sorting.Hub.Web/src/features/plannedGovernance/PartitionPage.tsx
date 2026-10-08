import {
  CalendarOutlined, CheckCircleFilled, CheckCircleOutlined, DatabaseOutlined,
  InfoCircleOutlined, PlusOutlined, ReloadOutlined, SearchOutlined, TableOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import { App, Button, Drawer, Empty, Input, Popconfirm, Segmented, Space, Spin, Tag } from 'antd';
import { useState } from 'react';
import { ApiFeedback } from '../../components/ApiFeedback';
import { DataTable } from '../../components/DataTable';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';
import { localTime, type PartitionStatus } from '../../data/api/operationalTypes';
import { useApiResource } from '../../data/api/useApiResource';
import { formatNumber } from '../../data/formatNumber';
import './partition.css';

const granularityLabels: Record<string, string> = {
  PerDay: '按日分表', PerWeek: '按周分表', PerMonth: '按月分表',
};

export function PartitionPage() {
  const { message } = App.useApp();
  const resource = useApiResource<PartitionStatus>('/api/operations/partitions');
  const data = resource.data;
  const plan = data?.prebuild;
  const [busy, setBusy] = useState(false);
  const [query, setQuery] = useState('');
  const [directoryPage, setDirectoryPage] = useState(1);
  const [planOpen, setPlanOpen] = useState(false);
  const [planQuery, setPlanQuery] = useState('');
  const [onlyMissing, setOnlyMissing] = useState(false);
  const [planPage, setPlanPage] = useState(1);
  const missingTables = new Set(plan?.missingPhysicalTables);
  const plannedTables = plan?.plannedPhysicalTables ?? [];
  const planReady = Boolean(plan?.isEnabled && plannedTables.length > 0 && missingTables.size === 0);
  const canPrebuild = Boolean(data?.allowTableCreation && !data.creationDryRun);
  const directoryEntries = data?.entries.filter(item => item.suffix.includes(query.trim())) ?? [];
  const planEntries = plannedTables
    .filter(name => (!onlyMissing || missingTables.has(name)) && name.toLowerCase().includes(planQuery.trim().toLowerCase()))
    .map(name => ({ name, missing: missingTables.has(name) }));

  const prebuild = async () => {
    setBusy(true);
    try {
      const result = await requestApi<{ createdSuffixes: string[] }>(
        '/api/operations/partitions/prebuild', undefined, { method: 'POST', body: '{}' },
      );
      resource.refresh();
      message.success(result.createdSuffixes.length
        ? `已创建分区：${result.createdSuffixes.join('、')}` : '计划分区均已存在');
    } catch (error) {
      message.error(error instanceof Error ? error.message : '预建失败');
    } finally {
      setBusy(false);
    }
  };

  return <div className="partition-page">
    <PageIntro
      title="分区管理"
      description="查看物理分表目录，按服务器策略预建当前及未来周期的包裹分区。"
      action={<Space className="partition-page-actions">
        <Button icon={<ReloadOutlined />} loading={resource.loading} onClick={resource.refresh}>刷新目录</Button>
        <Popconfirm
          title="预建包裹分区？"
          description="创建预建窗口内的主表及关联表。已登记的分区会自动跳过。"
          okText="预建" cancelText="取消" onConfirm={prebuild}
        >
          <Button type="primary" icon={<PlusOutlined />} loading={busy} disabled={!canPrebuild || resource.loading}>预建分区</Button>
        </Popconfirm>
      </Space>}
    />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    <InfoAlert
      message="预建按当前服务器粒度和运维策略执行，仅创建新的包裹分区。"
      description="需允许建表并关闭建表预演。目录展示最近 200 个分区；总计划包含审计日表，包裹预建不执行审计规划。"
      closable={false}
    />
    {resource.loading && !data ? <div className="partition-loading" role="status" aria-label="正在加载分区信息"><Spin /></div> : null}
    {data ? <Spin spinning={resource.loading} wrapperClassName="partition-content">
      <div className="partition-overview-grid">
        <SectionCard
          className="partition-policy-card"
          title={<span className="partition-section-title"><DatabaseOutlined />服务器分表策略</span>}
          extra={<Tag className="partition-provider">{data.provider}</Tag>}
        >
          <dl className="partition-policy-grid">
            <div><dt>分表粒度</dt><dd>{granularityLabels[data.granularity] ?? data.granularity}<span className="partition-policy-code">{data.granularity}</span></dd></div>
            <div><dt>当前物理表后缀</dt><dd><code className="partition-suffix-value">{data.currentSuffix}</code></dd></div>
            <div><dt>创建物理分表</dt><dd><Tag color={data.allowTableCreation ? 'green' : 'default'}>{data.allowTableCreation ? '已允许' : '已禁用'}</Tag></dd></div>
            <div><dt>创建模式</dt><dd><Tag color={data.creationDryRun ? 'orange' : 'blue'}>{data.creationDryRun ? '建表预演' : '实际创建'}</Tag></dd></div>
          </dl>
          <div className={`partition-policy-note${canPrebuild ? ' is-enabled' : ''}`}>
            {canPrebuild ? <CheckCircleOutlined /> : <InfoCircleOutlined />}
            <span>{!data.allowTableCreation ? '服务器禁止创建物理分表，预建操作暂不可用。'
              : data.creationDryRun ? '建表预演已开启，关闭后可执行预建。'
                : '可执行包裹预建，已登记的分区会自动跳过。'}</span>
          </div>
        </SectionCard>

        <SectionCard
          className="partition-plan-card"
          title={<span className="partition-section-title"><CalendarOutlined />最近预建计划</span>}
          extra={plan ? <Tag color={planReady ? 'green' : plan.isEnabled ? 'blue' : 'default'}>
            {planReady ? '计划表已齐备' : plan.isEnabled ? '已启用' : '已关闭'}
          </Tag> : <Tag>未生成</Tag>}
        >
          {plan ? <>
            <div className="partition-plan-metrics">
              <div><span>计划表数</span><strong>{formatNumber(plannedTables.length)}<small>张</small></strong></div>
              <div><span>待创建表数</span><strong className={missingTables.size ? 'is-pending' : 'is-complete'}>{formatNumber(missingTables.size)}<small>张</small></strong></div>
            </div>
            <div className={`partition-plan-message${planReady ? ' is-complete' : ''}`}>
              {planReady ? <CheckCircleFilled /> : <InfoCircleOutlined />}
              <span>{plan.message}</span>
              {plan.isDryRun ? <Tag color="orange">预演计划</Tag> : null}
            </div>
            <div className="partition-plan-footer">
              <div><span>计划生成时间</span><time>{localTime(plan.generatedAtLocal)}</time></div>
              <Button type="link" icon={<UnorderedListOutlined />} onClick={() => setPlanOpen(true)}>查看计划明细</Button>
            </div>
          </> : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚无预建计划记录" />}
        </SectionCard>
      </div>

      <SectionCard
        className="partition-directory-card"
        title={<span className="partition-section-title"><TableOutlined />物理分表目录<span className="partition-directory-count">{formatNumber(data.entries.length)} 个分区</span></span>}
        extra={<Input
          className="partition-directory-search" prefix={<SearchOutlined />}
          aria-label="筛选分表后缀" placeholder="搜索分区后缀，如 202610"
          value={query} onChange={event => { setQuery(event.target.value); setDirectoryPage(1); }} allowClear
        />}
      >
        <div className="partition-directory-note">
          <span>最近 200 个分区 · 时间范围包含开始，不包含结束</span>
          {query.trim() ? <span>匹配 {formatNumber(directoryEntries.length)} 个分区</span> : null}
        </div>
        <DataTable
          rowKey="suffix" dataSource={directoryEntries} scroll={{ x: 1000 }}
          pagination={{ size: 'default', current: directoryPage, onChange: page => setDirectoryPage(page) }}
          rowClassName={item => item.suffix === data.currentSuffix ? 'partition-current-row' : ''}
          locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={query.trim() ? '未找到匹配的分区，请调整后缀' : '暂无已登记分区'} /> }}
          columns={[
            { title: '物理表后缀', dataIndex: 'suffix', width: 190, render: suffix => <Space size={10}><code className="partition-table-suffix">{suffix}</code>{suffix === data.currentSuffix ? <Tag color="blue">当前分区</Tag> : null}</Space> },
            { title: '开始时间', dataIndex: 'start', width: 230, render: value => <span className="partition-time">{localTime(value)}</span> },
            { title: '结束时间（不含）', dataIndex: 'end', width: 230, render: value => <span className="partition-time">{localTime(value)}</span> },
            { title: '登记时间', dataIndex: 'createdTime', width: 230, render: value => <span className="partition-time">{localTime(value)}</span> },
            { title: '数据库', width: 120, render: () => <span className="partition-database-label"><DatabaseOutlined />{data.provider}</span> },
          ]}
        />
      </SectionCard>
    </Spin> : null}

    <Drawer
      title={<span className="partition-section-title"><UnorderedListOutlined />预建计划明细</span>}
      className="partition-plan-drawer" width="min(680px, 100vw)" open={planOpen} onClose={() => setPlanOpen(false)}
    >
      <p className="partition-plan-explanation">总计划包含包裹物理表及审计日表。包裹预建操作仅创建包裹分区。</p>
      {plan ? <p className="partition-plan-generated">计划生成时间：{localTime(plan.generatedAtLocal)}</p> : null}
      <div className="partition-plan-tools">
        <Segmented
          aria-label="计划表范围" value={onlyMissing ? 'missing' : 'all'}
          options={[
            { label: `全部计划 ${formatNumber(plannedTables.length)}`, value: 'all' },
            { label: `待创建 ${formatNumber(missingTables.size)}`, value: 'missing' },
          ]}
          onChange={value => { setOnlyMissing(value === 'missing'); setPlanPage(1); }}
        />
        <Input prefix={<SearchOutlined />} aria-label="筛选计划表名" placeholder="搜索物理表名" value={planQuery}
          onChange={event => { setPlanQuery(event.target.value); setPlanPage(1); }} allowClear />
      </div>
      <DataTable
        rowKey="name" size="small" dataSource={planEntries} tableLayout="fixed" scroll={{ x: '100%' }}
        pagination={{ size: 'default', current: planPage, onChange: page => setPlanPage(page) }}
        locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={planQuery.trim() ? '未找到匹配的计划表' : onlyMissing ? '没有待创建的物理表' : '暂无计划表'} /> }}
        columns={[
          { title: '物理表名', dataIndex: 'name', render: name => <code className="partition-physical-name">{name}</code> },
          { title: '检查结果', dataIndex: 'missing', width: 108, render: missing => <Tag color={missing ? 'orange' : 'green'}>{missing ? '待创建' : '已存在'}</Tag> },
        ]}
      />
    </Drawer>
  </div>;
}
