import type { CSSProperties } from 'react';

type TitleTypography = CSSProperties & {
  '--title-font-size'?: string;
  '--title-tracking'?: string;
  '--title-layout-width'?: string;
  '--title-offset-x'?: string;
  '--title-offset-y'?: string;
};

// Native glyph placement measured against the original 21 desktop titles.
// These values set actual text styles; no reference image pixels are rendered.
const pages: Record<string, TitleTypography> = {
  "/overview": {
    "fontFamily": "\"Microsoft YaHei\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "31.25px",
    "--title-tracking": "-1.24px",
    "--title-layout-width": "90.75px",
    "--title-offset-x": "2px",
    "--title-offset-y": "-1.775px"
  },
  "/parcels": {
    "fontFamily": "\"Microsoft YaHei\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "30.75px",
    "--title-tracking": "0.433px",
    "--title-layout-width": "124.402px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-2.112px"
  },
  "/parcels/:id": {
    "fontFamily": "\"Microsoft YaHei\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "32.25px",
    "--title-tracking": "2.17px",
    "--title-layout-width": "131.402px",
    "--title-offset-x": "0px",
    "--title-offset-y": "-2.887px"
  },
  "/parcels/new": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "31.5px",
    "--title-tracking": "-0.084px",
    "--title-layout-width": "123.607px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-3.775px"
  },
  "/parcels/batch": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "31px",
    "--title-tracking": "1.267px",
    "--title-layout-width": "126.402px",
    "--title-offset-x": "2px",
    "--title-offset-y": "-1px"
  },
  "/governance/parcel-cleanup": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "30.5px",
    "--title-tracking": "0.75px",
    "--title-layout-width": "185.089px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-2px"
  },
  "/audit/requests": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 600,
    "--title-font-size": "31.25px",
    "--title-tracking": "0.667px",
    "--title-layout-width": "124.991px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-3px"
  },
  "/audit/requests/:id": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "31.5px",
    "--title-tracking": "-0.084px",
    "--title-layout-width": "123.607px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-2.775px"
  },
  "/diagnostics/slow-queries": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "32.25px",
    "--title-tracking": "1.02px",
    "--title-layout-width": "163.25px",
    "--title-offset-x": "0px",
    "--title-offset-y": "-0.775px"
  },
  "/diagnostics/slow-queries/:id": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "31px",
    "--title-tracking": "0.1px",
    "--title-layout-width": "154.25px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-2px"
  },
  "/governance/archive-tasks": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "31px",
    "--title-tracking": "-0.411px",
    "--title-layout-width": "121.589px",
    "--title-offset-x": "0px",
    "--title-offset-y": "-0.775px"
  },
  "/governance/outbox": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 600,
    "--title-font-size": "32px",
    "--title-tracking": "-0.618px",
    "--title-layout-width": "180px",
    "--title-offset-x": "-2px",
    "--title-offset-y": "-6.55px"
  },
  "/diagnostics/health": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 700,
    "--title-font-size": "33px",
    "--title-tracking": "0.555px",
    "--title-layout-width": "132.196px",
    "--title-offset-x": "-2px",
    "--title-offset-y": "2.225px"
  },
  "/help": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "31px",
    "--title-tracking": "0.183px",
    "--title-layout-width": "123.402px",
    "--title-offset-x": "0px",
    "--title-offset-y": "-1px"
  },
  "/access": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 600,
    "--title-font-size": "32.5px",
    "--title-tracking": "-0.767px",
    "--title-layout-width": "160.5px",
    "--title-offset-x": "-1px",
    "--title-offset-y": "-4.775px"
  },
  "/operations/live": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 600,
    "--title-font-size": "31px",
    "--title-tracking": "-0.043px",
    "--title-layout-width": "183.884px",
    "--title-offset-x": "1px",
    "--title-offset-y": "-5.775px"
  },
  "/rules": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "31.5px",
    "--title-tracking": "-1.062px",
    "--title-layout-width": "121px",
    "--title-offset-x": "-1px",
    "--title-offset-y": "-4.775px"
  },
  "/analytics": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 720,
    "--title-font-size": "30.25px",
    "--title-tracking": "1.166px",
    "--title-layout-width": "122.991px",
    "--title-offset-x": "-2px",
    "--title-offset-y": "-1px"
  },
  "/governance/backup": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "33px",
    "--title-tracking": "0.323px",
    "--title-layout-width": "165.991px",
    "--title-offset-x": "0px",
    "--title-offset-y": "-0.775px"
  },
  "/governance/sharding": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "31.75px",
    "--title-tracking": "0.933px",
    "--title-layout-width": "129.402px",
    "--title-offset-x": "0px",
    "--title-offset-y": "0px"
  },
  "/settings": {
    "fontFamily": "\"Hub Noto Sans SC\", \"Microsoft YaHei UI\", sans-serif",
    "fontWeight": 675,
    "--title-font-size": "32px",
    "--title-tracking": "1.05px",
    "--title-layout-width": "128.205px",
    "--title-offset-x": "-1px",
    "--title-offset-y": "-1px"
  }
};

export function titleTypographyForPath(pathname: string): TitleTypography {
  const key = /^\/parcels\/(?!new$|batch$)/.test(pathname) ? '/parcels/:id'
    : pathname.startsWith('/audit/requests/') ? '/audit/requests/:id'
      : pathname.startsWith('/diagnostics/slow-queries/') ? '/diagnostics/slow-queries/:id' : pathname;
  return pages[key] ?? { fontFamily: "'Hub Noto Sans SC', 'Microsoft YaHei UI', sans-serif", fontWeight: 600 };
}
