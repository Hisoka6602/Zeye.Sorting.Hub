import { Empty, Table, type TableProps } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import { formatNumber } from '../data/formatNumber';

function formatColumns<T extends object>(columns: NonNullable<TableProps<T>['columns']>): NonNullable<TableProps<T>['columns']> {
  return columns.map((column): (typeof columns)[number] => {
    if ('children' in column) return { ...column, children: formatColumns(column.children) };
    if (column.render) return column;
    return { ...column, render: value => typeof value === 'number' ? formatNumber(value) : value };
  });
}

/** 总数与分页共用计数单位，包裹列表使用“票”。 */
export function DataTable<T extends object>(props: TableProps<T> & { countUnit?: '条' | '票' | '组' | '次' }) {
  const { className, pagination, scroll, rowKey, columns, locale, size = 'middle', countUnit = '条', ...rest } = props;
  const tableLocale = typeof locale?.emptyText === 'string'
    ? { ...locale, emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={locale.emptyText} /> }
    : locale;
  return <Table<T> {...rest} locale={tableLocale} columns={columns && formatColumns(columns)} rowKey={rowKey || ((record) => String((record as { id?: string | number; fingerprint?: string }).id ?? (record as { fingerprint?: string }).fingerprint))} size={size} className={`data-table ${className || ''}`} pagination={pagination === false ? false : { defaultPageSize: 10, showSizeChanger: { labelRender: ({ value }) => `${value} ${countUnit}/页` }, showTotal: total => `共 ${formatNumber(total)} ${countUnit}`, locale: { ...zhCN.Pagination, items_per_page: `${countUnit}/页` }, ...(typeof pagination === 'object' ? pagination : {}) }} scroll={{ x: 'max-content', ...scroll }} />;
}
