import { App, Button, Form, Input, Skeleton, Space, Tag } from 'antd';
import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { AccountAvatar } from '../../components/AccountAvatar';
import { ApiFeedback } from '../../components/ApiFeedback';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import type { AccessProfile } from '../../data/api/accessTypes';
import { ApiError, requestApi } from '../../data/api/client';
import { useApiResource } from '../../data/api/useApiResource';
import { sessionChanged } from '../../data/api/useAccessSession';
import { prepareAvatar } from './avatarUpload';
import './profile.css';

type ProfileFields = Pick<AccessProfile, 'name' | 'email' | 'phone' | 'bio'>;

export function ProfilePage() {
  const resource = useApiResource<AccessProfile>('/api/access/profile');
  const { message } = App.useApp();
  const [form] = Form.useForm<ProfileFields>();
  const [saved, setSaved] = useState<AccessProfile>();
  const [avatarDraft, setAvatarDraft] = useState<string>();
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [saveError, setSaveError] = useState<Error>();
  const fileInput = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (!resource.data) return;
    setSaved(resource.data); form.setFieldsValue(resource.data); setAvatarDraft(undefined); setDirty(false); setSaveError(undefined);
  }, [resource.data, form]);

  const reset = () => {
    if (saved) form.setFieldsValue(saved);
    setAvatarDraft(undefined); setDirty(false); setSaveError(undefined);
  };
  const chooseAvatar = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]; event.target.value = '';
    if (!file) return;
    setUploading(true);
    try { setAvatarDraft(await prepareAvatar(file)); setDirty(true); }
    catch (error) { message.error(error instanceof Error ? error.message : '头像处理失败'); }
    finally { setUploading(false); }
  };
  const save = async (values: ProfileFields) => {
    if (!saved || busy || uploading) return;
    setBusy(true); setSaveError(undefined);
    try {
      const updated = await requestApi<AccessProfile>('/api/access/profile', undefined, { method: 'PUT', body: JSON.stringify({
        name: values.name.trim(), email: values.email.trim(), phone: values.phone.trim(), bio: values.bio.trim(),
        expectedRevision: saved.revision, directoryRevision: saved.directoryRevision,
        ...(avatarDraft !== undefined ? { avatarDataUrl: avatarDraft } : {}),
      }) });
      setSaved(updated); form.setFieldsValue(updated); setAvatarDraft(undefined); setDirty(false);
      message.success('个人信息已保存'); sessionChanged();
    } catch (error) { setSaveError(error instanceof Error ? error : new Error('保存失败，请重试')); }
    finally { setBusy(false); }
  };
  const preview = avatarDraft !== undefined ? avatarDraft : saved?.avatarUrl;
  const disabled = busy || uploading;
  return <div className="profile-page">
    <PageIntro title="个人中心" description="管理你的头像和个人信息。" />
    <ApiFeedback error={resource.error} retry={resource.refresh} />
    {resource.loading && !saved ? <SectionCard><Skeleton active paragraph={{ rows: 6 }} /></SectionCard> : saved && <div className="profile-grid">
      <SectionCard className="profile-summary">
        <div className="profile-avatar-preview"><AccountAvatar src={preview} name={saved.name} size={80} /></div>
        <h2>{saved.name}</h2><Tag color="blue" className="profile-role">{saved.roleName}</Tag>
        <div className="profile-avatar-actions">
          <input ref={fileInput} type="file" accept="image/jpeg,image/png" aria-label="选择头像图片" className="profile-file-input" disabled={disabled} onChange={chooseAvatar} />
          <Button block loading={uploading} disabled={busy} onClick={() => fileInput.current?.click()}>更换头像</Button>
          <Button type="text" disabled={disabled || !preview} onClick={() => { setAvatarDraft(''); setDirty(true); }}>移除头像</Button>
          <p>JPG / PNG，最大 5 MB<br />头像会自动居中裁剪</p>
        </div>
        <dl className="profile-account-details">
          <div><dt>登录账号</dt><dd>{saved.account}</dd></div>
          <div><dt>最近登录</dt><dd>{saved.lastLogin ?? '—'}</dd></div>
        </dl>
      </SectionCard>
      <SectionCard title="基本信息" className="profile-form-card">
        <p className="profile-form-description">名称与头像用于页面显示，联系信息可按需填写。</p>
        <ApiFeedback error={saveError} retry={saveError instanceof ApiError && saveError.status === 409 ? resource.refresh : undefined} />
        <Form form={form} layout="vertical" requiredMark={(label, { required }) => <>{label}{!required && <span className="profile-optional">（选填）</span>}</>}
          onFinish={save} onValuesChange={() => setDirty(true)} disabled={disabled}>
          <Form.Item name="name" label="名称" extra="名称修改不会影响登录账号。" rules={[{ required: true, whitespace: true, message: '请输入名称' }, { max: 100, message: '名称不能超过 100 字' }]}>
            <Input maxLength={100} placeholder="请输入你的名称" autoComplete="name" />
          </Form.Item>
          <div className="profile-contact-grid">
            <Form.Item name="email" label="邮箱" rules={[{ type: 'email', message: '请输入有效的邮箱地址' }, { max: 254 }]}>
              <Input maxLength={254} placeholder="请输入邮箱地址" autoComplete="email" />
            </Form.Item>
            <Form.Item name="phone" label="手机号码" rules={[{ pattern: /^[+0-9][0-9() .-]{2,31}$/, message: '请输入有效的联系电话' }]}>
              <Input maxLength={32} placeholder="请输入联系电话" autoComplete="tel" />
            </Form.Item>
          </div>
          <Form.Item name="bio" label="个人简介" rules={[{ max: 500 }]}>
            <Input.TextArea rows={3} maxLength={500} showCount placeholder="简单介绍一下自己（选填）" />
          </Form.Item>
          <div className="profile-form-footer"><Space><Button type="primary" htmlType="submit" loading={busy} disabled={!dirty || uploading}>保存修改</Button><Button disabled={!dirty || disabled} onClick={reset}>重置</Button></Space>
            <span>保存后同步更新头像与名称</span></div>
        </Form>
      </SectionCard>
    </div>}
  </div>;
}
