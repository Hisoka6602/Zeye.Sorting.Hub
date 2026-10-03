import { formatNumber } from '../../data/formatNumber';
import { Alert, App, Button, Collapse, Descriptions, Empty, Space, Tabs, Tag, Typography, Spin } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import { useParams } from 'react-router';
import { ApiFeedback } from '../../components/ApiFeedback';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { useApiResource } from '../../data/api/useApiResource';
import { localTime, type AuditDetail } from '../../data/api/operationalTypes';
import { describeAuditRequest } from './requestDescriptions';
export function AuditDetailPage() {
  const { id } = useParams();
  const resource = useApiResource<AuditDetail>(id ? '/api/audit/web-requests/' + encodeURIComponent(id) : null);
  const { message } = App.useApp();
  const record = resource.data;
  const copy = async (text: string) => { try { await navigator.clipboard.writeText(text); message.success('已复制'); } catch { message.error('复制失败，请检查浏览器权限'); } };
  return <div className="audit-detail-page">
    <PageIntro title="审计详情" description="查看服务器记录的请求、响应及数据库访问摘要。" back="/audit/requests" />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    {resource.loading ? <Spin /> : !record ? !resource.error && <Empty description="审计记录不存在" /> : <>
      <SectionCard title="基本信息" className="audit-basic"><div className="detail-grid">
        <div><div className="detail-pair"><span className="detail-label">HTTP 方法</span><Tag color="blue">{record.requestMethod}</Tag></div><div className="detail-pair"><span className="detail-label">请求路径</span>{record.requestPath}</div><div className="detail-pair"><span className="detail-label">状态码</span><StatusTag value={record.statusCode} /></div></div>
        <div><div className="detail-pair"><span className="detail-label">耗时</span>{formatNumber(record.durationMs)} ms</div><div className="detail-pair"><span className="detail-label">TraceId</span><Typography.Text copyable>{record.traceId}</Typography.Text></div><div className="detail-pair"><span className="detail-label">开始时间</span>{localTime(record.startedAt)}</div></div>
      </div></SectionCard>
      <SectionCard className="audit-content"><Tabs items={[
        { key: 'overview', label: '概览', children: <><Descriptions bordered column={1} items={[
          { key: 'description', label: '中文说明', children: describeAuditRequest(record.requestMethod, record.requestPath) },
          { key: 'id', label: '记录 ID', children: record.id }, { key: 'correlation', label: 'CorrelationId', children: record.correlationId || '-' },
          { key: 'route', label: '路由模板', children: record.requestRouteTemplate || '-' }, { key: 'user', label: '用户身份', children: record.isAuthenticated ? record.userName || '已认证' : '匿名' },
          { key: 'host', label: '请求主机', children: record.requestHost || '-' }, { key: 'span', label: 'SpanId', children: record.spanId || '-' },
          { key: 'end', label: '结束时间', children: localTime(record.endedAt) }, { key: 'reqSize', label: '请求大小', children: formatNumber(record.requestSizeBytes) + ' 字节' }, { key: 'resSize', label: '响应大小', children: formatNumber(record.responseSizeBytes) + ' 字节' },
        ]} /><Collapse style={{ marginTop: 16 }} items={[{ key: 'details', label: '请求和响应详情（已按服务器策略脱敏）', children: <><Space><Button icon={<CopyOutlined />} onClick={() => copy(record.curlCommand)} disabled={!record.curlCommand}>复制 cURL</Button></Space><pre className="json-block">{record.curlCommand || '未记录回放命令'}</pre></> }]} /></> },
        { key: 'request', label: '请求', children: <><pre className="json-block">{record.requestHeadersJson || '未记录请求头'}</pre><pre className="json-block">{record.requestBody || '未记录请求体'}</pre></> },
        { key: 'response', label: '响应', children: <><pre className="json-block">{record.responseHeadersJson || '未记录响应头'}</pre><pre className="json-block">{record.responseBody || '未记录响应体'}</pre></> },
        { key: 'exception', label: '异常', children: record.errorMessage ? <><Alert type="error" showIcon message={record.errorMessage} description={record.exceptionType} /><pre className="json-block">{record.exceptionStackTrace}</pre></> : <Empty description="本次请求没有异常" /> },
        { key: 'db', label: '数据库访问', children: <><Descriptions bordered column={2} items={[{ key: 'ms', label: '数据库耗时', children: formatNumber(record.databaseDurationMs) + ' ms' }, { key: 'count', label: '访问次数', children: record.databaseAccessCount }]} /><pre className="json-block">{record.databaseOperationSummary || '未记录数据库访问摘要'}</pre></> },
      ]} /></SectionCard>
    </>}
  </div>;
}
