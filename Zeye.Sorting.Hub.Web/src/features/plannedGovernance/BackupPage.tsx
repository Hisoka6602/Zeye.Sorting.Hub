import { formatNumber } from '../../data/formatNumber';
import { App, Button, Descriptions, Popconfirm, Space, Spin, Tag } from 'antd';
import { useState } from 'react';
import { ReloadOutlined, SafetyCertificateFilled } from '@ant-design/icons';
import { ApiFeedback } from '../../components/ApiFeedback';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { DataTable } from '../../components/DataTable';
import { useApiResource } from '../../data/api/useApiResource';
import { requestApi } from '../../data/api/client';
import { localTime, type BackupStatus, type BackupArtifact, type BackupArtifacts } from '../../data/api/operationalTypes';
export function BackupPage() {
  const { message } = App.useApp();
  const resource = useApiResource<BackupStatus>('/api/operations/backup');
  const artifacts = useApiResource<BackupArtifacts>('/api/operations/backup/artifacts');
  const [busy, setBusy] = useState(false);
  const refresh = () => { resource.refresh(); artifacts.refresh(); };
  const execute = async (id?: string) => {
    setBusy(true);
    try {
      const result = await requestApi<BackupArtifact>('/api/operations/backup/artifacts' + (id ? `/${id}/restore-isolated` : ''), undefined, { method: 'POST', body: '{}' });
      refresh(); message.success(id ? `恢复及行数核验通过：${result.restoredDatabase}` : '数据库备份已完成');
    } catch (error) { message.error(error instanceof Error ? error.message : '执行失败'); } finally { setBusy(false); }
  };
  const backup = resource.data;
  return <>
    <PageIntro title="备份与恢复" description="备份数据库表结构及数据，并在隔离数据库中恢复核验。" action={<Space><Button icon={<ReloadOutlined />} loading={resource.loading || artifacts.loading} onClick={refresh}>刷新状态</Button><Button type="primary" loading={busy} disabled={!artifacts.data?.isSupported || !resource.data?.isEnabled} onClick={() => execute()}>创建备份</Button></Space>} />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    <ApiFeedback error={artifacts.error} retry={artifacts.refresh} />
    <InfoAlert message="隔离恢复会新建 zeye_restore_ 数据库，逐表核对备份行数。" description="当前支持 MySQL 事务表的结构与数据快照，包含包裹物理分表、规则及账号。文件完整性通过 SHA256 校验；恢复不会切换或覆盖业务库。含视图或存储程序的数据库请使用原生备份工具。" closable={false} />
    {resource.loading ? <Spin /> : backup && <div className="two-cols backup-top">
      <SectionCard title={<><SafetyCertificateFilled style={{ color: '#1677ff', marginRight: 10 }} />备份治理策略</>}>
        <Descriptions column={1} items={[
          { key: 'enabled', label: '备份治理', children: backup.isEnabled ? '启用' : '关闭' }, { key: 'dryrun', label: '自动创建备份', children: backup.automaticBackups ? '启用' : '关闭（可手动创建）' },
          { key: 'interval', label: '自动备份间隔', children: formatNumber(backup.backupIntervalMinutes) + ' 分钟' }, { key: 'age', label: '文件最大允许年龄', children: formatNumber(backup.maxAllowedBackupAgeHours) + ' 小时' },
          { key: 'provider', label: '当前数据源', children: backup.provider || '尚无执行记录' }, { key: 'database', label: '数据库', children: backup.database || '-' },
        ]} />
      </SectionCard>
      <SectionCard title="最近一次校验">
        <Tag color={backup.status === 'Completed' ? 'green' : backup.status === 'Failed' ? 'red' : 'default'}>{backup.status}</Tag><p>{backup.summary}</p>
        <Descriptions column={1} items={[
          { key: 'recorded', label: '校验时间', children: localTime(backup.recordedAtLocal) }, { key: 'backup', label: '备份文件时间', children: localTime(backup.verifiedBackupAtLocal) },
          { key: 'exists', label: '发现备份文件', children: backup.hasBackupFile ? '是' : '否' }, { key: 'fresh', label: '文件仍在有效期内', children: backup.isBackupFileFresh ? '是' : '否' },
        ]} />
      </SectionCard>
    </div>}
    <SectionCard title="数据库备份文件"><DataTable<BackupArtifact> rowKey="id" loading={artifacts.loading} dataSource={artifacts.data?.artifacts ?? []} scroll={{ x: 1100 }} columns={[
      { title: '备份时间', dataIndex: 'createdAtLocal', width: 180, render: localTime }, { title: '来源数据库', dataIndex: 'database', width: 180 },
      { title: '表数', render: (_, item) => Object.keys(item.tableRows).length }, { title: '总行数', render: (_, item) => Object.values(item.tableRows).reduce((sum, value) => sum + Number(value), 0) },
      { title: '文件大小', render: (_, item) => `${formatNumber(Number(item.sizeBytes) / 1024)} KB` },
      { title: '恢复核验', width: 260, render: (_, item) => item.verifiedAtLocal ? <><Tag color="green">行数核验通过</Tag><div>{item.restoredDatabase}</div><small>{localTime(item.verifiedAtLocal)}</small></> : '尚未执行' },
      { title: '操作', width: 170, render: (_, item) => <Space><Button type="link" href={`${import.meta.env.VITE_API_BASE_URL ?? ''}/api/operations/backup/artifacts/${item.id}/download`}>下载</Button><Popconfirm title="恢复到新的隔离数据库？" description="核对完整性后新建独立数据库，并逐表验证数据行数。" okText="恢复核验" cancelText="取消" onConfirm={() => execute(item.id)}><Button type="link" loading={busy}>恢复核验</Button></Popconfirm></Space> },
    ]} /></SectionCard>
  </>;
}
