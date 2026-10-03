import { Alert, App, Button, Checkbox, Form, Input, Spin, Tag, Typography } from 'antd';
import { ExclamationCircleOutlined, LockOutlined, UserOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { BrandWordmark } from '../../components/BrandMark';
import { ApiFeedback } from '../../components/ApiFeedback';
import { requestApi } from '../../data/api/client';
import { useAccessSession, sessionChanged } from '../../data/api/useAccessSession';
import { reservedAccountRule } from '../../data/api/reservedAccountValidation';
export function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { message, modal } = App.useApp();
  const session = useAccessSession();
  const [error, setError] = useState('');
  const [remember, setRemember] = useState(false);
  const [busy, setBusy] = useState(false);
  const setup = session.data?.configured === false;
  const login = async (values: { username: string; password: string; name?: string; bootstrapKey?: string }) => {
    setBusy(true); setError('');
    try {
      await requestApi('/api/access/' + (setup ? 'bootstrap' : 'login'), undefined, { method: 'POST', body: JSON.stringify({ ...values, remember }) });
      sessionChanged(); message.success(setup ? '管理员已初始化并登录' : '登录成功'); navigate(searchParams.get('returnTo') === '/profile' ? '/profile' : '/data-overview', { replace: true });
    } catch (reason) { setError(reason instanceof Error ? reason.message : '登录失败'); } finally { setBusy(false); }
  };
  return <div className="login-card">
    <div className="login-brand"><BrandWordmark className="login-wordmark" variant="login" />{setup && <Tag color="blue">首次初始化</Tag>}</div>
    <div className="login-subtitle">智能分拣管理平台</div><div className="login-divider" />
    <ApiFeedback error={session.error} retry={session.refresh} />
    {session.loading ? <Spin /> : session.data && <>
      {setup && <Alert style={{ marginBottom: 16 }} type="info" showIcon message={session.data.bootstrapAvailable ? '创建首个管理员' : '服务器尚未配置初始化密钥'} description="管理员请使用部署配置中的初始化密钥，并自行设置账号和密码。创建成功后初始化入口自动关闭。" />}
      <Form layout="vertical" onFinish={login}>
        {setup && <><Form.Item name="bootstrapKey" label="初始化密钥" rules={[{ required: true, message: '请输入部署初始化密钥' }]}><Input.Password autoComplete="off" placeholder="部署配置中的 ZEYE_BOOTSTRAP_KEY" /></Form.Item><Form.Item name="name" label="姓名" rules={[{ required: true, whitespace: true, message: '请输入管理员姓名' }]}><Input maxLength={100} placeholder="管理员姓名" /></Form.Item></>}
        <Form.Item className="login-user-field" name="username" label="账号" rules={[{ required: true, message: '请输入账号' }, ...(setup ? [{ pattern: /^[A-Za-z0-9_.-]{3,64}$/, message: '请输入 3~64 位字母、数字或 _.-' }, reservedAccountRule] : [])]}><Input autoComplete="username" prefix={<UserOutlined />} placeholder="请输入账号" size="large" /></Form.Item>
        <Form.Item className="login-password-field" name="password" label="密码" rules={[{ required: true, message: '请输入密码' }, ...(setup ? [{ min: 12, max: 128, message: '密码长度需为 12~128' }] : [])]}><Input.Password autoComplete={setup ? 'new-password' : 'current-password'} prefix={<LockOutlined />} placeholder={setup ? '至少 12 位' : '请输入密码'} size="large" /></Form.Item>
        {!setup && <div className="login-options"><Checkbox checked={remember} onChange={event => setRemember(event.target.checked)}>记住我</Checkbox><Button type="link" onClick={() => modal.info({ title: '忘记密码', content: '请联系具有账号管理权限的管理员重置密码。' })}>忘记密码？</Button></div>}
        <Button type="primary" htmlType="submit" size="large" block loading={busy} disabled={setup && !session.data.bootstrapAvailable} autoInsertSpace={false}>{setup ? '创建管理员并登录' : '登录'}</Button>
        {error && <Typography.Text type="danger" style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 16 }}><ExclamationCircleOutlined />{error}</Typography.Text>}
      </Form>
    </>}
  </div>;
}
