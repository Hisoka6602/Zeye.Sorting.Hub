import type { CSSProperties } from "react";

type ContentTypography = { intro?: CSSProperties; sections: Record<string, CSSProperties> };

// Native caption and heading fonts verified in actual page screenshots.
// Unlisted text inherits the existing component typography.
const pages: Record<string, ContentTypography> = {
  "/overview": {
    "sections": {
      "失败的 Outbox 消息": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      },
      "最近归档试运行任务": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16,
      "letterSpacing": 0.235,
      "lineHeight": 24
    }
  },
  "/parcels": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450
    }
  },
  "/parcels/:id": {
    "sections": {
      "基本信息": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450
    }
  },
  "/parcels/new": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450
    }
  },
  "/parcels/batch": {
    "sections": {
      "预览与校验结果": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    }
  },
  "/governance/parcel-cleanup": {
    "sections": {
      "清理条件": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      },
      "结果预览": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16,
      "letterSpacing": -0.031,
      "lineHeight": 24
    }
  },
  "/audit/requests": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16,
      "letterSpacing": 0.529,
      "lineHeight": 24
    }
  },
  "/audit/requests/:id": {
    "sections": {
      "基本信息": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      },
      "请求处理链路": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      },
      "关键字段": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 15.75,
      "letterSpacing": 0,
      "lineHeight": 24
    }
  },
  "/diagnostics/slow-queries": {
    "sections": {
      "当前快照内筛选": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      }
    },
    "intro": {
      "fontSize": 16.75,
      "letterSpacing": 0,
      "lineHeight": 24
    }
  },
  "/diagnostics/slow-queries/:id": {
    "sections": {
      "当前内存观测窗口": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    }
  },
  "/governance/outbox": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16,
      "letterSpacing": -0.308,
      "lineHeight": 24
    }
  },
  "/diagnostics/health": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 18.75,
      "letterSpacing": 0,
      "lineHeight": 27
    }
  },
  "/help": {
    "sections": {
      "包裹台账": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      },
      "新建包裹": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      },
      "慢查询": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 600
      },
      "详细说明": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 14.125,
      "letterSpacing": 0.671,
      "lineHeight": 22.5
    }
  },
  "/access": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16.375,
      "letterSpacing": 0,
      "lineHeight": 24
    }
  },
  "/operations/live": {
    "sections": {
      "事件动态": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 14.875,
      "letterSpacing": 0,
      "lineHeight": 24
    }
  },
  "/rules": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16,
      "letterSpacing": -0.926,
      "lineHeight": 24
    }
  },
  "/analytics": {
    "sections": {
      "按工作台分布": {
        "fontFamily": "\"Microsoft YaHei\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 700
      }
    },
    "intro": {
      "fontSize": 15.25,
      "letterSpacing": 0.36,
      "lineHeight": 24
    }
  },
  "/governance/backup": {
    "sections": {},
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16.25,
      "letterSpacing": 0.119,
      "lineHeight": 24
    }
  },
  "/governance/sharding": {
    "sections": {
      "历史分区列表": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450,
      "fontSize": 16,
      "letterSpacing": 0.239,
      "lineHeight": 24
    }
  },
  "/settings": {
    "sections": {
      "配置来源与生效方式": {
        "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
        "fontWeight": 675
      }
    },
    "intro": {
      "fontFamily": "\"Hub Noto Sans SC Content\", \"Microsoft YaHei UI\", sans-serif",
      "fontWeight": 450
    }
  }
};

export function contentTypographyForPath(pathname: string, surface: "intro" | "section", title = ""): CSSProperties | undefined {
  const key = /^\/parcels\/(?!new$|batch$)/.test(pathname) ? "/parcels/:id"
    : pathname.startsWith("/audit/requests/") ? "/audit/requests/:id"
      : pathname.startsWith("/diagnostics/slow-queries/") ? "/diagnostics/slow-queries/:id" : pathname;
  return surface === "intro" ? pages[key]?.intro : pages[key]?.sections[title];
}
