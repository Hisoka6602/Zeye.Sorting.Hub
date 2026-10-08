// 按请求方法和实际路由说明用途，历史记录无需重新写入即可显示中文说明。
const descriptions: Record<string, string> = {
  'GET /api/parcels/timing/candidates': '按包裹编号或完整条码查询时序候选，选择要追溯的包裹。',
  'GET /api/parcels/timing/compare': '读取选定多票包裹的重量、尺寸、体积及真实动作时序，比较量测差异和节点耗时。',
  'GET /api/parcels': '分页查询包裹台账，用于检索包裹及处理状态。',
  'GET /api/parcels/cursor': '通过游标分页查询包裹，连续浏览台账记录。',
  'GET /api/parcels/adjacent': '查询指定包裹的前后邻近记录，辅助定位过机顺序。',
  'GET /api/parcels/analytics': '查询包裹运营统计，用于数据概览与分析报表。',
  'GET /api/parcels/analysis': '按首次入库日期和来源分析包裹异常、完成耗时、DWS 获取、格口决策、分拣执行及各业务接口调用耗时，支持分布筛选和分页追溯。',
  'GET /api/parcels/dws-consistency': '比较同条码的 DWS 重复重量、物理体积和扫码耗时，合并接收和绑定记录，查看偏差排行、扫码 P95、来源对比和原始测量明细。',
  'GET /api/parcels/workbench': '按来源汇总滚动最近24小时的全部包裹，用于分拣工作台计数，不受明细分页上限限制。',
  'GET /api/parcels/processing-records/unbound': '查询未关联包裹的处理记录，排查数据绑定问题。',
  'POST /api/admin/parcels': '管理员手工创建测试包裹，用于验证包裹处理功能。',
  'POST /api/admin/parcels/batch-buffer': '管理员将测试包裹加入缓冲队列，验证异步批量写入。',
  'POST /api/admin/parcels/processing-records': '追加包裹处理记录，同步更新包裹当前状态。',
  'POST /api/admin/parcels/cleanup-expired': '验证当前用户登录密码，清理过期包裹并永久保存操作汇总和执行结果。',
  'GET /api/admin/parcels/cleanup-history': '查询永久清理操作历史，查看操作人、范围及执行结果。',
  'GET /api/access/session': '读取登录状态和用户权限，用于身份校验与页面访问控制。',
  'POST /api/access/bootstrap': '首次初始化管理员账号，并建立登录会话。',
  'POST /api/access/login': '校验账号和密码，建立用户登录会话。',
  'POST /api/access/logout': '结束当前登录会话，退出系统。',
  'GET /api/access': '查询用户与角色目录，用于账号和权限管理。',
  'POST /api/access/users': '新建或修改用户账号、角色、密码及启用状态。',
  'POST /api/access/roles': '新建或修改角色，配置角色拥有的权限。',
  'GET /api/access/profile': '读取当前用户的个人资料，用于个人中心展示。',
  'PUT /api/access/profile': '保存当前用户的名称、头像及其他个人资料。',
  'GET /api/access/profile/avatar': '读取当前用户的头像，用于账号菜单与个人中心展示。',
  'GET /api/operations/rules/parcel': '读取包裹分类规则，用于查看分类条件与标记动作。',
  'PUT /api/operations/rules/parcel': '保存包裹分类规则，更新分类条件、动作与发布状态。',
  'GET /api/operations/rules/exception': '读取异常分类规则，用于查看异常识别条件。',
  'PUT /api/operations/rules/exception': '保存异常分类规则，更新异常识别条件与发布状态。',
  'GET /api/operations/backup': '查询备份治理状态，核查最近备份与验证结果。',
  'GET /api/operations/backup/artifacts': '查询已有备份文件，用于下载和隔离恢复。',
  'POST /api/operations/backup/artifacts': '创建数据库备份文件，并核查备份状态。',
  'GET /api/operations/partitions': '查询物理分表与预建计划，用于分区管理。',
  'POST /api/operations/partitions/prebuild': '预建当前及未来窗口的包裹分表，为后续写入做准备。',
  'GET /api/operations/configuration': '读取当前生效配置，用于查看系统运行设置。',
  'GET /api/operations/configuration/runtime': '超级管理员读取运行配置原值及生效状态，查看配置版本、环境覆盖项和待重启参数。',
  'PUT /api/operations/configuration/runtime': '按已读取版本保存运行配置，校验修改内容并应用热更新；启动参数在下次启动生效。',
  'GET /api/operations/configuration/history': '超级管理员读取配置变更历史，追溯修改前后原值及提交状态。',
  'GET /api/operations/configuration/policy': '读取运维策略，用于查看自动备份、备份间隔和分表预建窗口。',
  'PUT /api/operations/configuration/policy': '保存运维策略，更新自动备份、备份间隔和分表预建窗口。',
  'GET /api/operations/configuration/fusion': '读取 Fusion 接入配置和工作台目录，用于管理来源登记与通信设置。',
  'PUT /api/operations/configuration/fusion': '保存 Fusion 接入配置，更新 Hub 身份与工作台发现设置。',
  'POST /api/operations/configuration/fusion/sources': '登记新的来源工作台，生成机器认证密钥和接入配对信息。',
  'GET /api/parcels/fusion/sources': '查询已登记来源工作台的在线、心跳和待确认状态。',
  'GET /api/diagnostics/fusion/facts': '查询 Fusion 上报的原始处理事实与投影结果，用于追溯包裹处理链路。',
  'GET /hubs/fusion-ingestion': '建立或读取 Fusion 工作台实时通道，接收处理事实、心跳及包裹图片。',
  'POST /hubs/fusion-ingestion': '通过 Fusion 实时通道提交工作台登记、处理事实、心跳或图片上传消息。',
  'DELETE /hubs/fusion-ingestion': '关闭 Fusion 工作台实时连接，结束当前传输会话。',
  'POST /hubs/fusion-ingestion/negotiate': '协商 Fusion 工作台的实时连接，确定连接标识与可用传输方式。',
  'GET /hubs/sorting': '建立或读取管理页面实时通道，接收业务数据及订阅更新。',
  'POST /hubs/sorting': '通过管理页面实时通道提交读取、订阅、包裹状态更新或处理记录消息。',
  'DELETE /hubs/sorting': '关闭管理页面实时连接，结束当前传输会话。',
  'POST /hubs/sorting/negotiate': '协商管理页面的实时连接，确定连接标识与可用传输方式。',
  'GET /api/data-governance/archive-tasks': '查询归档任务列表，查看任务状态与预演计划。',
  'POST /api/data-governance/archive-tasks': '创建归档预演任务，生成计划与审计摘要。',
  'GET /api/audit/web-requests': '查询请求审计日志，按时间、路径或追踪编号排查请求。',
  'GET /api/diagnostics/slow-queries': '查询慢查询统计，定位耗时较高的数据库访问。',
  'GET /health': '检查服务是否存活，用于兼容旧版健康探测。',
  'GET /health/live': '检查服务进程是否存活，供容器存活探测使用。',
  'GET /health/ready': '检查服务及数据库是否就绪，确认是否可接收请求。',
  'GET /health/deep': '执行深度健康诊断，核查备份、归档、消息及分片治理。',
};

const parameterizedDescriptions: [string, RegExp, string][] = [
  ['GET', /^\/api\/parcels\/timing\/-?\d+$/, '读取指定包裹及前后邻近包裹的真实动作时序，辅助排查处理顺序。'],
  ['GET', /^\/api\/admin\/parcels\/cleanup-history\/[^/]+$/, '读取清理操作汇总，追溯操作人、清理条件、执行数量和结果。'],
  ['GET', /^\/api\/parcels\/fusion\/images\/[^/]+\/content$/, '读取 Fusion 工作台上传的完整包裹图片，用于图片预览。'],
  ['PUT', /^\/api\/operations\/configuration\/fusion\/sources\/[^/]+$/, '更新已登记来源工作台的名称、身份配置及启用状态。'],
  ['POST', /^\/api\/operations\/configuration\/fusion\/sources\/[^/]+\/rotate-key$/, '轮换指定来源工作台的机器认证密钥，生成新的接入配对信息。'],
  ['GET', /^\/api\/parcels\/-?\d+\/images$/, '读取指定包裹的图片记录及预览地址，查看采集图片。'],
  ['GET', /^\/api\/parcels\/-?\d+$/, '按编号查询包裹详情，查看处理状态与测量信息。'],
  ['PUT', /^\/api\/admin\/parcels\/-?\d+$/, '更新指定包裹的完成、异常或接口请求状态。'],
  ['DELETE', /^\/api\/admin\/parcels\/-?\d+$/, '按编号删除指定包裹记录。'],
  ['GET', /^\/api\/audit\/web-requests\/-?\d+$/, '查询单次请求的审计详情，查看请求、响应及数据库访问。'],
  ['GET', /^\/api\/diagnostics\/slow-queries\/[^/]+$/, '查询指定慢查询的统计与 SQL 样本，辅助性能排查。'],
  ['POST', /^\/api\/data-governance\/archive-tasks\/-?\d+\/retry$/, '将已结束的归档预演任务重新入队执行。'],
  ['GET', /^\/api\/operations\/backup\/artifacts\/[^/]+\/download$/, '下载指定备份文件，用于备份留存或恢复准备。'],
  ['POST', /^\/api\/operations\/backup\/artifacts\/[^/]+\/restore-isolated$/, '将指定备份恢复到隔离数据库，验证备份可恢复性。'],
];

/** 说明接口的请求意图；执行是否成功仍由状态码表示。 */
export function describeAuditRequest(requestMethod: string, requestPath: string): string {
  const method = requestMethod.trim().toUpperCase();
  const path = requestPath.trim().split(/[?#]/, 1)[0].replace(/\/+$/, '').toLowerCase();
  const description = descriptions[`${method} ${path}`];
  if (description) return description;
  const parameterized = parameterizedDescriptions.find(([verb, route]) => verb === method && route.test(path));
  if (parameterized) return parameterized[2];
  if (method === 'OPTIONS') return '查询接口允许的请求方法与请求头，用于跨域预检。';
  if (method === 'HEAD') return '查询响应头与资源可用性，不读取响应正文。';
  return '未配置该接口的业务说明。';
}
