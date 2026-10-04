import { App, Button, Checkbox, Descriptions, Drawer, Form, Input, Select, Space, Switch, Tabs, Tag } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { DataTable } from '../../components/DataTable';
import { ApiFeedback } from '../../components/ApiFeedback';
import { InfoAlert } from '../../components/InfoAlert';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { requestApi } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import { useAccessSession, sessionChanged } from '../../data/api/useAccessSession';
import { localTime } from '../../data/api/operationalTypes';
import type { AccessDirectory, AccessRole, AccessUser } from '../../data/api/accessTypes';
import { reservedAccountRule } from '../../data/api/reservedAccountValidation';
import { canAccessRestrictedSections } from '../../app/sectionAccess';
export function AccessPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const session = useAccessSession();
  const directory = useApiResource<AccessDirectory>(session.data?.authenticated ? '/api/access' : null);
  const data = directory.data;
  const [search, setSearch] = useState('');
  const [drawer, setDrawer] = useState<'role' | 'user' | null>(null);
  const [selectedRole, setSelectedRole] = useState<AccessRole>();
  const [selectedUser, setSelectedUser] = useState<AccessUser>();
  const [busy, setBusy] = useState(false);
  const [form] = Form.useForm();
  const openRole = (role?: AccessRole) => { setSelectedRole(role); setDrawer('role'); form.resetFields(); form.setFieldsValue(role ?? { permissions: [] }); };
  const openUser = (user?: AccessUser) => { setSelectedUser(user); setDrawer('user'); form.resetFields(); form.setFieldsValue(user ? { ...user, roleId: Number(user.roleId) } : { enabled: true }); };
  const save = async (values: Record<string, unknown>) => {
    if (!data) return;
    setBusy(true);
    try {
      await requestApi('/api/access/' + (drawer === 'role' ? 'roles' : 'users'), undefined, { method: 'POST', body: JSON.stringify({ ...values, id: drawer === 'role' ? selectedRole ? Number(selectedRole.id) : undefined : selectedUser?.id, expectedRevision: data.revision }) });
      setDrawer(null); directory.refresh(); sessionChanged(); message.success('账号目录已保存到服务器');
    } catch (error) { message.error(error instanceof Error ? error.message : '保存失败'); } finally { setBusy(false); }
  };
  const canManage = session.data?.permissions.includes('access.manage') === true;
  const canManageSuperAdministrators = canAccessRestrictedSections(session.data);
  return <>
    <PageIntro title="账号与权限" description="维护真实用户和角色。停用或修改密码后，旧会话将失效。" action={<Button onClick={() => { session.refresh(); directory.refresh(); }}>刷新</Button>} />
    <ApiFeedback error={session.error ?? directory.error} retry={() => { session.refresh(); directory.refresh(); }} />
    {!session.data?.authenticated ? <InfoAlert message="请先登录管理员账号。" description={<Button type="primary" onClick={() => navigate('/access/login')}>前往登录</Button>} closable={false} /> : <>
      <InfoAlert message={session.data.enforceAuthorization ? '接口访问按服务器角色权限保护。' : '当前部署尚未启用全平台权限保护。'} description={session.data.enforceAuthorization ? '角色权限在每次请求时验证。' : '账号管理仍要求管理员权限；其他业务接口需由部署设置开启 Access:EnforceAuthorization。'} closable={false} />
      <SectionCard className="access-table"><div className="toolbar-row"><Input.Search style={{ width: 300 }} placeholder="按姓名、账号或角色名称筛选" allowClear value={search} onChange={event => setSearch(event.target.value)} /><Space><Button disabled={!canManage || !data} onClick={() => openUser()}>新建用户</Button><Button type="primary" disabled={!canManage || !data} onClick={() => openRole()}>新建角色</Button></Space></div><Tabs items={[
        { key: 'users', label: '用户', children: <DataTable<AccessUser> loading={directory.loading} dataSource={(data?.users ?? []).filter(x => !x.builtIn && (x.name + x.account + (data?.roles.find(role => role.id === x.roleId)?.name ?? '')).includes(search))} columns={[
          { title: '姓名', dataIndex: 'name' }, { title: '账号', dataIndex: 'account' }, { title: '角色', render: (_, user) => data?.roles.find(x => x.id === user.roleId)?.name ?? '-' },
          { title: '状态', dataIndex: 'enabled', render: (enabled: boolean) => <Tag color={enabled ? 'green' : 'default'}>{enabled ? '启用' : '停用'}</Tag> }, { title: '最近登录', dataIndex: 'lastLogin', render: localTime },
          { title: '操作', render: (_, user) => <Button type="link" onClick={() => openUser(user)} disabled={!canManage || !canManageSuperAdministrators && data?.roles.some(role => role.id === user.roleId && role.builtIn)}>编辑</Button> },
        ]} /> },
        { key: 'roles', label: '角色', children: <DataTable<AccessRole> loading={directory.loading} dataSource={(data?.roles ?? []).filter(x => (x.name + x.description).includes(search))} columns={[
          { title: '角色名称', dataIndex: 'name', render: (name: string, role) => <Space>{name}{role.builtIn && <Tag color="blue">内置</Tag>}</Space> }, { title: '说明', dataIndex: 'description' },
          { title: '成员数', dataIndex: 'members' }, { title: '最近修改', dataIndex: 'modified', render: localTime }, { title: '操作', render: (_, role) => <Button type="link" onClick={() => openRole(role)}>查看</Button> },
        ]} /> },
        { key: 'matrix', label: '权限矩阵', children: <DataTable rowKey="code" pagination={false} dataSource={data?.permissions ?? []} columns={[{ title: '权限代码', dataIndex: 'code' }, { title: '权限', dataIndex: 'label' }, ...(data?.roles ?? []).map(role => ({ title: role.name, key: role.id, render: (_: unknown, permission: { code: string }) => role.permissions.includes(permission.code) ? <Tag color="green">允许</Tag> : <span>—</span> }))]} /> },
      ]} /></SectionCard>
    </>}
    <Drawer title={drawer === 'role' ? selectedRole ? '查看角色' : '新建角色' : selectedUser ? '编辑用户' : '新建用户'} open={drawer !== null} onClose={() => setDrawer(null)} width={440} footer={<Space><Button onClick={() => setDrawer(null)}>取消</Button><Button type="primary" loading={busy} disabled={!canManage || drawer === 'role' && selectedRole?.builtIn} onClick={() => form.submit()}>保存</Button></Space>}>
      <Form form={form} layout="vertical" onFinish={save} disabled={drawer === 'role' && selectedRole?.builtIn}>
        {drawer === 'role' ? <>
          <Form.Item name="name" label="角色名称" rules={[{ required: true, whitespace: true }]}><Input maxLength={100} /></Form.Item><Form.Item name="description" label="说明"><Input.TextArea maxLength={512} rows={3} /></Form.Item>
          <Form.Item name="permissions" label="权限"><Checkbox.Group options={(data?.permissions ?? []).map(x => ({ value: x.code, label: x.label }))} /></Form.Item>
          {selectedRole?.builtIn && <Descriptions column={1} items={[{ key: 'builtIn', label: '内置管理员', children: '不能修改；至少保留一位启用的管理员。' }]} />}
        </> : <>
          <Form.Item name="name" label="姓名" rules={[{ required: true, whitespace: true }]}><Input maxLength={100} /></Form.Item>
          <Form.Item name="account" label="账号" rules={[{ required: true }, { pattern: /^[A-Za-z0-9_.-]{3,64}$/, message: '3~64 位字母、数字或 _.-' }, reservedAccountRule]}><Input autoComplete="off" /></Form.Item>
          <Form.Item name="roleId" label="角色" rules={[{ required: true }]}><Select options={(data?.roles ?? []).filter(role => canManageSuperAdministrators || !role.builtIn).map(role => ({ value: Number(role.id), label: role.name }))} /></Form.Item>
          <Form.Item name="password" label={selectedUser ? '新密码（留空则保持原密码）' : '密码'} rules={[{ required: !selectedUser }, { min: 12, max: 128, message: '密码长度需为 12~128' }]}><Input.Password autoComplete="new-password" /></Form.Item>
          <Form.Item name="enabled" label="启用" valuePropName="checked"><Switch /></Form.Item>
        </>}
      </Form>
    </Drawer>
  </>;
}
