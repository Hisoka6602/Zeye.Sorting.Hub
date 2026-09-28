import { Table, type TableProps } from 'antd';
import zhCN from 'antd/locale/zh_CN';

export function DataTable<T extends object>(props: TableProps<T>) {
  const { className, pagination, scroll, rowKey, ...rest } = props;
  return <Table<T> {...rest} rowKey={rowKey || ((record) => String((record as { id?: string | number; fingerprint?: string }).id ?? (record as { fingerprint?: string }).fingerprint))} size="middle" className={`data-table ${className || ''}`} pagination={pagination === false ? false : { defaultPageSize: 10, showSizeChanger: { labelRender: ({ value }) => `${value} 条/页` }, showTotal: total => `共 ${total} 条`, locale: { ...zhCN.Pagination, items_per_page: '条/页' }, ...(typeof pagination === 'object' ? pagination : {}) }} scroll={{ x: 'max-content', ...scroll }} />;
}
