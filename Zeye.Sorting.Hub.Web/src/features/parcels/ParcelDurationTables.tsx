import { Alert, Space, Tag } from 'antd';
import { Link } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { SectionCard } from '../../components/SectionCard';
import type { ParcelDurationAnalysis, ParcelDurationInterface, ParcelDurationSample } from '../../data/api/parcelAnalysisTypes';
import { localTime } from '../../data/api/operationalTypes';
import { formatNumber } from '../../data/formatNumber';

/** 亚毫秒分析显示保留100ns精度，不影响全站其他数字的格式。 */
const durationNumber = new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 4 });
export const durationMilliseconds = (value: number | null | undefined) => value == null ? '—' : durationNumber.format(value);

/** 各接口独立汇总，正文和认证查询参数不会进入图表或列表。 */
export function DurationInterfaces({ data, loading }: { data: ParcelDurationAnalysis | undefined | null; loading: boolean }) {
  return <SectionCard title="各接口调用耗时" extra={<span className="text-muted">按 Provider 与接口地址区分</span>}>
    {data?.interfacesTruncated && <Alert showIcon type="info" message="接口分组较多，仅展示调用最多的 100 组；总体统计仍包含全部样本。" />}
    <DataTable<ParcelDurationInterface> rowKey={row => JSON.stringify([row.provider, row.requestUrl])} dataSource={data?.interfaces ?? []} loading={loading}
      countUnit="组" scroll={{ x: 920 }} pagination={{ pageSize: 10, showSizeChanger: false }} columns={[
        { title: 'Provider', dataIndex: 'provider', width: 170, render: value => value || '未提供' },
        { title: '接口地址', dataIndex: 'requestUrl', width: 320, ellipsis: true, render: value => value || '未提供地址' },
        { title: '调用次数', dataIndex: 'count', width: 100, render: value => formatNumber(value, { grouping: true }) },
        { title: '明确失败', dataIndex: 'failedCount', width: 100, render: value => formatNumber(value, { grouping: true }) },
        { title: '平均耗时（ms）', dataIndex: 'averageMilliseconds', width: 140, render: durationMilliseconds },
        { title: 'P95（ms）', dataIndex: 'p95Milliseconds', width: 130, render: durationMilliseconds },
      ]} locale={{ emptyText: '该范围暂无有效接口调用耗时' }} />
  </SectionCard>;
}

/** 阶段按票、接口按调用列出；明确上报的耗时不会倒推出虚构端点。 */
export function DurationSamples({ data, loading, unavailable, page, calls, onPage }: {
  data: ParcelDurationAnalysis | undefined | null; loading: boolean; unavailable: boolean; page: number; calls: boolean; onPage: (page: number) => void;
}) {
  return <DataTable<ParcelDurationSample> rowKey="key" dataSource={data?.items ?? []} loading={loading} countUnit={calls ? '次' : '票'}
    scroll={{ x: calls ? 1790 : 1460 }} pagination={{ current: page, pageSize: 20, total: data?.filteredCount ?? 0, showSizeChanger: false, onChange: onPage }}
    locale={{ emptyText: unavailable ? '分析数据暂不可用，请重试' : '当前区间没有有效耗时样本' }} columns={[
      { title: '包裹 ID', dataIndex: 'parcelId', width: 190, render: value => <Link to={`/parcels/${encodeURIComponent(value)}`}>{value}</Link> },
      { title: '主条码', dataIndex: 'barCodes', width: 190, ellipsis: true, render: value => value || '未提供' },
      { title: '耗时（ms）', dataIndex: 'milliseconds', width: 150, render: (value, row) => <><strong>{durationMilliseconds(value)}</strong><div className="text-muted">{row.timingSource}</div></> },
      { title: '来源工作台 / 实例', width: 220, render: (_, row) => <><div>{row.workstationName || '未提供'}</div><span className="text-muted">{row.sourceInstanceId || '未提供来源实例'}</span></> },
      { title: '开始时间', dataIndex: 'startedAt', width: 230, render: value => value ? localTime(value) : '未提供' },
      { title: '结束时间', dataIndex: 'endedAt', width: 230, render: value => value ? localTime(value) : '未提供' },
      ...(calls ? [
        { title: 'Provider / 尝试', width: 180, render: (_: unknown, row: ParcelDurationSample) => <><div>{row.provider || '未提供'}</div><span className="text-muted">{row.attemptNumber ? `第 ${row.attemptNumber} 次尝试` : '未提供尝试号'}</span></> },
        { title: '业务结果', width: 120, render: (_: unknown, row: ParcelDurationSample) => <Tag color={row.isSuccess === true ? 'green' : row.isSuccess === false ? 'red' : undefined}>{row.isSuccess === true ? '成功' : row.isSuccess === false ? '失败' : '结果未知'}</Tag> },
      ] : []),
      { title: '分析', width: 150, fixed: 'right', render: (_, row) => <Space><Link to={`/parcels/${encodeURIComponent(row.parcelId)}`}>详情</Link><Link to={`/parcels/timing?id=${encodeURIComponent(row.parcelId)}`}>时序</Link></Space> },
    ]} />;
}
