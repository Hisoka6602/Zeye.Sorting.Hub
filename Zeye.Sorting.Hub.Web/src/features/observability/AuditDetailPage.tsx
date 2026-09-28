import { Alert, App, Button, Collapse, Descriptions, Empty, Space, Steps, Tabs, Tag, Typography } from 'antd';
import { CalendarOutlined, CopyOutlined } from '@ant-design/icons';
import { useParams } from 'react-router';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import { auditRecords, type AuditRecord } from '../../data/mock/observability';
import { contentTypographyForPath } from '../../app/contentTypography';

export function AuditDetailPage() {
  const { id } = useParams();
  const selected = auditRecords.find(item => item.id === Number(id));
  const { message } = App.useApp();
  if (!selected) return <><PageIntro title="审计详情" back="/audit/requests" /><SectionCard><Empty description="审计记录不存在" /></SectionCard></>;
  const record: AuditRecord = selected.id === 1001
    ? { ...selected, path: '/api/parcels/2509250001', status: 500, duration: 386, trace: 'b7e2c8d4-1f9a-4c30-9f2e-6d7a9c3e5f01', correlation: 'c1f3a9d2-8d2e-4f1b-9ad6-3e9b8f7c2a11', error: '处理异常，返回 500' }
    : selected;
  const copy = (text: string) => navigator.clipboard.writeText(text).then(() => message.success('已复制'));
  return <div className="audit-detail-page">
    <PageIntro title="审计详情" description="查看请求的完整执行信息、处理链路与相关上下文。" back="/audit/requests" />
    <SectionCard title="基本信息" className="audit-basic"><div className="detail-grid">
      <div><div className="detail-pair"><span className="detail-label">HTTP 方法</span><Tag color="blue">{record.method}</Tag></div><div className="detail-pair"><span className="detail-label">请求路径</span><span className="audit-request-path">{record.path}</span></div><div className="detail-pair"><span className="detail-label">状态码</span><StatusTag value={record.status} /></div></div>
      <div><div className="detail-pair"><span className="detail-label">耗时</span>{record.duration} ms</div><div className="detail-pair"><span className="detail-label">TraceId</span><Typography.Text copyable={{ text: record.trace }}>{record.trace}</Typography.Text></div><div className="detail-pair"><span className="detail-label">开始时间</span><span className="audit-start-time">{record.time}<CalendarOutlined className="audit-date-icon" /></span></div></div>
    </div></SectionCard>
    <SectionCard className="audit-content">
      <Tabs defaultActiveKey="overview" items={[
        { key: 'overview', label: '概览', children: <>
          <h3 style={{ fontSize: 18, marginBottom: 22, ...contentTypographyForPath('/audit/requests/:id', 'section', '请求处理链路') }}>请求处理链路</h3>
          <Steps labelPlacement="vertical" current={2} status={record.status >= 400 ? 'error' : 'finish'} items={[
            { title: '接收请求', description: <span>{record.time}<br />网关接收并校验请求</span> },
            { title: '业务处理', description: <span>{record.time}<br />进入应用，执行业务逻辑</span> },
            { title: '返回响应', description: <span>{record.time}<br />{record.status >= 400 ? `处理异常，返回 ${record.status}` : '处理完成，返回响应'}</span> },
          ]} />
          <div className="audit-key-box"><h3 style={contentTypographyForPath('/audit/requests/:id', 'section', '关键字段')}>关键字段</h3><div className="two-cols">
            <Descriptions bordered size="small" column={1} items={[
              { key: '1', label: 'CorrelationId', children: <Typography.Text copyable={{ text: record.correlation }}>{record.correlation}</Typography.Text> },
              { key: '2', label: '路由模板', children: record.path.replace(/\d{8,}/g, '{id}') },
              { key: '3', label: '用户身份', children: 'user_1001 (张三)' },
              { key: '4', label: '请求大小', children: '1.24 KB' },
            ]} />
            <Descriptions bordered size="small" column={1} items={[
              { key: '1', label: '服务名', children: record.service },
              { key: '2', label: '实例地址', children: '10.0.12.36:8080' },
              { key: '3', label: 'spanId', children: '9a7d3e2b1c4f6d8e' },
              { key: '4', label: '响应大小', children: '0.56 KB' },
            ]} />
          </div></div>
          <Collapse className="audit-sensitive" items={[{ key: 'sensitive', label: <><b>敏感详情（需权限）</b><div className="text-muted">包含请求/响应 Header、Body 及完整的 cURL 命令。此内容可能包含敏感信息，需特定权限后查看。</div></>, children: <><InfoAlert message="本地演示内容。生产环境需要服务端鉴权和脱敏。" /><Space><Button onClick={() => copy(JSON.stringify(record, null, 2))} icon={<CopyOutlined />}>复制示例详情</Button></Space><pre className="json-block">{JSON.stringify({ headers: { 'content-type': 'application/json' }, body: { parcelId: record.path.split('/').at(-1) }, curl: `curl ${record.path}` }, null, 2)}</pre></> }]} />
        </> },
        { key: 'request', label: '请求', children: <Descriptions bordered column={1} items={[{ key: '1', label: '方法', children: record.method }, { key: '2', label: '路径', children: record.path }, { key: '3', label: 'TraceId', children: record.trace }]} /> },
        { key: 'response', label: '响应', children: <Descriptions bordered column={1} items={[{ key: '1', label: '状态码', children: <StatusTag value={record.status} /> }, { key: '2', label: '耗时', children: `${record.duration} ms` }, { key: '3', label: '响应大小', children: '0.56 KB' }]} /> },
        { key: 'exception', label: '异常', children: record.error ? <Alert type="error" showIcon message={record.error} description="请检查相关服务及 TraceId。" /> : <Empty description="本次请求没有异常" /> },
        { key: 'db', label: '数据库访问', children: <Descriptions bordered column={2} items={[{ key: '1', label: '数据库耗时', children: `${Math.round(record.duration * .45)} ms` }, { key: '2', label: '查询次数', children: '3' }]} /> },
      ]} />
    </SectionCard>
  </div>;
}
