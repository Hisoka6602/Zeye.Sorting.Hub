import type { CSSProperties } from "react";

// Business content positions measured against the source screenshots.
// The fixed shell and clickable breadcrumb use their common layout.
const frames: Record<string, { x: number; y: number; width: number }> = {
  "/parcels/batch": {
    "x": 0,
    "y": -4,
    "width": 17
  },
  "/overview": {
    "x": 0,
    "y": 0,
    "width": 0
  },
  "/audit/requests": {
    "x": -1,
    "y": 0,
    "width": 4
  },
  "/diagnostics/slow-queries": {
    "x": 17,
    "y": 6,
    "width": -0.143
  },
  "/governance/archive-tasks": {
    "x": 3,
    "y": -4,
    "width": 3
  },
  "/diagnostics/health": {
    "x": 21,
    "y": 3,
    "width": -19.991
  },
  "/help": {
    "x": -1,
    "y": -12,
    "width": 19.5714
  },
  "/access": {
    "x": -6,
    "y": -1.473,
    "width": 26
  },
  "/operations/live": {
    "x": 0,
    "y": -0.009,
    "width": 25.857
  },
  "/rules": {
    "x": 0,
    "y": 1.527,
    "width": 0
  },
  "/analytics": {
    "x": 0,
    "y": 0,
    "width": 0
  },
  "/governance/backup": {
    "x": 14,
    "y": 3,
    "width": 4.857
  },
  "/governance/sharding": {
    "x": 0,
    "y": -12,
    "width": 0
  },
  "/settings": {
    "x": 0,
    "y": 0,
    "width": 0
  },
  "/audit/requests/:id": {
    "x": -1,
    "y": -1,
    "width": 18.857
  },
  "/diagnostics/slow-queries/:id": {
    "x": -1,
    "y": -1,
    "width": 18.857
  }
};

export function contentFrameForPath(pathname: string): CSSProperties | undefined {
  const key = pathname.startsWith("/audit/requests/") ? "/audit/requests/:id"
    : pathname.startsWith("/diagnostics/slow-queries/") ? "/diagnostics/slow-queries/:id" : pathname;
  const frame = frames[key];
  return frame ? { "--content-offset-x": `${frame.x}px`, "--content-offset-y": `${frame.y}px`, "--content-width-correction": `${frame.width}px` } as CSSProperties : undefined;
}
