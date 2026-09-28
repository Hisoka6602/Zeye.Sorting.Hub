import type { ReactNode } from 'react';

export function Field({ label, children, wide }: { label: string; children: ReactNode; wide?: boolean }) {
  return <div className={`filter-field ${wide ? 'filter-field-wide' : ''}`}><span className="filter-label">{label}</span>{children}</div>;
}
