import { initialExceptionRules, legacyExceptionRules, unknownExceptionRule, type Rule, type RuleCondition } from './mock/operations.ts';
import { conditionUnitFactor, getExceptionField, measurementNumber } from './exceptionConditions.ts';

/** Upgrade untouched examples while preserving user-created or edited rules. System rules remain canonical. */
export function normalizeExceptionRules(stored: Rule[]): Rule[] {
  const custom = stored.filter(rule => rule && Number.isFinite(rule.id)
    && !initialExceptionRules.some(seed => seed.id === rule.id)
    && rule.exceptionType !== 0 && rule.systemRule !== 'unknown-fallback'
    && !legacyExceptionRules.some(seed => JSON.stringify(seed) === JSON.stringify(rule))
  ).map(rule => ({ ...rule, systemRule: undefined }));
  return [...initialExceptionRules.filter(rule => rule.systemRule === 'sorter-protocol'), ...custom, unknownExceptionRule];
}

export type ExceptionFacts = Record<string, string | number | boolean | readonly string[] | null | undefined>;

function matchesCondition(condition: RuleCondition, facts: ExceptionFacts): boolean {
  const actual = facts[condition.field];
  const texts = (Array.isArray(actual) ? actual : actual == null ? [] : [String(actual)]).map(value => value.trim()).filter(Boolean);
  if (condition.operator === '为空') return !texts.length;
  if (condition.operator === '不为空') return Boolean(texts.length);
  // A missing fact cannot satisfy a value comparison, including "not equal".
  if (!texts.length) return false;
  const numeric = getExceptionField(condition.field)?.kind === 'number'
    || ['大于', '小于', '大于或等于', '小于或等于'].includes(condition.operator);
  if (numeric) {
    const value = measurementNumber(actual);
    const threshold = measurementNumber(condition.value);
    const factor = conditionUnitFactor(condition);
    if (value === undefined || threshold === undefined || factor === undefined) return false;
    const expected = threshold * factor;
    if (!Number.isFinite(expected)) return false;
    // Unit conversion can introduce rounding at an otherwise exact threshold.
    const equal = Math.abs(value - expected) <= Number.EPSILON * Math.max(1, Math.abs(value), Math.abs(expected)) * 8;
    switch (condition.operator) {
      case '等于': return equal;
      case '不等于': return !equal;
      case '大于': return value > expected && !equal;
      case '小于': return value < expected && !equal;
      case '大于或等于': return value > expected || equal;
      case '小于或等于': return value < expected || equal;
      default: return false;
    }
  }
  const expected = (condition.values ?? (condition.value ? [condition.value] : [])).map(value => value.trim()).filter(Boolean);
  if (!expected.length) return false;
  const normalize = (value: string) => condition.field === '来源异常代码' ? value.toLowerCase() : value;
  const normalized = texts.map(normalize);
  const targets = expected.map(normalize);
  const equals = normalized.some(value => targets.some(target => value === target));
  const contains = normalized.some(value => targets.some(target => value.includes(target)));
  switch (condition.operator) {
    case '等于': return equals;
    case '不等于': return !equals;
    case '包含': return contains;
    case '不包含': return !contains;
    default: return false;
  }
}

/** Draft preview uses the same condition matcher without publishing or mutating the rule. */
export function matchesExceptionRule(rule: Rule, facts: ExceptionFacts): boolean {
  return rule.exceptionType !== 0 && rule.systemRule !== 'unknown-fallback' && Boolean(rule.conditions?.length)
    && (rule.matchMode === 'any'
      ? rule.conditions!.some(condition => matchesCondition(condition, facts))
      : rule.conditions!.every(condition => matchesCondition(condition, facts)));
}

/** Only published rules run. The protected fallback always runs last, even if removed or reordered in storage. */
export function classifyException(rules: Rule[], facts: ExceptionFacts): Rule {
  return rules.find(rule => rule.status === '已发布' && matchesExceptionRule(rule, facts)) ?? unknownExceptionRule;
}
