// Native Menu offsets measured from fixed source/native pixels.
// Evidence: qa/candidates/menu-alignment/font, 01 and final.
// Fixed-region label/icon regressions retain the prior metrics.
export interface NavigationAlignment {x: number; y: number; tracking: number; iconX?: number; iconY?: number; ink?: string; stroke?: number}

export const navigationAlignment: Record<string, Record<string, NavigationAlignment>> = {
  "/overview": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "包裹中心": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#4a618c",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#556793",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "ink": "#465b88",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": -1,
      "y": 1,
      "tracking": 0,
      "ink": "#475c8a"
    },
    "过期清理": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "ink": "#4a5b88",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -1.5,
      "tracking": 0,
      "iconX": -1,
      "iconY": -2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "慢查询": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#526793",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": -1,
      "tracking": -0.333,
      "ink": "#4f638e",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -2,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -3,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/parcels": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 2
    },
    "包裹中心": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "新建包裹": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#465891",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "ink": "#435989",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 0.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": -1,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -1,
      "tracking": -0.333
    },
    "可观测性": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": -1
    },
    "请求审计": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#4f6194",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#44598c",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -1,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -1.5
    }
  },
  "/parcels/:detail": {
    "工作台": {
      "x": 0,
      "y": -1,
      "tracking": -0.5,
      "iconX": 1.5,
      "iconY": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.667,
      "iconX": 1.5,
      "iconY": 1
    },
    "包裹台账": {
      "x": 4,
      "y": 0,
      "tracking": -0.667,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "新建包裹": {
      "x": 4,
      "y": 0,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 4,
      "y": -0.5,
      "tracking": -0.667
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": 1.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 3,
      "y": -0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 4,
      "y": 0,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.667,
      "iconX": 1,
      "iconY": -0.5
    },
    "请求审计": {
      "x": 4,
      "y": -0.5,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "慢查询": {
      "x": 4,
      "y": 0.5,
      "tracking": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 4,
      "y": 1,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 0.5,
      "iconY": -1
    }
  },
  "/parcels/new": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": -0.5,
      "iconX": -1,
      "iconY": 2
    },
    "包裹中心": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "批量入队": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": -1,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -1,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -2,
      "tracking": -0.333,
      "iconX": -1.5,
      "iconY": -2
    },
    "请求审计": {
      "x": 0,
      "y": -2,
      "tracking": -0.333
    },
    "慢查询": {
      "x": -1,
      "y": -2.5,
      "tracking": 0,
      "ink": "#455986",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": -3.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -6,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -7.5,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/parcels/batch": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 2
    },
    "包裹中心": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1.5
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 1.5,
      "tracking": 0,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "数据治理": {
      "x": 0,
      "y": -0.5,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "iconX": -1,
      "iconY": -2
    },
    "请求审计": {
      "x": 0,
      "y": -0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "慢查询": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -2.5,
      "tracking": 0,
      "iconX": -2,
      "iconY": -4,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/governance/parcel-cleanup": {
    "工作台": {
      "x": 0,
      "y": 1,
      "tracking": -0.5,
      "iconX": -0.5,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 1.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 0,
      "iconY": 0,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -2,
      "tracking": -0.333,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "可观测性": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333,
      "iconX": -0.5,
      "iconY": -2
    },
    "请求审计": {
      "x": 0,
      "y": -2,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 0,
      "y": -2.5,
      "tracking": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 1,
      "y": -2.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -4.5,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -6.5,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/audit/requests": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": -0.5,
      "iconX": -1,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 0.5,
      "tracking": 0
    },
    "数据治理": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -0.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": -1,
      "y": 0.5,
      "tracking": 0
    },
    "过期清理": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333
    },
    "可观测性": {
      "x": 0,
      "y": -2,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -2
    },
    "请求审计": {
      "x": 0,
      "y": -3,
      "tracking": -0.333,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "慢查询": {
      "x": 0,
      "y": -3,
      "tracking": -0.5
    },
    "系统健康": {
      "x": 0,
      "y": -4.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -6.5,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -7.5,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/audit/requests/:detail": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 2
    },
    "包裹中心": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 0.5,
      "tracking": 0
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333
    },
    "过期清理": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333
    },
    "可观测性": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": 0,
      "iconY": 0
    },
    "请求审计": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -1
    }
  },
  "/diagnostics/slow-queries": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 2,
      "iconY": 0.5
    },
    "包裹中心": {
      "x": 0,
      "y": -1,
      "tracking": -0.333,
      "iconX": 1.5,
      "iconY": 0.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 4,
      "y": 1,
      "tracking": -0.667
    },
    "新建包裹": {
      "x": 4,
      "y": 1,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 4,
      "y": 0.5,
      "tracking": -0.667
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": 1.5,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 4,
      "y": 0.5,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 4,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -2,
      "tracking": -0.333,
      "iconX": 1.5,
      "iconY": -1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 4,
      "y": -0.5,
      "tracking": -0.667
    },
    "慢查询": {
      "x": 4,
      "y": -0.5,
      "tracking": -0.5,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "系统健康": {
      "x": 5,
      "y": 0.5,
      "tracking": -1
    },
    "操作指南": {
      "x": 0,
      "y": 4,
      "tracking": -0.667,
      "iconX": 0.5,
      "iconY": 2.5
    }
  },
  "/diagnostics/slow-queries/:detail": {
    "工作台": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.5,
      "iconX": -0.5,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 1.5
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": -1,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -1,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 0,
      "y": 2,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -1
    }
  },
  "/governance/archive-tasks": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 0.5,
      "iconY": 1.5
    },
    "包裹中心": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 1,
      "y": -0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 1,
      "y": -0.5,
      "tracking": 0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 1,
      "y": -0.5,
      "tracking": 0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 1,
      "y": 0,
      "tracking": 0
    },
    "过期清理": {
      "x": 0,
      "y": 0,
      "tracking": 0
    },
    "可观测性": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "iconX": 0,
      "iconY": 0
    },
    "请求审计": {
      "x": 1,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "慢查询": {
      "x": 1,
      "y": 0.5,
      "tracking": 0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 2,
      "y": 0,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": 0.5,
      "tracking": 0,
      "iconX": -1,
      "iconY": -1,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/diagnostics/health": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 1.5,
      "iconY": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 1,
      "y": -1,
      "tracking": -0.333,
      "iconX": 1,
      "iconY": -1
    },
    "包裹台账": {
      "x": 4,
      "y": -3.5,
      "tracking": -0.667,
      "ink": "#53648f",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 4,
      "y": -3,
      "tracking": -0.333,
      "ink": "#4a5b89",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 4,
      "y": -3.5,
      "tracking": -0.667
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 1,
      "iconY": -1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 3,
      "y": -0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 3,
      "y": 2.5,
      "tracking": 0,
      "ink": "#475985",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 1,
      "y": 2.5,
      "tracking": -0.333,
      "iconX": 1,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 3,
      "y": 0.5,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 3,
      "y": 0.5,
      "tracking": 0,
      "ink": "#4f5f8c",
      "stroke": 0.1
    },
    "健康检查": {
      "x": 3,
      "y": 0.5,
      "tracking": -0.333
    },
    "操作指南": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": 0,
      "iconY": -2
    }
  },
  "/help": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": 1,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": 1,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 3,
      "y": -1.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 3,
      "y": -1,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 3,
      "y": -1.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "iconX": 1,
      "iconY": 2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 2,
      "y": 1.5,
      "tracking": -0.333
    },
    "过期清理": {
      "x": 3,
      "y": 3,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "iconX": 1,
      "iconY": -1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 3,
      "y": 1,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 3,
      "y": 2,
      "tracking": -0.5
    },
    "系统健康": {
      "x": 4,
      "y": 2.5,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "iconX": 0,
      "iconY": -1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "使用帮助": {
      "x": 3,
      "y": 0.5,
      "tracking": 0
    }
  },
  "/access": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 2
    },
    "包裹中心": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1.5
    },
    "包裹台账": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "ink": "#465b82",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 2,
      "tracking": -0.333,
      "ink": "#505e87",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 2,
      "tracking": 0,
      "ink": "#4b5f87",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 1,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": -1,
      "y": 2.5,
      "tracking": 0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#475883",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -1
    },
    "请求审计": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "慢查询": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#596791",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#4c618e",
      "stroke": 0.1
    },
    "系统管理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": -1.5,
      "ink": "#0068ff",
      "stroke": 0.25
    },
    "账号与权限": {
      "x": -1,
      "y": 0.5,
      "tracking": 0.25,
      "ink": "#0077ff",
      "stroke": 0.1
    },
    "系统配置": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作日志": {
      "x": 0,
      "y": -0.5,
      "tracking": 0,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "通知设置": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "ink": "#52617d",
      "stroke": 0.1
    }
  },
  "/operations/live": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -0.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 0,
      "y": 0.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 0,
      "y": -2,
      "tracking": -0.333
    },
    "慢查询": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "运行态势": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -2,
      "ink": "#455775",
      "stroke": 0.25
    },
    "实时运行": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333
    },
    "告警管理": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "运行报表": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": 0,
      "tracking": -0.333,
      "iconX": -2,
      "iconY": -0.5
    }
  },
  "/rules": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 0
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "可观测性": {
      "x": 1,
      "y": 1,
      "tracking": -0.667,
      "iconX": -1,
      "iconY": 1.5
    },
    "规则管理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "规则列表": {
      "x": 1,
      "y": -2.5,
      "tracking": -0.667,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "规则模板": {
      "x": 1,
      "y": -1.5,
      "tracking": -0.667,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "规则模拟": {
      "x": 1,
      "y": -3,
      "tracking": -0.333,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "执行日志": {
      "x": 1,
      "y": -2.5,
      "tracking": -0.333,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "系统管理": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.667,
      "iconX": -2,
      "iconY": -1.5
    },
    "操作指南": {
      "x": 0,
      "y": 3.5,
      "tracking": -0.667,
      "iconX": -2,
      "iconY": 2.5
    }
  },
  "/analytics": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": -0.5,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "数据治理": {
      "x": 0,
      "y": -1,
      "tracking": 0,
      "iconX": -1,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "可观测性": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0.5
    },
    "分析报表": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1,
      "iconY": 0.5,
      "ink": "#0066ff",
      "stroke": 0.25
    },
    "运营概览": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#0071ff"
    },
    "异常分析": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "时效分析": {
      "x": -1,
      "y": -1,
      "tracking": 0,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "产能分析": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#52617d",
      "stroke": 0.1
    },
    "操作指南": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -2,
      "iconY": -1.5
    }
  },
  "/governance/backup": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": -1.5,
      "iconX": 0,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 1,
      "y": -3,
      "tracking": -0.667,
      "iconX": -0.5,
      "iconY": -1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 1,
      "y": -1,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 1,
      "y": -1.5,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 1,
      "y": -3,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 0,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 1,
      "y": 1.5,
      "tracking": -0.333
    },
    "可观测性": {
      "x": 1,
      "y": 1,
      "tracking": -0.333,
      "iconX": -0.5,
      "iconY": 1.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 1,
      "y": 1,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "慢查询": {
      "x": 1,
      "y": 1.5,
      "tracking": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 1,
      "y": 1.5,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "操作指南": {
      "x": -1,
      "y": -1,
      "tracking": -0.333,
      "iconX": -1.5,
      "iconY": -1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "备份与恢复": {
      "x": -2,
      "y": 0.5,
      "tracking": 0,
      "iconX": -2,
      "iconY": 1,
      "ink": "#0066ff",
      "stroke": 0.25
    }
  },
  "/governance/sharding": {
    "工作台": {
      "x": -1,
      "y": 0.5,
      "tracking": -1,
      "iconX": -1,
      "iconY": 0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 1,
      "y": -0.5,
      "tracking": -1,
      "iconX": -1.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.25
    },
    "包裹台账": {
      "x": 1,
      "y": -1,
      "tracking": -1,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 1,
      "y": -1.5,
      "tracking": -1,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 1,
      "y": -2,
      "tracking": -1
    },
    "数据治理": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 0,
      "ink": "#455775",
      "stroke": 0.25
    },
    "归档任务": {
      "x": 0,
      "y": -1,
      "tracking": -0.667,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 1,
      "y": -2,
      "tracking": -1
    },
    "可观测性": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -1.5,
      "iconY": -0.5,
      "ink": "#455775",
      "stroke": 0.25
    },
    "请求审计": {
      "x": 0,
      "y": 0,
      "tracking": 0
    },
    "慢查询": {
      "x": 0,
      "y": -2,
      "tracking": -0.5,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": -5,
      "tracking": -0.667
    },
    "操作指南": {
      "x": -2,
      "y": 0,
      "tracking": -0.333,
      "iconX": -2.5,
      "iconY": -1.5,
      "ink": "#455775",
      "stroke": 0.25
    }
  },
  "/settings": {
    "工作台": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -0.5,
      "iconY": 1,
      "ink": "#455775",
      "stroke": 0.1
    },
    "包裹中心": {
      "x": 0,
      "y": 1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": 2
    },
    "包裹台账": {
      "x": 0,
      "y": 0,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "新建包裹": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "批量入队": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "数据治理": {
      "x": 0,
      "y": -0.5,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -0.5
    },
    "归档任务": {
      "x": -1,
      "y": 0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "过期清理": {
      "x": 0,
      "y": -2,
      "tracking": -0.333,
      "ink": "#455775",
      "stroke": 0.1
    },
    "可观测性": {
      "x": 0,
      "y": -1,
      "tracking": -0.333,
      "iconX": -1,
      "iconY": -2
    },
    "请求审计": {
      "x": 0,
      "y": -1.5,
      "tracking": -0.333
    },
    "慢查询": {
      "x": -1,
      "y": -0.5,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统健康": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#455775",
      "stroke": 0.1
    },
    "系统管理": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "iconX": -2,
      "iconY": -1.5
    },
    "系统配置": {
      "x": 0,
      "y": 0,
      "tracking": 0
    },
    "用户与权限": {
      "x": 0,
      "y": -1,
      "tracking": 0
    },
    "操作日志": {
      "x": 0,
      "y": 0,
      "tracking": 0
    },
    "系统升级": {
      "x": 0,
      "y": 0,
      "tracking": 0,
      "ink": "#52617d",
      "stroke": 0.1
    }
  }
};
