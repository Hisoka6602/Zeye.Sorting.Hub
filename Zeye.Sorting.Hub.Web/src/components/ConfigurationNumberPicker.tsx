import { InputNumber, Select } from 'antd';
import { useState } from 'react';

/** 常用数值可直接选择；自定义入口保留现有精度和完整取值范围。 */
interface ConfigurationNumberPickerProps {
  value?: number | null; onChange?: (value: number | null) => void;
  presets?: readonly number[]; min?: number; max?: number; step?: number; precision?: number;
  id?: string; 'aria-label'?: string; disabled?: boolean; unit?: string;
}

/** 配置数值的统一编辑控件，未知旧值作为当前选项保留，不自动改成预设值。 */
export function ConfigurationNumberPicker({ value, onChange, presets, min, max, step, precision, id, 'aria-label': ariaLabel, disabled, unit }: ConfigurationNumberPickerProps) {
  const [custom, setCustom] = useState(false);
  const inputProps = { value, onChange, min, max, step, precision, disabled, style: { width: '100%' } };
  if (!presets?.length) return <InputNumber id={id} aria-label={ariaLabel} {...inputProps} />;
  const choices = [...new Set(presets.filter(number => (min === undefined || number >= min) && (max === undefined || number <= max)))];
  if (typeof value === 'number' && Number.isFinite(value) && !choices.includes(value)) choices.push(value);
  const options: { value: number | string; label: string }[] = choices.map(number => ({ value: number, label: `${number}${unit ? ` ${unit}` : ''}` }));
  options.push({ value: 'custom', label: '自定义数值…' });
  return <div style={{ display: 'grid', gap: 8, minWidth: 0, width: '100%' }}>
    <Select id={id} aria-label={ariaLabel} value={custom ? 'custom' : value ?? undefined} options={options} disabled={disabled} style={{ width: '100%' }}
      onChange={next => { setCustom(next === 'custom'); if (typeof next === 'number') onChange?.(next); }} />
    {custom && <InputNumber id={id ? `${id}-custom` : undefined} aria-label={`${ariaLabel ?? '配置数值'} 自定义数值`} {...inputProps} />}
  </div>;
}
