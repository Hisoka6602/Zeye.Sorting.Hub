import { Table, type TableProps } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import { formatNumber } from '../data/formatNumber';

function formatColumns<T extends object>(columns: NonNullable<TableProps<T>['columns']>): NonNullable<TableProps<T>['columns']> {
  return columns.map((column): (typeof columns)[number] => {
    if ('children' in column) return { ...column, children: formatColumns(column.children) };
    if (column.render) return column;
    return { ...column, render: value => typeof value === 'number' ? formatNumber(value) : value };
  });
}

export function DataTable<T extends object>(props: TableProps<T>) {
  const { className, pagination, scroll, rowKey, columns, size = 'middle', ...rest } = props;
  return <Table<T> {...rest} columns={columns && formatColumns(columns)} rowKey={rowKey || ((record) => String((record as { id?: string | number; fingerprint?: string }).id ?? (record as { fingerprint?: string }).fingerprint))} size={size} className={`data-table ${className || ''}`} pagination={pagination === false ? false : { defaultPageSize: 10, showSizeChanger: { labelRender: ({ value }) => `${value} 条/页` }, showTotal: total => `共 ${formatNumber(total)} 条`, locale: { ...zhCN.Pagination, items_per_page: '条/页' }, ...(typeof pagination === 'object' ? pagination : {}) }} scroll={{ x: 'max-content', ...scroll }} />;
}
