import { Button, Form, InputNumber, Select } from 'antd';
import { conditionOperators, createExceptionCondition, exceptionFieldOptions, getExceptionField, measurementNumber, noValueOperators } from '../../data/exceptionConditions';
import { formatNumericInput } from '../../data/formatNumber';

export function ExceptionConditionEditor({ name, index, canRemove, onRemove }: { name: number; index: number; canRemove: boolean; onRemove: () => void }) {
  const form = Form.useFormInstance();
  const watchedField = Form.useWatch(['conditions', name, 'field'], form);
  const field = watchedField ?? form.getFieldValue(['conditions', name, 'field']);
  const watchedOperator = Form.useWatch(['conditions', name, 'operator'], form);
  const operator = watchedOperator ?? form.getFieldValue(['conditions', name, 'operator']);
  const definition = getExceptionField(field);
  const needsValue = !noValueOperators.has(operator);
  const numeric = definition?.kind === 'number';

  const changeField = (nextField: string) => {
    const next = createExceptionCondition(nextField);
    form.setFields(['operator', 'value', 'values', 'unit'].map(key => ({
      name: ['conditions', name, key], value: next[key as keyof typeof next], errors: [],
    })));
  };

  return <div className="rule-condition rule-editor-condition">
    <div className="rule-editor-condition-heading"><strong>条件 {index + 1}</strong><Button type="text" size="small" disabled={!canRemove} aria-label={`删除条件 ${index + 1}`} onClick={onRemove}>删除</Button></div>
    <Form.Item name={[name, 'field']} label="匹配字段" rules={[{ required: true, message: '请选择匹配字段' }]}>
      <Select showSearch optionFilterProp="label" placeholder="选择重量、体积、条码或接口响应" options={exceptionFieldOptions} onChange={changeField} />
    </Form.Item>
    <Form.Item name={[name, 'operator']} label="运算符" rules={[{ validator: async (_, value) => {
      if (!conditionOperators(field).includes(value)) throw new Error('请选择适用于此字段的运算符');
    } }]}>
      <Select options={conditionOperators(field).map(value => ({ value, label: value }))} />
    </Form.Item>
    {needsValue ? numeric
      ? <div className="rule-threshold-row">
        <Form.Item name={[name, 'value']} label="阈值" rules={[{ validator: async (_, value) => {
          if (measurementNumber(value) === undefined) throw new Error('请输入大于或等于 0 的有效数值');
        } }]}>
          <InputNumber stringMode formatter={formatNumericInput} min="0" placeholder="例如 20" />
        </Form.Item>
        {definition.units && <Form.Item name={[name, 'unit']} label="单位" rules={[{ required: true, message: '请选择单位' }]}>
          <Select options={definition.units.map(unit => ({ value: unit.value, label: unit.value }))} />
        </Form.Item>}
      </div>
      : <Form.Item name={[name, 'values']} label="匹配内容" extra={`输入后按回车添加；${operator === '不包含' || operator === '不等于' ? '所有内容都不匹配时满足。' : '任意一项匹配即满足。'}`} rules={[{ validator: async (_, values: string[]) => {
        if (!values?.length || values.some(value => !value.trim())) throw new Error('请输入匹配内容并按回车添加');
      } }]}>
        <Select mode="tags" placeholder={definition?.placeholder ?? '输入内容后按回车'} maxTagTextLength={36} notFoundContent={null} />
      </Form.Item>
      : <p className="rule-condition-hint">此运算符无需匹配值。</p>}
    {field === '包裹体积（长×宽×高）' && <p className="rule-condition-hint">使用长 × 宽 × 高的物理体积，长宽高均有值时才参与比较。</p>}
  </div>;
}
