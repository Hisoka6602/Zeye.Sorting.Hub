import { InputNumber, type InputNumberProps } from 'antd';
import { formatNumericInput } from '../data/formatNumber';

export function NumberUnit({ unit, style, ...props }: InputNumberProps<number> & { unit: string }) {
  return <div className="number-unit-control" style={style}><InputNumber<number> formatter={formatNumericInput} {...props} style={{ flex: 1, width: 1 }} /><span className="number-unit">{unit}</span></div>;
}
