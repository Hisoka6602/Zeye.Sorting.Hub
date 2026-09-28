import { App, Button, Descriptions, Drawer, Form, Input, Space, Tabs, Tag, Tree } from 'antd';
import { DownOutlined, SearchOutlined } from '@ant-design/icons';
import { useMemo, useState } from 'react';
import { useRoles } from '../../data/stores/access';
import { DataTable } from '../../components/DataTable';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { StatusTag } from '../../components/StatusTag';
import type { Role } from '../../data/mock/access';

const permissionGroups = [
  { name: '包裹查询', children: ['查看包裹台账', '查看包裹详情', '导出包裹数据'] },
  { name: '写入操作', children: ['新建包裹', '批量入队', '修改包裹信息', '取消 / 删除包裹'] },
  { name: '审计详情', children: ['查看操作日志', '查看请求审计'] },
  { name: '数据治理', children: ['执行归档任务', '管理 Outbox 消息', '清理过期数据'] },
];
const demoUsers = [
  { id: 1, name: '张三', account: 'zhangsan', role: '超级管理员', status: '启用', last: '2026-09-25 16:18:40' },
  { id: 2, name: '李四', account: 'lisi', role: '运营管理员', status: '启用', last: '2026-09-25 14:22:13' },
  { id: 3, name: '王五', account: 'wangwu', role: '审计专员', status: '启用', last: '2026-09-24 09:17:32' },
  { id: 4, name: '赵六', account: 'zhaoliu', role: '仓库操作员', status: '停用', last: '2026-09-23 11:32:03' },
];

export function AccessPage() {
  const { message } = App.useApp();
  const [roles, setRoles] = useRoles();
  const [tab, setTab] = useState('roles');
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState(2);
  const [drawer, setDrawer] = useState<'detail' | 'create' | null>('detail');
  const [draftPermissions, setDraftPermissions] = useState<string[]>(roles.find(role => role.id === 2)?.permissions || []);
  const [form] = Form.useForm();
  const selected = roles.find(role => role.id === selectedId);
  const filteredRoles = useMemo(() => roles.filter(role => `${role.name} ${role.description}`.includes(search)), [roles, search]);
  const newRole = (values: { name: string; description: string }) => {
    const id = Math.max(...roles.map(item => item.id), 6) + 1;
    setRoles(current => [...current, { ...values, id, members: 0, modified: '2026-09-25 16:28:40', permissions: draftPermissions }]);
    setSelectedId(id); setDrawer('detail'); form.resetFields(); message.success('新角色草稿已保存');
  };
  const save = () => {
    setRoles(current => current.map(role => role.id === selectedId ? { ...role, permissions: draftPermissions, modified: '2026-09-25 16:28:40' } : role));
    message.success('角色权限草稿已保存在本地'); setDrawer(null);
  };
  const select = (role: Role) => { setSelectedId(role.id); setDraftPermissions(role.permissions); setDrawer('detail'); };
  return <>
    <PageIntro title="账号与权限" planned description="管理平台用户、角色及权限配置。" />
    <SectionCard className="access-table"><Tabs activeKey={tab} onChange={setTab} items={[
      { key: 'users', label: '用户', children: <><div className="toolbar-row"><Input.Search style={{ width: 300 }} placeholder="请输入用户姓名或账号" onSearch={setSearch} allowClear onChange={event => setSearch(event.target.value)} /><Button onClick={() => message.info('用户创建需接入认证服务，当前为规划稿')}>新建用户</Button></div><DataTable dataSource={demoUsers.filter(user => `${user.name} ${user.account}`.includes(search))} columns={[{ title: '姓名', dataIndex: 'name' }, { title: '账号', dataIndex: 'account' }, { title: '角色', dataIndex: 'role' }, { title: '状态', dataIndex: 'status', render: (value: string) => <StatusTag value={value} /> }, { title: '最近登录', dataIndex: 'last' }]} /></> },
      { key: 'roles', label: '角色', children: <><div className="toolbar-row"><Space><Input type="search" className="role-search" style={{ width: 300 }} placeholder="请输入角色名称或说明" onPressEnter={() => setSearch(search)} value={search} onChange={event => setSearch(event.target.value)} allowClear suffix={<Button className="role-search-button" aria-label="查询角色" type="text" icon={<SearchOutlined />} onClick={() => setSearch(search)} />} /><Button onClick={() => setSearch('')}>重置</Button></Space><Button type="primary" onClick={() => { setDraftPermissions([]); setDrawer('create'); }}>新建角色</Button></div><DataTable tableLayout="fixed" scroll={{ x: undefined }} dataSource={filteredRoles} rowClassName={role => role.id === selectedId && drawer === 'detail' ? 'selected-table-row' : ''} columns={[{ title: '角色名称', dataIndex: 'name', render: (value: string, role: Role) => <Space>{value}{role.builtIn && <Tag color="blue">内置</Tag>}</Space> }, { title: '说明', dataIndex: 'description' }, { title: '成员数', dataIndex: 'members' }, { title: '最近修改', dataIndex: 'modified' }, { title: '操作', render: (_: unknown, role: Role) => <Button type="link" className="table-link" onClick={() => select(role)}>查看</Button> }].map((column, index) => ({ ...column, width: [159, 270, 85, 169, 83][index] }))} /></> },
      { key: 'matrix', label: '权限矩阵', children: <DataTable dataSource={permissionGroups.map((group, index) => ({ id: index, name: group.name, permissions: group.children.join('、') }))} columns={[{ title: '权限分组', dataIndex: 'name' }, { title: '可配置权限', dataIndex: 'permissions' }]} pagination={false} /> },
    ]} /></SectionCard>
    <Drawer title={drawer === 'create' ? '新建角色' : '查看角色'} open={drawer !== null} onClose={() => setDrawer(null)} width={398} mask={false} rootClassName="reference-drawer access-drawer" footer={<Space><Button onClick={() => setDrawer(null)}>{drawer === 'detail' ? '返回' : '取消'}</Button><Button type="primary" onClick={() => drawer === 'create' ? form.submit() : save()}>保存草稿</Button></Space>}>
      {drawer === 'create' ? <Form form={form} layout="vertical" onFinish={newRole}><Form.Item name="name" label="角色名称" rules={[{ required: true }]}><Input /></Form.Item><Form.Item name="description" label="说明" rules={[{ required: true }]}><Input.TextArea rows={3} /></Form.Item><h3>权限配置</h3><PermissionGroups value={draftPermissions} onChange={setDraftPermissions} /></Form>
        : selected && <><h3>基本信息</h3><Descriptions className="drawer-meta" size="small" column={1} colon={false} items={[{ key: '1', label: '角色名称', children: selected.name }, { key: '2', label: '说明', children: selected.description }]} /><div className="access-detail-divider" /><h3>权限配置</h3><PermissionGroups value={draftPermissions} onChange={setDraftPermissions} /></>}
    </Drawer>
  </>;
}

function PermissionGroups({ value, onChange }: { value: string[]; onChange: (next: string[]) => void }) {
  const toggle = (permission: string, checked: boolean) => onChange(checked ? [...new Set([...value, permission])] : value.filter(item => item !== permission));
  return <div className="permission-groups">{permissionGroups.map(group => <div className="permission-group" key={group.name}>
    <Tree
      className="permission-tree"
      aria-label={`${group.name}权限`}
      checkable
      checkStrictly
      selectable={false}
      defaultExpandAll
      showLine={{ showLeafIcon: false }}
      switcherIcon={<DownOutlined />}
      checkedKeys={[...value.filter(permission => group.children.includes(permission)), ...(value.includes(group.name) || group.children.some(item => value.includes(item)) ? [group.name] : [])]}
      onCheck={(_, info) => {
        const permission = String(info.node.key);
        if (permission === group.name) onChange(info.checked ? [...new Set([...value, group.name])] : value.filter(item => item !== group.name && !group.children.includes(item)));
        else toggle(permission, info.checked);
      }}
      treeData={[{ key: group.name, title: <b>{group.name}</b>, children: group.children.map(permission => ({ key: permission, title: permission })) }]}
    />
  </div>)}</div>;
}
