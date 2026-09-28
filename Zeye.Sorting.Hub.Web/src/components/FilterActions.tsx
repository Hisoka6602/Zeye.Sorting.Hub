import { Button, Space } from 'antd';
import type { ReactNode } from 'react';

export function FilterActions({ onSearch, onReset, extra }: { onSearch: () => void; onReset: () => void; extra?: ReactNode }) {
  return <Space className="filter-actions"><Button type="primary" autoInsertSpace={false} onClick={onSearch}>查询</Button><Button autoInsertSpace={false} onClick={onReset}>重置</Button>{extra}</Space>;
}
