import { useSyncExternalStore } from 'react';

type ListUpdate<T> = T[] | ((current: T[]) => T[]);

/** A small persistent external store with a stable snapshot and focused subscription. */
export function createStoredList<T>(key: string, initial: T[], normalize: (items: T[]) => T[] = items => items) {
  let snapshot: T[] | undefined;
  let listening = false;
  const listeners = new Set<() => void>();

  const readStorage = (): T[] => {
    try {
      const value: unknown = JSON.parse(localStorage.getItem(key) || 'null');
      return normalize(Array.isArray(value) ? value as T[] : initial);
    } catch {
      return normalize(initial);
    }
  };

  const getSnapshot = () => snapshot ?? (snapshot = readStorage());
  const notify = () => listeners.forEach(listener => listener());
  const onStorage = (event: StorageEvent) => {
    if (event.key !== key && event.key !== null) return;
    snapshot = readStorage();
    notify();
  };
  const subscribe = (listener: () => void) => {
    if (!listening) {
      window.addEventListener('storage', onStorage);
      listening = true;
    }
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  };

  const update = (next: ListUpdate<T>) => {
    const current = getSnapshot();
    const value = typeof next === 'function' ? (next as (items: T[]) => T[])(current) : next;
    if (value === current) return;
    snapshot = normalize(value);
    try { localStorage.setItem(key, JSON.stringify(snapshot)); } catch { /* The demo still works when storage is unavailable. */ }
    notify();
  };

  return () => [useSyncExternalStore(subscribe, getSnapshot, () => initial), update] as const;
}
