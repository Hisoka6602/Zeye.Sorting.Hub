import { Avatar } from 'antd';

const accountGlyph = <svg width="1em" height="1em" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
  <circle cx="12" cy="7" r="4" /><path d="M4 20.5c0-4.2 3.2-7 8-7s8 2.8 8 7Z" />
</svg>;

/** 头像地址与 API 使用同一来源；没有图片时保持原有的圆形用户图标。 */
export function AccountAvatar({ src, name, size = 32 }: { src?: string | null; name?: string; size?: number }) {
  const image = src?.startsWith('/api/') ? (import.meta.env.VITE_API_BASE_URL ?? '') + src : src;
  return <Avatar className="account-avatar" shape="circle" size={size} src={image || undefined}
    alt={name ? `${name}的头像` : '用户头像'} icon={accountGlyph}
    style={{ fontSize: size * .7, color: '#fff', backgroundColor: '#609bf5' }} />;
}
