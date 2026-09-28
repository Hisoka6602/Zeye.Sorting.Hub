import { App, Button, Checkbox, Form, Input, Tag, Typography } from 'antd';
import { ExclamationCircleOutlined, LockOutlined, UserOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { BrandWordmark } from '../../components/BrandMark';

export function LoginPage() {
  const navigate = useNavigate();
  const { message, modal } = App.useApp();
  const [error, setError] = useState('账号或密码不正确，请重试。');
  const [remember, setRemember] = useState(false);
  const [form] = Form.useForm();
  const login = (values: { username: string; password: string }) => {
    if (!values.username || !values.password) { setError('请输入账号和密码'); return; }
    localStorage.setItem('zeye-demo-user', values.username);
    if (remember) localStorage.setItem('zeye-demo-remember', 'true');
    setError(''); message.success('已进入本地演示工作台'); navigate('/overview');
  };
  return <div className="login-card">
    <div className="login-brand"><BrandWordmark className="login-wordmark" variant="login" /><Tag className="planned-tag">规划稿</Tag></div>
    <div className="login-subtitle">智能分拣管理平台</div><div className="login-divider" />
    <Form form={form} layout="vertical" onFinish={login}>
      <Form.Item className="login-user-field" name="username" label="账号" rules={[{ required: true, message: '请输入账号' }]}><Input prefix={<UserOutlined />} placeholder="请输入账号" size="large" /></Form.Item>
      <Form.Item className="login-password-field" name="password" label="密码" rules={[{ required: true, message: '请输入密码' }]}><Input.Password prefix={<LockOutlined />} placeholder="请输入密码" size="large" /></Form.Item>
      <div className="login-options"><Checkbox checked={remember} onChange={event => setRemember(event.target.checked)}>记住我</Checkbox><Button type="link" onClick={() => modal.info({ title: '忘记密码', content: '当前为本地演示，未接入账号服务。请联系系统管理员重置账号。' })}>忘记密码？</Button></div>
      <Button type="primary" htmlType="submit" size="large" block autoInsertSpace={false}>登录</Button>
      {error && <Typography.Text className="login-error" type="danger" style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 16 }}><ExclamationCircleOutlined />{error}</Typography.Text>}
    </Form>
  </div>;
}
