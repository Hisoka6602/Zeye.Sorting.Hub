import type { RuleCondition } from './mock/operations.ts';
import { formatNumber } from './formatNumber.ts';

export const noValueOperators = new Set(['为空', '不为空']);
const numericOperators = ['小于', '小于或等于', '等于', '大于或等于', '大于', '不等于', '为空', '不为空'];
const textOperators = ['包含', '等于', '不包含', '不等于', '为空', '不为空'];

interface ConditionUnit { value: string; factor: number }
interface ExceptionField {
  value: string;
  group: string;
  kind: 'number' | 'text';
  placeholder?: string;
  units?: ConditionUnit[];
  defaultUnit?: string;
  defaultOperator?: string;
}

const lengthUnits = [{ value: 'mm', factor: 1 }, { value: 'cm', factor: 10 }, { value: 'm', factor: 1000 }];
const typeNames = ['普通包裹', '大型包裹', '聚合包裹', '超薄包裹', '异形件', '流体包裹', '易碎品'];
const typeCodes = ['Normal', 'Large', 'Aggregated', 'UltraThin', 'Irregular', 'Liquid', 'Fragile'];
export const exceptionFields: ExceptionField[] = [
  { value: '包裹重量', group: '包裹数据', kind: 'number', units: [{ value: 'kg', factor: 1 }, { value: 'g', factor: 0.001 }], defaultUnit: 'kg' },
  { value: '包裹体积（长×宽×高）', group: '包裹数据', kind: 'number', units: [{ value: 'mm³', factor: 1 }, { value: 'cm³', factor: 1000 }, { value: 'm³', factor: 1e9 }], defaultUnit: 'cm³' },
  ...['包裹长度', '包裹宽度', '包裹高度'].map(value => ({ value, group: '包裹数据', kind: 'number' as const, units: lengthUnits, defaultUnit: 'mm' })),
  { value: '条码', group: '包裹数据', kind: 'text', placeholder: '例如 SF、001234567890', defaultOperator: '包含' },
  { value: '包裹类型', group: '包裹数据', kind: 'text', placeholder: '例如 大型包裹、易碎品' },
  { value: 'Provider 名称', group: '外部接口', kind: 'text', placeholder: '填写业务 Provider 标识' },
  { value: 'Provider 响应内容', group: '外部接口', kind: 'text', placeholder: '例如 无路由、"code":"ERROR"', defaultOperator: '包含' },
  { value: '响应状态码', group: '外部接口', kind: 'number' },
  { value: '来源异常代码', group: '分拣机与处理信息', kind: 'text', placeholder: '例如 ParcelSpacingViolation' },
  { value: '异常信息', group: '分拣机与处理信息', kind: 'text', placeholder: '例如 电机故障', defaultOperator: '包含' },
  ...['处理阶段', '目标格口', '实际格口', '路由阻断', '叠包标记', '包裹间距违规'].map(value => ({ value, group: '分拣机与处理信息', kind: 'text' as const })),
];

export const exceptionFieldOptions = [...new Set(exceptionFields.map(field => field.group))].map(label => ({
  label, options: exceptionFields.filter(field => field.group === label).map(field => ({ value: field.value, label: field.value })),
}));

export function getExceptionField(field: string | undefined) {
  return exceptionFields.find(item => item.value === field);
}

export function conditionOperators(field: string | undefined) {
  return getExceptionField(field)?.kind === 'number' ? numericOperators : textOperators;
}

/** Reject missing values, booleans, hexadecimal strings and invalid measurements instead of coercing them to zero. */
export function measurementNumber(value: unknown): number | undefined {
  if (typeof value !== 'number' && typeof value !== 'string') return undefined;
  if (typeof value === 'string' && !/^[+]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$/.test(value.trim())) return undefined;
  const number = Number(value);
  return Number.isFinite(number) && number >= 0 ? number : undefined;
}

export function createExceptionCondition(field = '包裹重量'): RuleCondition {
  const definition = getExceptionField(field);
  return {
    field, operator: definition?.defaultOperator ?? (definition?.kind === 'number' ? '大于' : '等于'),
    unit: definition?.defaultUnit, value: undefined, values: [],
  };
}

/** Old numeric thresholds have the API's base unit; old text values become a single matching item. */
export function editableExceptionCondition(condition: RuleCondition): RuleCondition {
  const definition = getExceptionField(condition.field);
  return definition?.kind === 'number'
    ? { ...condition, unit: condition.unit ?? definition.units?.[0]?.value }
    : { ...condition, values: condition.values ?? (condition.value?.trim() ? [condition.value] : []) };
}

export function saveExceptionCondition(condition: RuleCondition): RuleCondition {
  const definition = getExceptionField(condition.field);
  const base = { field: condition.field, operator: condition.operator };
  if (noValueOperators.has(condition.operator)) return base;
  if (definition?.kind === 'number') return { ...base, value: String(condition.value ?? '').trim(), unit: condition.unit ?? definition.units?.[0]?.value };
  return { ...base, values: (condition.values ?? (condition.value ? [condition.value] : [])).map(value => value.trim()).filter(Boolean) };
}

export function conditionUnitFactor(condition: RuleCondition): number | undefined {
  const units = getExceptionField(condition.field)?.units;
  if (!units) return condition.unit ? undefined : 1;
  return condition.unit ? units.find(unit => unit.value === condition.unit)?.factor : units[0].factor;
}

export function formatConditionValue(condition: RuleCondition): string {
  if (noValueOperators.has(condition.operator)) return '无需匹配值';
  if (getExceptionField(condition.field)?.kind === 'number') {
    const unit = condition.unit ?? getExceptionField(condition.field)?.units?.[0]?.value;
    return `${formatNumber(measurementNumber(condition.value))}${unit ? ` ${unit}` : ''}`;
  }
  return condition.values?.join(' / ') || condition.value || '—';
}

export interface ExceptionInput {
  typeName?: string;
  sourceCode?: string;
  errorMessage?: string;
  weightKg?: number | string | null;
  lengthMm?: number | string | null;
  widthMm?: number | string | null;
  heightMm?: number | string | null;
  barcodes?: string[];
  provider?: string;
  providerResponse?: string;
  responseStatusCode?: number | string | null;
}

/** Physical volume uses all three dimensions. Incomplete measurements never become a zero-volume parcel. */
export function exceptionFactsFromInput(input: ExceptionInput) {
  const typeIndex = Math.max(typeNames.indexOf(input.typeName ?? ''), typeCodes.indexOf(input.typeName ?? ''));
  const length = measurementNumber(input.lengthMm);
  const width = measurementNumber(input.widthMm);
  const height = measurementNumber(input.heightMm);
  const product = length !== undefined && width !== undefined && height !== undefined ? length * width * height : undefined;
  return {
    '包裹类型': typeIndex >= 0 ? [typeNames[typeIndex], typeCodes[typeIndex]] : input.typeName,
    '来源异常代码': input.sourceCode, '异常信息': input.errorMessage,
    '包裹重量': measurementNumber(input.weightKg),
    '包裹长度': length, '包裹宽度': width, '包裹高度': height,
    '包裹体积（长×宽×高）': product !== undefined && Number.isFinite(product) ? product : undefined,
    '条码': input.barcodes,
    'Provider 名称': input.provider, 'Provider 响应内容': input.providerResponse,
    '响应状态码': measurementNumber(input.responseStatusCode),
  };
}
