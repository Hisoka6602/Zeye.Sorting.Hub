import { useRef, useState } from 'react';
import type { Rule } from '../mock/operations';
import { requestApi } from './client';
import { useApiResource } from './useApiResource';
interface RuleSnapshot { revision: number; rules: Rule[] }
function normalize(snapshot: RuleSnapshot): RuleSnapshot {
  return { ...snapshot, rules: snapshot.rules.map(rule => ({ ...rule, id: Number(rule.id) })) };
}
/** 规则提交到数据库；版本冲突或网络错误时保留当前编辑内容。 */
export function useServerRules(category: 'parcel' | 'exception') {
  const resource = useApiResource<RuleSnapshot>('/api/operations/rules/' + category);
  const [saved, setSaved] = useState<RuleSnapshot>();
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const snapshot = resource.data && resource.data.revision >= (saved?.revision ?? -1) ? normalize(resource.data) : saved;
  const rules = snapshot?.rules ?? [];
  const commit = async (update: (current: Rule[]) => Rule[]) => {
    if (!snapshot || submitting.current) throw new Error('规则尚未加载或正在保存，请稍后重试');
    submitting.current = true; setBusy(true);
    try {
      const result = await requestApi<RuleSnapshot>('/api/operations/rules/' + category, undefined, { method: 'PUT', body: JSON.stringify({ expectedRevision: snapshot.revision, rules: update(rules) }) });
      setSaved(normalize(result)); resource.refresh();
    } finally { submitting.current = false; setBusy(false); }
  };
  return { rules, commit, busy, loading: resource.loading, error: resource.error, refresh: resource.refresh, loaded: Boolean(snapshot) };
}
