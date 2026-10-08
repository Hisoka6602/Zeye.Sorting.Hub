import { AppstoreOutlined, ClockCircleOutlined, DatabaseOutlined, IdcardOutlined, SwapOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';
import { ParcelFacts } from './ParcelFacts';
import { groupParcelContractFields } from './parcelContractFieldGroups';
import './parcelContractFields.css';

/** 分组图标仅提示信息归属，不代表包裹业务状态。 */
const groupIcons: Record<string, ReactNode> = {
  identity: <IdcardOutlined />, routing: <SwapOutlined />, measurement: <AppstoreOutlined />,
  times: <ClockCircleOutlined />, attachments: <DatabaseOutlined />, other: <AppstoreOutlined />,
};

/** 在原有完整字段区中分组展示，复用现有真实值格式化与复制行为。 */
export function ParcelContractFields({ facts, keys }: { facts: object; keys: readonly string[] }) {
  const groups = groupParcelContractFields(keys);
  return <div className="parcel-contract-fields">
    <p className="parcel-contract-intro">按业务分组查看完整信息，点击字段值旁的图标可复制。</p>
    {groups.map(group => <section key={group.id} className="parcel-contract-group" data-group={group.id} aria-label={group.title}>
      <div className="parcel-contract-group-heading">
        <span className="parcel-contract-group-icon" aria-hidden="true">{groupIcons[group.id]}</span>
        <h3>{group.title}</h3>
        <span className="parcel-contract-group-description">{group.description}</span>
        <span className="parcel-contract-group-count">{group.keys.length} 项字段</span>
      </div>
      <ParcelFacts facts={facts} keys={group.keys} />
    </section>)}
  </div>;
}
