const options: Intl.NumberFormatOptions = {
  notation: 'standard',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
};
const plainNumber = new Intl.NumberFormat('zh-CN', { ...options, useGrouping: false });
const groupedNumber = new Intl.NumberFormat('zh-CN', { ...options, useGrouping: true });

/** 只格式化显示值：最多两位小数、不补零、不使用科学计数法。 */
export function formatNumber(value: number | bigint | null | undefined, { grouping = false } = {}): string {
  if (value == null || (typeof value === 'number' && !Number.isFinite(value))) return '—';
  const formatted = (grouping ? groupedNumber : plainNumber).format(value);
  return formatted === '-0' ? '0' : formatted;
}

/** 输入期间保留原文；结束编辑后统一显示，原始值仍由表单保存。 */
export function formatNumericInput(value: number | string | undefined, info: { userTyping: boolean; input: string }): string {
  if (info.userTyping) return info.input;
  if (value == null || value === '') return '';
  // stringMode 的整数无需转换为 Number，避免丢失大整数的精度。
  if (typeof value === 'string' && /^[+-]?\d+$/.test(value)) return formatNumber(BigInt(value));
  return formatNumber(Number(value));
}
