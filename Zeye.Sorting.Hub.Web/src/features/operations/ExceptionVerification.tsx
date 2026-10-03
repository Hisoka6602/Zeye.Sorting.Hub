import { Alert, Button, Collapse, Form, Input, InputNumber, Modal, Select } from 'antd';
import { useState } from 'react';
import { exceptionFactsFromInput, getExceptionField, measurementNumber, type ExceptionInput } from '../../data/exceptionConditions';
import { classifyException, matchesExceptionRule } from '../../data/exceptionRules';
import type { Rule } from '../../data/mock/operations';
import { formatNumber, formatNumericInput } from '../../data/formatNumber';

interface VerificationValues extends Omit<ExceptionInput, 'barcodes'> { barcodeText?: string }
const optionalMeasurement = { validator: async (_: unknown, value: unknown) => {
  if (value !== undefined && value !== null && value !== '' && measurementNumber(value) === undefined) throw new Error('请输入大于或等于 0 的有效数值');
} };

export function ExceptionVerification({ rules, previewRule, open, onClose }: { rules: Rule[]; previewRule?: Rule; open: boolean; onClose: () => void }) {
  const [result, setResult] = useState<{ rule: Rule; draftMatched?: boolean }>();
  const previewGroups = previewRule?.conditions?.map(condition => getExceptionField(condition.field)?.group) ?? [];
  const initialPanels = previewRule
    ? [...(previewGroups.includes('包裹数据') ? ['parcel'] : []), ...(previewGroups.includes('外部接口') ? ['provider'] : []), ...(previewGroups.includes('分拣机与处理信息') ? ['source'] : [])]
    : ['source'];
  const verify = (values: VerificationValues) => {
    const facts = exceptionFactsFromInput({ ...values, barcodes: values.barcodeText?.split(/\r?\n/).map(value => value.trim()).filter(Boolean) });
    const draftMatched = previewRule ? matchesExceptionRule(previewRule, facts) : undefined;
    setResult({ rule: draftMatched ? previewRule! : classifyException(rules, facts), draftMatched });
  };

  return <Modal title={previewRule ? '验证异常规则草稿' : '验证异常分类'} open={open} onCancel={onClose} destroyOnHidden zIndex={1200} width={720} centered rootClassName={`rule-verification-modal${result ? ' has-result' : ''}`} styles={{ body: { maxHeight: result ? 'calc(100dvh - 310px)' : 'calc(100dvh - 220px)', overflowY: 'auto' } }} footer={<div className="rule-verification-footer">
    <Button type="primary" htmlType="submit" form="exception-verification-form">{previewRule ? '验证草稿' : '验证分类'}</Button>
    {result && <Alert className="rule-verification-result" showIcon type={result.rule.systemRule === 'unknown-fallback' ? 'warning' : 'success'}
      message={`${result.draftMatched === true ? '草稿预览命中：' : result.draftMatched === false ? '草稿未命中，分类结果：' : '分类结果：'}${result.rule.targetType}`}
      description={result.draftMatched === true ? `命中草稿：${result.rule.name}。此验证不会发布规则。` : result.rule.systemRule === 'unknown-fallback' ? '所有已发布规则均未匹配，使用不可删除的系统兜底规则。原始异常代码和报文仍会保留。' : `命中已发布规则：${result.rule.name}`} />}
    <p className="rule-verification-note">使用当前已加载的服务器规则进行条件预览。正式处理还会校验产线范围；草稿发布后才参与实际分类。</p>
  </div>}>
    <p className="text-muted">{previewRule ? `验证“${previewRule.name}”的条件。命中仅作草稿预览；未命中则展示已发布规则的分类结果。` : '填写待分类的包裹或接口数据，按当前已发布规则顺序匹配；草稿不参与正式分类。'}</p>
    <Form id="exception-verification-form" layout="vertical" onValuesChange={() => setResult(undefined)} onFinish={verify}>
      <Collapse className="rule-verification-inputs" defaultActiveKey={initialPanels.length ? initialPanels : ['source']} items={[
        { key: 'source', label: '分拣机与异常信息', children: <>
          <Form.Item name="sourceCode" label="来源异常代码"><Input placeholder="例如 ParcelSpacingViolation" /></Form.Item>
          <Form.Item name="errorMessage" label="异常信息"><Input.TextArea rows={2} placeholder="可选，设备原始异常说明" /></Form.Item>
        </> },
        { key: 'parcel', label: '包裹重量、体积与条码', children: <>
          <Form.Item name="weightKg" label="包裹重量（kg）" rules={[optionalMeasurement]}><InputNumber stringMode formatter={formatNumericInput} min="0" placeholder="例如 20" /></Form.Item>
          <div className="rule-verification-dimensions">{(['lengthMm', 'widthMm', 'heightMm'] as const).map((name, index) => <Form.Item key={name} name={name} label={`${['长度', '宽度', '高度'][index]}（mm）`} rules={[optionalMeasurement]}><InputNumber stringMode formatter={formatNumericInput} min="0" placeholder="例如 100" /></Form.Item>)}</div>
          <Form.Item noStyle dependencies={['lengthMm', 'widthMm', 'heightMm']}>
            {({ getFieldsValue }) => {
              const volume = exceptionFactsFromInput(getFieldsValue())['包裹体积（长×宽×高）'];
              return <p className="rule-condition-hint">{volume === undefined ? '填写完整长宽高后计算物理体积；缺失数据不会按 0 比较。' : `计算体积：${formatNumber(volume / 1000)} cm³`}</p>;
            }}
          </Form.Item>
          <Form.Item name="typeName" label="包裹类型"><Select allowClear placeholder="选择当前包裹类型" options={['普通包裹', '大型包裹', '聚合包裹', '超薄包裹', '异形件', '流体包裹', '易碎品'].map(value => ({ value, label: value }))} /></Form.Item>
          <Form.Item name="barcodeText" label="条码（每行一条）"><Input.TextArea rows={2} placeholder="例如 SF001234567890，保留前导 0" /></Form.Item>
        </> },
        { key: 'provider', label: '外部接口 Provider 响应', children: <>
          <div className="rule-verification-provider"><Form.Item name="provider" label="Provider 名称"><Input placeholder="业务 Provider 标识" /></Form.Item><Form.Item name="responseStatusCode" label="响应状态码" rules={[optionalMeasurement]}><InputNumber stringMode precision={0} min="0" placeholder="例如 200、500" /></Form.Item></div>
          <Form.Item name="providerResponse" label="Provider 响应内容"><Input.TextArea rows={3} placeholder={'粘贴响应正文，例如 {"code":"ERROR","message":"无路由"}'} /></Form.Item>
        </> },
      ]} />
    </Form>
  </Modal>;
}
