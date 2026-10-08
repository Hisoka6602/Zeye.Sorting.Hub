import { useEffect, useRef, useState } from 'react';
import { requestHttpApi } from '../../data/api/client';
import type { ParcelTimingCandidate, ParcelTimingCandidates } from '../../data/api/parcelTimingTypes';
import { comparisonLimit, comparisonQueries } from './parcelComparisonModel';

type SearchBy = 'auto' | 'id' | 'barcode';
interface LookupRun { queries: string[]; index: number; ids: string[]; searchBy: SearchBy; notices: string[] }

/** 精确查询逐项解析；歧义必须人工选择，取消和卸载均阻止迟到响应修改选中集合。 */
export function useParcelComparisonLookup(selectedIds: string[], onApply: (ids: string[]) => void) {
  const controller = useRef<AbortController | null>(null);
  const run = useRef<LookupRun | null>(null);
  const [busy, setBusy] = useState(false);
  const [candidates, setCandidates] = useState<ParcelTimingCandidates | null>(null);
  const [activeQuery, setActiveQuery] = useState('');
  const [notices, setNotices] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const selectionKey = selectedIds.join(',');
  useEffect(() => {
    controller.current?.abort(); run.current = null; setBusy(false); setCandidates(null);
  }, [selectionKey]);
  useEffect(() => () => { controller.current?.abort(); run.current = null; }, []);
  const finish = (current: LookupRun) => {
    run.current = null; setBusy(false); setCandidates(null); setNotices(current.notices); onApply(current.ids);
  };
  const add = (current: LookupRun, parcel: ParcelTimingCandidate) => {
    if (current.ids.includes(parcel.id)) current.notices.push(`“${current.queries[current.index]}”已在对比中，未重复添加。`);
    else if (current.ids.length < comparisonLimit) current.ids.push(parcel.id);
  };
  const advance = async (current: LookupRun) => {
    setBusy(true); setCandidates(null); setError(null);
    const abort = new AbortController(); controller.current = abort;
    while (current.index < current.queries.length && !abort.signal.aborted && run.current === current) {
      if (current.ids.length >= comparisonLimit) { current.notices.push('已达到8票上限，其余输入未查询；移除包裹后可以继续添加。'); break; }
      const query = current.queries[current.index]; setActiveQuery(query);
      try {
        const result = await requestHttpApi<ParcelTimingCandidates>(`/api/parcels/timing/candidates?${new URLSearchParams({ query, searchBy: current.searchBy, pageNumber: '1' })}`, abort.signal);
        if (abort.signal.aborted || run.current !== current) return;
        if (!result.totalCount) current.notices.push(`未找到“${query}”对应的包裹。`);
        else if (result.totalCount === 1 && result.items[0]) add(current, result.items[0]);
        else { setCandidates(result); setBusy(false); return; }
      } catch (failure) {
        if (abort.signal.aborted || run.current !== current) return;
        current.notices.push(`“${query}”查询失败：${failure instanceof Error ? failure.message : '请重试'}`);
      }
      current.index++;
    }
    if (!abort.signal.aborted && run.current === current) finish(current);
  };
  const start = (text: string, searchBy: SearchBy) => {
    try {
      const queries = comparisonQueries(text);
      if (selectedIds.length >= comparisonLimit) throw new Error('最多对比8票，请先移除一票。');
      controller.current?.abort(); setNotices([]); setError(null);
      const current: LookupRun = { queries, index: 0, ids: [...selectedIds], searchBy, notices: [] };
      run.current = current; void advance(current);
    } catch (failure) { setError(failure instanceof Error ? failure.message : '请输入有效的查询条件。'); }
  };
  const choose = (parcel: ParcelTimingCandidate) => {
    const current = run.current; if (!current || busy) return;
    add(current, parcel); current.index++; void advance(current);
  };
  const skip = () => {
    const current = run.current; if (!current || busy) return;
    current.notices.push(`已跳过“${current.queries[current.index]}”。`); current.index++; void advance(current);
  };
  const cancel = () => {
    controller.current?.abort(); const current = run.current;
    if (current) { current.notices.push('已取消剩余查询，保留已确认的包裹。'); finish(current); }
  };
  const page = async (pageNumber: number) => {
    const current = run.current; if (!current) return;
    controller.current?.abort(); const abort = new AbortController(); controller.current = abort;
    setBusy(true); setError(null);
    try {
      const result = await requestHttpApi<ParcelTimingCandidates>(`/api/parcels/timing/candidates?${new URLSearchParams({ query: current.queries[current.index], searchBy: current.searchBy, pageNumber: String(pageNumber) })}`, abort.signal);
      if (!abort.signal.aborted && run.current === current) setCandidates(result);
    } catch (failure) { if (!abort.signal.aborted) setError(failure instanceof Error ? failure.message : '候选加载失败，请重试。'); }
    finally { if (!abort.signal.aborted) setBusy(false); }
  };
  return { busy, candidates, activeQuery, notices, error, start, choose, skip, cancel, page, resolving: Boolean(run.current), pendingIds: run.current?.ids ?? selectedIds };
}
