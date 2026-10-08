import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseSync } from 'rolldown/utils';
import { localDateTimeFormat as displayFormat } from '../src/data/api/operationalTypes.ts';

/** 仅识别日期时间字段，耗时、纯日期和原始报文不属于此规则。 */
const timestampField = /(?:Time(?:Local)?|At(?:Local)?)$|^(?:time|modified|lastLogin|lastUpdated|createdBefore)$/;
/** 可见时间必须使用本地钟面和三位毫秒；不检查接口载荷的原始精度。 */
const timestampText = /\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?/g;
/** 共用格式常量在开发热编译时也必须保留三位毫秒。 */
const displayFormatPattern = /^Y{4}-M{2}-D{2} H{2}:m{2}:s{2}\.S{3}$/;
/** 可见内容及提示参与校验，机器属性和事件参数不属于展示值。 */
const visibleAttributes = new Set(['title', 'description', 'label', 'placeholder', 'aria-label', 'time', 'children']);
/** 深度遍历语法节点，跳过范围及注释等非语法对象。 */
function walk(node, visitor, parent) {
  if (!node || typeof node.type !== 'string') return;
  visitor(node, parent);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) for (const child of value) walk(child, visitor, node);
    else if (value && typeof value === 'object') walk(value, visitor, node);
  }
}

/** 统一读取标识符、静态属性及字符串键，支持可选链的解析结果。 */
function name(node) { return node?.name ?? (typeof node?.value === 'string' ? node.value : ''); }

/** 检查可见字面量，长小数或未经过统一格式化的整秒时间均需修正。 */
function invalidText(text) {
  return [...String(text).matchAll(timestampText)].some(match => !/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}$/.test(match[0]));
}

/** 解析源码中的真实渲染位置，避免注释、排序、协议报文和请求赋值误报。 */
export function inspectTimeDisplay(source, filename = 'component.tsx') {
  const parsed = parseSync(filename, source);
  if (parsed.errors.length) throw new Error(`${filename} 无法解析：${parsed.errors.map(error => error.message).join('；')}`);
  const nodes = [], parents = new Map(), bindings = new Map(), rawIdentifiers = new Set(), safeFunctions = new Set(), formatNames = new Set();
  walk(parsed.program, (node, parent) => { nodes.push(node); if (parent) parents.set(node, parent); });

  // 仅信任共享函数的实际导入，支持别名；局部变量继续沿定义追踪。
  for (const node of nodes) {
    if (node.type === 'ImportDeclaration') for (const specifier of node.specifiers) {
      const imported = name(specifier.imported);
      const moduleName = String(node.source.value).replace(/\.[jt]sx?$/, '');
      if (moduleName.endsWith('/operationalTypes')) {
        if (imported === 'localTime') safeFunctions.add(name(specifier.local));
        if (imported === 'localDateTimeFormat') formatNames.add(name(specifier.local));
      }
      if (moduleName.endsWith('/ParcelFacts') && imported === 'parcelFactValue') safeFunctions.add(name(specifier.local));
    }
    if (node.type === 'VariableDeclarator' && node.id.type === 'Identifier') bindings.set(node.id.name, node.init);
    if (node.type === 'VariableDeclarator' && node.id.type === 'ObjectPattern') for (const property of node.id.properties) {
      if (property.type === 'Property' && timestampField.test(name(property.key))) rawIdentifiers.add(name(property.value));
    }
  }

  /** 解析控件及格式化函数引用的静态格式，支持常量别名和静态模板。 */
  function stringValue(node, visited = new Set()) {
    if (!node || visited.has(node)) return undefined;
    visited.add(node);
    if (node.type === 'Literal' && typeof node.value === 'string') return node.value;
    if (node.type === 'Identifier') return stringValue(bindings.get(node.name), visited);
    if (node.type === 'TemplateLiteral' && !node.expressions.length) return node.quasis.map(part => part.value.cooked).join('');
    return undefined;
  }

  /** 原文代码区可包含来源时间的完整精度，嵌套高亮节点也保持原文。 */
  function inPayload(node) {
    for (let parent = node; parent; parent = parents.get(parent)) {
      if (parent.type === 'JSXElement' && ['pre', 'code'].includes(name(parent.openingElement.name))) return true;
    }
    return false;
  }

  /** 文本输入值属于可见内容，日期选择器的值由其统一格式控制。 */
  function visibleAttribute(attribute) {
    return visibleAttributes.has(name(attribute.name)) || name(attribute.name) === 'value' && ['input', 'textarea', 'Input'].includes(name(parents.get(attribute)?.name));
  }

  /** 共享格式化调用保护其输入；机械截断和局部替换不能替代显示规范。 */
  function normalizedCall(node) {
    return node.type === 'CallExpression' && (safeFunctions.has(name(node.callee)) ||
      node.callee.type === 'MemberExpression' && name(node.callee.property) === 'format' &&
      (formatNames.has(name(node.arguments[0])) || stringValue(node.arguments[0]) === displayFormat));
  }

  /** 本地展示函数可复用共享格式化，但所有返回分支均须保持显示规范。 */
  function normalizedOutput(node) {
    if (!node) return false;
    if (normalizedCall(node)) return true;
    if (node.type === 'Literal') return !invalidText(node.value);
    if (node.type === 'ConditionalExpression') return normalizedOutput(node.consequent) && normalizedOutput(node.alternate);
    return false;
  }
  for (let pass = 0; pass < bindings.size; pass++) {
    let changed = false;
    for (const [binding, initializer] of bindings) {
      if (!safeFunctions.has(binding) && initializer?.type === 'ArrowFunctionExpression' && normalizedOutput(initializer.body)) {
        safeFunctions.add(binding); changed = true;
      }
    }
    if (!changed) break;
  }

  /** 沿输出分支追踪原始时间，条件判断、属性名称及事件回调不作为可见值。 */
  function rawTime(node, parameters = new Set(), visited = new Set()) {
    if (!node || visited.has(node) || normalizedCall(node)) return null;
    visited.add(node);
    if (node.type.startsWith('TS') && !node.expression) return null;
    if (node.type === 'JSXElement' || node.type === 'JSXFragment') {
      if (node.type === 'JSXElement' && ['pre', 'code'].includes(name(node.openingElement.name))) return null;
      for (const attribute of node.openingElement?.attributes ?? []) if (visibleAttribute(attribute)) {
        const raw = rawTime(attribute.value?.expression ?? attribute.value, parameters, visited); if (raw) return raw;
      }
      for (const child of node.children) {
        const raw = rawTime(child.type === 'JSXExpressionContainer' ? child.expression : child, parameters, visited); if (raw) return raw;
      }
      return null;
    }
    if (node.type === 'BlockStatement') {
      for (const statement of node.body) {
        if (statement.type === 'VariableDeclaration') continue;
        const raw = rawTime(statement, parameters, visited); if (raw) return raw;
      }
      return null;
    }
    if (node.type === 'ConditionalExpression') return rawTime(node.consequent, parameters, visited) ?? rawTime(node.alternate, parameters, visited);
    if (node.type === 'LogicalExpression' && node.operator === '&&') return rawTime(node.right, parameters, visited);
    if (node.type === 'BinaryExpression' && !['+'].includes(node.operator)) return null;
    if (node.type === 'MemberExpression' && timestampField.test(name(node.property))) return node;
    if (node.type === 'Identifier') {
      if (parameters.has(node.name)) return node;
      const binding = bindings.get(node.name);
      return binding ? rawTime(binding, parameters, visited) : parameters.has(node.name) || rawIdentifiers.has(node.name) || timestampField.test(node.name) ? node : null;
    }
    if (node.type === 'CallExpression' && node.callee.type === 'MemberExpression' && name(node.callee.property) === 'format') {
      const format = stringValue(node.arguments[0]);
      if (typeof format === 'string' && /YYYY.*HH/.test(format) && format !== displayFormat) return node;
    }
    if (node.type === 'CallExpression' && node.callee.type === 'MemberExpression' && ['toISOString', 'toJSON', 'toLocaleString'].includes(name(node.callee.property))) {
      const receiver = node.callee.object;
      if (name(node.callee.property) === 'toISOString' || receiver.type === 'NewExpression' && name(receiver.callee) === 'Date') return node;
    }
    if (node.type === 'CallExpression' && node.callee.type === 'MemberExpression') {
      const receiver = rawTime(node.callee.object, parameters, visited); if (receiver) return receiver;
    }
    if (node.type === 'Literal' && typeof node.value === 'string' && invalidText(node.value)) return node;
    if (node.type === 'MemberExpression') return rawTime(node.object, parameters, visited) ?? (node.computed ? rawTime(node.property, parameters, visited) : null);
    for (const [key, value] of Object.entries(node)) {
      if (['callee', 'key', 'params', 'id', 'typeAnnotation'].includes(key)) continue;
      const children = Array.isArray(value) ? value : [value];
      for (const child of children) if (child && typeof child.type === 'string') {
        const result = rawTime(child, parameters, visited); if (result) return result;
      }
    }
    return null;
  }

  const failures = new Map();
  /** 报错包含源码行列，便于编译失败时定位具体页面和字段。 */
  function report(node, message) {
    const prefix = source.slice(0, node.start), lines = prefix.split('\n');
    const issue = { file: filename, line: lines.length, column: lines.at(-1).length + 1, message };
    failures.set(`${node.start}:${message}`, issue);
  }
  /** 对屏幕输出和可见提示执行同一校验。 */
  function check(node, parameters) { const raw = rawTime(node, parameters); if (raw) report(raw, '时间展示必须经过 localTime，最多三位毫秒（yyyy-MM-dd HH:mm:ss.fff）。'); }

  for (const node of nodes) {
    if (node.type.startsWith('JSX') && inPayload(node)) continue;
    if (node.type === 'JSXExpressionContainer') {
      const parent = parents.get(node);
      if (parent?.type === 'JSXAttribute') {
        if (visibleAttribute(parent)) check(node.expression);
      } else if (parent?.type === 'JSXFragment' || parent?.type === 'JSXElement' && !['pre', 'code'].includes(name(parent.openingElement.name))) check(node.expression);
    }
    if (node.type === 'JSXText' && invalidText(node.value)) report(node, '可见日期时间字面量必须使用空格分隔和三位毫秒。');
    if (node.type === 'JSXAttribute' && visibleAttribute(node) && node.value?.type === 'Literal' && invalidText(node.value.value)) report(node, '时间提示必须使用统一显示格式。');
    if (node.type === 'VariableDeclarator' && name(node.id) === 'localDateTimeFormat' && !displayFormatPattern.test(stringValue(node.init) ?? '')) report(node, '共享时间显示格式必须保持三位毫秒。');
    if (node.type === 'JSXAttribute' && name(node.name) === 'format' && /S{4,}|f{4,}/.test(stringValue(node.value?.expression ?? node.value) ?? '')) report(node, '禁止定义超过三位毫秒的时间格式。');
    if (node.type === 'Property' && name(node.key) === 'children') check(node.value);
    if (node.type === 'ObjectExpression') {
      const fields = new Map(node.properties.filter(property => property.type === 'Property').map(property => [name(property.key), property.value]));
      const dataIndex = fields.get('dataIndex');
      const index = dataIndex?.value ?? (dataIndex?.type === 'ArrayExpression' ? dataIndex.elements.at(-1)?.value : undefined);
      if (typeof index === 'string' && timestampField.test(index)) {
        const render = fields.get('render');
        if (!render) report(node, `时间列 ${index} 必须显式配置统一时间 render。`);
        else if (!safeFunctions.has(name(render))) {
          if (['ArrowFunctionExpression', 'FunctionExpression'].includes(render.type)) check(render.body, new Set(render.params.slice(0, 1).map(parameter => name(parameter.type === 'AssignmentPattern' ? parameter.left : parameter))));
          else report(render, `时间列 ${index} 的 render 必须使用统一格式化函数。`);
        }
      }
    }
    if (node.type === 'JSXOpeningElement' && (name(node.name) === 'DatePicker' || node.name.type === 'JSXMemberExpression' && name(node.name.object) === 'DatePicker')) {
      const attributes = new Map(node.attributes.filter(attribute => attribute.type === 'JSXAttribute').map(attribute => [name(attribute.name), attribute.value]));
      if (attributes.has('showTime') && attributes.get('showTime')?.expression?.value !== false) {
        const format = attributes.get('format');
        if (stringValue(format?.expression ?? format) !== displayFormat && !formatNames.has(name(format?.expression))) report(node, '日期时间控件必须配置 localDateTimeFormat。');
      }
    }
    if (node.type === 'CallExpression' && node.callee.type === 'MemberExpression' && name(node.callee.property) === 'format' && /S{4,}|f{4,}/.test(stringValue(node.arguments[0]) ?? '')) report(node, '禁止定义超过三位毫秒的时间格式。');
  }
  return [...failures.values()];
}

/** 遍历所有前端源码，包含未被当前路由引用的页面，避免遗漏演示及备用入口。 */
function sourceFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? sourceFiles(file) : /\.[jt]sx?$/.test(entry.name) ? [file] : [];
  });
}

/** 构建和命令行共用入口，任何违反时间显示规范的源码都会阻止产物生成。 */
export function assertTimeDisplay(root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')) {
  if (!displayFormatPattern.test(displayFormat)) throw new Error('共享时间显示格式必须为 yyyy-MM-dd HH:mm:ss.fff。');
  const failures = sourceFiles(path.join(root, 'src')).flatMap(file => inspectTimeDisplay(readFileSync(file, 'utf8'), path.relative(root, file)));
  if (failures.length) throw new Error(`前端时间显示守卫失败：\n${failures.map(issue => `${issue.file}:${issue.line}:${issue.column} ${issue.message}`).join('\n')}`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { assertTimeDisplay(process.argv[2]); console.log('前端时间显示守卫通过'); }
  catch (error) { console.error(error instanceof Error ? error.message : error); process.exitCode = 1; }
}
