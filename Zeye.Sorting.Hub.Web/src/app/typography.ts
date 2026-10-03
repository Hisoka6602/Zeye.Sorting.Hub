/** 所有页面及 Ant Design 组件共用的文字层级。 */
export const typography = {
  fontFamily: "'Hub Noto Sans SC Content', 'Hub Noto Sans SC', 'Microsoft YaHei UI', 'Microsoft YaHei', 'PingFang SC', 'Segoe UI', sans-serif",
  color: { heading: '#13213f', body: '#263957', secondary: '#61708d', muted: '#74829c' },
  size: { page: 30, mobilePage: 26, section: 18, item: 16, body: 15, label: 14, caption: 13, chart: 12, metric: 32, compactMetric: 24 },
  weight: { regular: 400, semibold: 600, bold: 700 },
} as const;

/** 在根节点提供 CSS 变量，抽屉和弹窗也能继承同一套样式。 */
export const typographyCssVariables: Record<string, string> = {
  '--hub-font-family': typography.fontFamily,
  ...Object.fromEntries(Object.entries(typography.color).map(([name, value]) => [`--hub-text-${name}`, value])),
  ...Object.fromEntries(Object.entries(typography.size).map(([name, value]) => [`--hub-font-${name.replace(/[A-Z]/g, letter => `-${letter.toLowerCase()}`)}`, `${value}px`])),
  ...Object.fromEntries(Object.entries(typography.weight).map(([name, value]) => [`--hub-weight-${name}`, String(value)])),
};
