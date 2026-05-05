# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .github（Copilot 仓库级指令目录）
│   ├── DDD分层接口与实现放置规范.md（DDD 分层接口定义与实现放置规范：依赖方向约束、接口归属原则、禁止项清单）
│   ├── copilot-instructions.md（Copilot 自定义指令：禁止 UTC、统一本地时间）
│   ├── scripts（CI 校验脚本目录）
│   │   └── validate-copilot-rules.sh（Copilot 限制规则校验脚本：从 copilot-instructions.md 解析规则并执行自动校验）
│   └── workflows（CI 工作流目录）
│       ├── copilot-instructions-validation.yml（Copilot 限制规则 PR 校验流水线：每次 PR 运行规则校验脚本）
│       ├── ef-migration-validation.yml（EF 迁移验收流水线：MySQL+SQL Server 双 Provider 执行 dotnet ef list/update/script）
│       └── stability-gates.yml（长期运行稳定性门禁：构建+测试、配置合法性、隔离器边界、回滚资产、健康探针端点、契约兼容性、蓝绿部署验证、演练记录（强制阻断）、分表预建校验、迁移归档验证共 10 项门禁）
├── .gitattributes（Git 属性配置）
├── .gitignore（Git 忽略规则）
├── 待完善事项.md（待完善事项列表，仅记录代码中尚未实现的可完善点）
├── 更新记录.md（更新记录，按时间倒序记录每次 PR 更新内容）
├── README.md（仓库总览、结构清单与维护规范）
├── 长期运行优化与热更新支持清单.md（面向一年无人值守运行的优化与热更新治理清单）
├── drill-records/（季度/年度稳定性演练记录目录，供演练记录门禁检查；每次演练后在此新增记录文件）
├── 检查台账/（逐文件全量审查台账目录；存放文件清单基线与各批次检查结果，按 PR-A/B/C… 分批递增）
│   ├── 文件清单基线.txt（全仓库受版本控制文件基线清单，由 git ls-files 生成，作为防遗漏对账基准）
│   ├── PR-A-检查台账.md（PR-A 批次检查台账：覆盖根目录、.github/、文档与脚本共 21 个文件的审查结论与问题清单）
│   ├── PR-B-检查台账.md（PR-B 批次检查台账：覆盖 Domain 层共 67 个文件的审查结论与问题清单）
│   ├── PR-C-检查台账.md（PR-C 批次检查台账：覆盖 Application + Contracts 层共 45 个文件的审查结论与问题清单）
│   ├── PR-D-检查台账.md（PR-D 批次检查台账：覆盖 Infrastructure 层共 63 个文件的审查结论与问题清单）
│   ├── PR-E-检查台账.md（PR-E 批次检查台账：覆盖 Host 层共 43 个文件的审查结论与问题清单）
│   ├── PR-F-检查台账.md（PR-F 批次检查台账：覆盖 SharedKernel + Host.Tests + 占位子域共 45 个文件的审查结论与问题清单）
│   ├── PR-长期数据库底座A-检查台账.md（长期数据库底座 PR-A 台账：记录现状核对、数据库连接诊断切片交付与下一 PR 入口）
│   ├── PR-长期数据库底座B-检查台账.md（长期数据库底座 PR-B 台账：记录查询保护、游标分页交付与下一 PR 入口）
│   ├── PR-长期数据库底座C-检查台账.md（长期数据库底座 PR-C 台账：记录批量缓冲写入、死信隔离交付与下一 PR 入口）
│   └── PR-长期数据库底座D-检查台账.md（长期数据库底座 PR-D 台账：记录分表巡检、预建计划、索引检查与下一 PR 入口）
├── Zeye.Sorting.Hub.Analytics（分析与报表子域，占位工程）
│   └── Zeye.Sorting.Hub.Analytics.csproj（Analytics 项目定义）
├── Zeye.Sorting.Hub.Application（应用层）
│   ├── Services（应用服务目录）
│   │   ├── AuditLogs（审计日志应用服务目录）
│   │   │   ├── GetWebRequestAuditLogByIdQueryService.cs（Web 请求审计日志详情查询应用服务）
│   │   │   ├── GetWebRequestAuditLogPagedQueryService.cs（Web 请求审计日志分页查询应用服务）
│   │   │   ├── WebRequestAuditLogContractMapper.cs（Web 请求审计日志读模型到 Contracts 映射器）
│   │   │   └── WriteWebRequestAuditLogCommandService.cs（Web 请求审计日志最小写入应用服务入口）
│   │   └── Parcels（Parcel 查询应用服务目录）
│   │       ├── CleanupExpiredParcelsCommandService.cs（过期包裹清理应用服务（治理型，调用仓储隔离器，不可绕过））
│   │       ├── CreateParcelCommandService.cs（管理端新增包裹应用服务）
│   │       ├── DeleteParcelCommandService.cs（管理端删除单个包裹应用服务）
│   │       ├── GetAdjacentParcelsQueryService.cs（Parcel 邻近查询应用服务）
│   │       ├── GetParcelByIdQueryService.cs（Parcel 详情查询应用服务）
│   │       ├── GetParcelPagedQueryService.cs（Parcel 分页查询应用服务）
│   │       ├── GetParcelCursorPagedQueryService.cs（Parcel 游标分页查询应用服务）
│   │       ├── ParcelContractMapper.cs（Parcel 领域模型到 Contracts 模型映射器）
│   │       ├── ParcelQueryRequestMapper.cs（Parcel 查询请求映射器：统一默认时间窗口与过滤模型构建）
│   │       └── UpdateParcelStatusCommandService.cs（管理端更新包裹状态应用服务（仅支持领域允许的状态转换））
│   ├── Utilities（应用层内部共享工具目录）
│   │   ├── EnumGuard.cs（枚举值合法性校验工具：统一封装 Enum.IsDefined + Warn 日志 + 异常抛出）
│   │   └── Guard.cs（基础参数边界守卫工具：ThrowIfZeroOrNegative / ThrowIfNegative，消除各服务重复检查代码）
│   ├── Mappers/Parcels（应用层 Parcel 映射目录）
│   │   └── ParcelCreateRequestMapper.cs（Parcel 新增请求映射器：统一同步新增与缓冲写入的聚合构建）
│   ├── Services/WriteBuffers（批量缓冲写入应用抽象目录）
│   │   ├── BufferedWriteOptions.cs（批量缓冲写入配置模型）
│   │   ├── BufferedWriteResult.cs（批量缓冲写入结果模型）
│   │   └── IBufferedWriteService.cs（批量缓冲写入服务契约）
│   └── Zeye.Sorting.Hub.Application.csproj（Application 项目定义）
├── Zeye.Sorting.Hub.Contracts（契约层）
│   ├── Enums（契约层枚举目录）
│   │   └── Parcels（Parcel 枚举目录）
│   │       ├── ParcelExceptionType.cs（包裹异常类型对外合同枚举：与 Domain.ParcelExceptionType 数值一一对应，供 API 客户端按语义筛选）
│   │       └── ParcelUpdateOperation.cs（Parcel 更新操作类型枚举：MarkCompleted/MarkSortingException/UpdateRequestStatus）
│   ├── Models（对外合同模型目录）
│   │   ├── AuditLogs（审计日志合同目录）
│   │   │   └── WebRequests（Web 请求审计日志合同目录）
│   │   │       ├── WebRequestAuditLogDetailResponse.cs（Web 请求审计日志详情响应合同）
│   │   │       ├── WebRequestAuditLogListItemResponse.cs（Web 请求审计日志列表项响应合同）
│   │   │       ├── WebRequestAuditLogListRequest.cs（Web 请求审计日志列表查询请求合同）
│   │   │       └── WebRequestAuditLogListResponse.cs（Web 请求审计日志列表分页响应合同）
│   │   └── Parcels（Parcel 合同目录）
│   │       ├── Admin（管理端写接口合同目录）
│   │       │   ├── ParcelBatchBufferedCreateRequest.cs（Parcel 批量缓冲写入请求合同）
│   │       │   ├── ParcelBatchBufferedCreateResponse.cs（Parcel 批量缓冲写入响应合同）
│   │       │   ├── ParcelCleanupExpiredRequest.cs（过期清理治理接口请求合同）
│   │       │   ├── ParcelCleanupExpiredResponse.cs（过期清理治理接口响应合同（含决策/计划量/执行量/补偿边界））
│   │       │   ├── ParcelCreateRequest.cs（管理端新增包裹请求合同）
│   │       │   └── ParcelUpdateRequest.cs（管理端更新包裹状态请求合同）
│   │       ├── ParcelAdjacentRequest.cs（Parcel 邻近查询请求合同）
│   │       ├── ParcelAdjacentResponse.cs（Parcel 邻近查询响应合同）
│   │       ├── ParcelCursorListRequest.cs（Parcel 游标分页查询请求合同）
│   │       ├── ParcelCursorListResponse.cs（Parcel 游标分页查询响应合同）
│   │       ├── ParcelCursorToken.cs（Parcel 游标令牌合同：编码上一页最后一条记录的 ScannedTime/Id）
│   │       ├── ParcelDetailResponse.cs（Parcel 详情响应合同（包含所有联表值对象内容））
│   │       ├── ParcelListItemResponse.cs（Parcel 列表项响应合同）
│   │       ├── ParcelListRequest.cs（Parcel 列表查询请求合同）
│   │       ├── ParcelListResponse.cs（Parcel 列表分页响应合同）
│   │       └── ValueObjects（Parcel 值对象响应合同目录）
│   │           ├── ApiRequestInfoResponse.cs（外部接口请求记录响应合同）
│   │           ├── BagInfoResponse.cs（集包信息响应合同）
│   │           ├── BarCodeInfoResponse.cs（条码明细响应合同）
│   │           ├── ChuteInfoResponse.cs（格口信息响应合同）
│   │           ├── CommandInfoResponse.cs（通信指令记录响应合同）
│   │           ├── GrayDetectorInfoResponse.cs（灰检信息响应合同）
│   │           ├── ImageInfoResponse.cs（图片信息响应合同）
│   │           ├── ParcelDeviceInfoResponse.cs（包裹设备信息响应合同）
│   │           ├── ParcelPositionInfoResponse.cs（包裹坐标信息响应合同）
│   │           ├── SorterCarrierInfoResponse.cs（小车信息响应合同）
│   │           ├── StickingParcelInfoResponse.cs（叠包信息响应合同）
│   │           ├── VideoInfoResponse.cs（视频信息响应合同）
│   │           ├── VolumeInfoResponse.cs（体积信息响应合同）
│   │           └── WeightInfoResponse.cs（称重明细响应合同）
│   └── Zeye.Sorting.Hub.Contracts.csproj（Contracts 项目定义）
├── Zeye.Sorting.Hub.Domain（核心领域层）
│   ├── Abstractions（领域抽象接口目录）
│   │   ├── IEntity.cs（实体通用接口）
│   │   └── IParcelSummaryView.cs（Parcel 摘要视图接口，供 Application 层映射器统一消费聚合根与读模型）
│   ├── Aggregates（领域聚合目录）
│   │   ├── AuditLogs（审计日志聚合目录）
│   │   │   └── WebRequests（Web 请求审计聚合目录）
│   │   │       ├── WebRequestAuditLog.cs（Web 请求审计热数据聚合根）
│   │   │       └── WebRequestAuditLogDetail.cs（Web 请求审计冷数据详情实体）
│   │   └── Parcels（包裹聚合目录）
│   │       ├── Parcel.cs（包裹聚合根）
│   │       └── ValueObjects（包裹聚合值对象目录）
│   │           ├── ApiRequestInfo.cs（外部接口请求/响应信息值对象）
│   │           ├── BagInfo.cs（袋笼/集包信息值对象）
│   │           ├── BarCodeInfo.cs（条码识别信息值对象）
│   │           ├── ChuteInfo.cs（格口分配信息值对象）
│   │           ├── CommandInfo.cs（设备命令交互信息值对象）
│   │           ├── GrayDetectorInfo.cs（灰度检测结果值对象）
│   │           ├── ImageInfo.cs（图片元数据值对象）
│   │           ├── ParcelDeviceInfo.cs（包裹相关设备信息值对象）
│   │           ├── ParcelPositionInfo.cs（包裹空间/轨迹位置信息值对象）
│   │           ├── SorterCarrierInfo.cs（分拣小车/载体信息值对象）
│   │           ├── StickingParcelInfo.cs（叠包检测结果值对象）
│   │           ├── VideoInfo.cs（视频信息值对象）
│   │           ├── VolumeInfo.cs（体积信息值对象）
│   │           └── WeightInfo.cs（重量信息值对象）
│   ├── Events（领域事件载荷目录）
│   │   └── Parcels（包裹相关领域事件载荷目录）
│   │       ├── ParcelChuteAssignedEventArgs.cs（包裹分配格口事件载荷，携带 ParcelId/TargetChuteId/ActualChuteId/ScannedTime 业务字段）
│   │       └── ParcelScannedEventArgs.cs（包裹扫描事件载荷，携带 ParcelId/BarCodes/WorkstationName/ScannedTime/BagCode/TargetChuteId 业务字段）
│   ├── Enums（领域枚举目录）
│   │   ├── ActionType.cs（动作类型枚举）
│   │   ├── ActionIsolationDecision.cs（自动调优危险动作隔离决策枚举）
│   │   ├── ApiRequestStatus.cs（接口请求状态枚举）
│   │   ├── ApiRequestType.cs（接口请求类型枚举）
│   │   ├── AutoTuningClosedLoopStage.cs（自动调优闭环阶段枚举）
│   │   ├── AutoTuningUnavailableReason.cs（自动调优 unavailable 原因枚举与标签扩展）
│   │   ├── BarCodeType.cs（条码类型枚举）
│   │   ├── CommandDirection.cs（命令方向枚举）
│   │   ├── ImageCaptureType.cs（图像采集方式枚举）
│   │   ├── ImageType.cs（图像类型枚举）
│   │   ├── NoReadType.cs（无码/难码类型枚举）
│   │   ├── ParcelExceptionType.cs（包裹异常类型枚举）
│   │   ├── ParcelStatus.cs（包裹状态枚举）
│   │   ├── ParcelType.cs（包裹类别枚举）
│   │   ├── VideoNodeType.cs（视频节点类型枚举）
│   │   ├── VolumeSourceType.cs（体积来源类型枚举）
│   │   └── AuditLogs（审计日志枚举目录）
│   │       ├── AuditResourceType.cs（审计资源类型枚举）
│   │       ├── FileOperationType.cs（文件操作类型枚举）
│   │       ├── WebRequestPayloadType.cs（请求载荷类型枚举）
│   │       └── WebResponsePayloadType.cs（响应载荷类型枚举）
│   ├── Primitives（领域基础类型目录）
│   │   └── AuditableEntity.cs（可审计实体基类）
│   ├── Repositories（领域仓储契约目录）
│   │   ├── IParcelRepository.cs（包裹仓储接口，含过期清理危险动作治理结果契约）
│   │   ├── IWebRequestAuditLogQueryRepository.cs（Web 请求审计日志只读查询仓储契约）
│   │   ├── IWebRequestAuditLogRepository.cs（Web 请求审计日志仓储写入契约）
│   │   └── Models（仓储查询与分页模型目录）
│   │       ├── Filters（查询过滤模型目录）
│   │       │   ├── ParcelQueryFilter.cs（Parcel 查询过滤模型）
│   │       │   └── WebRequestAuditLogQueryFilter.cs（Web 请求审计日志查询过滤模型）
│   │       ├── Paging（通用分页模型目录）
│   │       │   ├── CursorPageRequest.cs（游标分页请求模型：携带页大小与上一页最后记录游标）
│   │       │   ├── CursorPageResult.cs（游标分页结果模型：携带下一页游标位置与 HasMore 标识）
│   │       │   ├── PageRequest.cs（通用分页请求模型）
│   │       │   └── PageResult.cs（通用分页结果模型）
│   │       ├── ReadModels（查询读模型目录）
│   │       │   ├── ParcelSummaryReadModel.cs（Parcel 列表摘要读模型）
│   │       │   ├── WebRequestAuditLogDetailReadModel.cs（Web 请求审计日志详情读模型，含 WebRequestAuditLogId 外键镜像字段）
│   │       │   └── WebRequestAuditLogSummaryReadModel.cs（Web 请求审计日志列表摘要读模型）
│   │       ├── Results（仓储结果模型目录）
│   │       │   └── RepositoryResult.cs（仓储统一结果模型，含泛型版本与危险批量动作结果模型）
│   │       └── Validation（查询校验模型目录）
│   │           └── MaxTimeRangeAttribute.cs（查询时间跨度限制特性，默认不超过 3 个月）
│   └── Zeye.Sorting.Hub.Domain.csproj（Domain 项目定义）
├── Zeye.Sorting.Hub.Host（宿主层）
│   ├── Enums（宿主层枚举目录）
│   │   └── MigrationFailureMode.cs（数据库迁移失败策略枚举：FailFast/Degraded，含 Description）
│   ├── HostedServices（托管服务目录）
│   │   ├── AutoTuningLoggerObservability.cs（自动调优观测默认日志实现）
│   │   ├── DatabaseAutoTuningHostedService.cs（数据库自动调谐托管服务）
│   │   ├── DatabaseInitializerHostedService.cs（数据库初始化与迁移托管服务：迁移前自动建库检查（隔离器+审计）并继续迁移链路）
│   │   ├── DatabaseConnectionWarmupHostedService.cs（数据库连接预热托管服务：启动期按配置预热短生命周期连接）
│   │   ├── ParcelBatchWriteFlushHostedService.cs（Parcel 批量缓冲写入后台 Flush 托管服务）
│   │   ├── ShardingInspectionHostedService.cs（分表运行期巡检托管服务）
│   │   ├── ShardingPrebuildHostedService.cs（分表预建计划托管服务）
│   │   ├── DevelopmentBrowserLauncherHostedService.cs（Development 启动浏览器隔离器）
│   │   ├── ShardingGovernanceGuardException.cs（分表治理守卫异常类型）
│   │   ├── EvidenceContext.cs（自动调优证据上下文）
│   │   ├── PendingRollbackAction.cs（待回滚动作模型）
│   │   ├── PerDayGovernanceGroup.cs（PerDay 治理组模型：组名 + 逻辑表名清单）
│   │   ├── PolicyDecision.cs（策略决策模型）
│   │   ├── TableCapacitySnapshot.cs（表容量快照模型）
│   │   └── WebRequestAuditLogRetentionCandidates.cs（WebRequestAuditLog 历史分表保留候选模型：候选总数 + 物理表名清单）
│   ├── Routing（路由扩展目录）
│   │   ├── ParcelReadOnlyApiRouteExtensions.cs（Parcel 只读 API 路由扩展：含偏移分页、游标分页、详情与邻近查询）
│   │   ├── ParcelAdminApiRouteExtensions.cs（Parcel 管理端 API 路由扩展：含同步写接口、cleanup-expired 与 batch-buffer 缓冲写入接口）
│   │   └── AuditReadOnlyApiRouteExtensions.cs（Web 请求审计日志只读 API 路由扩展）
│   ├── QueryParameters（路由参数绑定模型目录）
│   │   ├── ParcelListQueryParameters.cs（Parcel 列表查询参数）
│   │   ├── ParcelCursorListQueryParameters.cs（Parcel 游标分页查询参数）
│   │   ├── ParcelAdjacentQueryParameters.cs（Parcel 邻近查询参数）
│   │   └── WebRequestAuditLogListQueryParameters.cs（审计日志列表查询参数）
│   ├── Options（配置模型目录）
│   │   ├── HostingOptions.cs（Hosting 主配置模型）
│   │   ├── SwaggerOptions.cs（Swagger 子配置模型）
│   │   ├── BrowserAutoOpenOptions.cs（Development 浏览器自动打开配置）
│   │   ├── AuditReadOnlyApiOptions.cs（AuditReadOnlyApi 显式开关配置）
│   │   └── ResourceThresholdsOptions.cs（运行时资源阈值告警配置）
│   ├── HealthChecks（健康检查目录）
│   │   ├── BufferedWriteQueueHealthCheck.cs（批量缓冲写入队列健康检查：输出队列深度、死信数量与 Flush 状态）
│   │   ├── ShardingGovernanceHealthCheck.cs（分表治理健康检查：输出缺表、缺索引、容量与预建计划状态）
│   │   ├── DatabaseConnectionDetailedHealthCheck.cs（数据库详细就绪探针：输出 provider、database、连续失败/成功次数等诊断数据）
│   │   ├── DatabaseReadinessHealthCheck.cs（数据库基础就绪探针：保留兼容实现）
│   │   └── HealthCheckResponseWriter.cs（健康检查 JSON 响应序列化工具，支持输出 Data 诊断数据）
│   ├── Utilities（工具目录）
│   │   └── LocalDateTimeParsing.cs（本地时间解析与 API 问题响应工厂共享工具）
│   ├── Middleware（请求审计中间件目录）
│   │   ├── CapturedBody.cs（正文采集结果值类型：内容/是否有Body/是否截断/原始字节长度）
│   │   ├── ResponseCaptureResult.cs（响应采集结果值类型）
│   │   ├── ResponseCaptureTeeStream.cs（响应双写有界采集流）
│   │   ├── WebRequestAuditBackgroundEntry.cs（审计后台队列项值类型）
│   │   ├── WebRequestAuditBackgroundQueue.cs（审计有界后台队列，含丢弃保护）
│   │   ├── WebRequestAuditBackgroundWorkerHostedService.cs（审计后台消费者服务）
│   │   ├── WebRequestAuditLogOptions.cs（Web 请求审计中间件配置模型）
│   │   ├── WebRequestAuditLogMiddleware.cs（Web 请求审计中间件实现：主请求零阻塞，异步脱钩写审计，补齐 Body 与 Curl 采集）
│   │   └── WebRequestAuditLogMiddlewareExtensions.cs（中间件注册与接线扩展）
│   ├── Swagger（Swagger 扩展目录）
│   │   └── EnumDescriptionSchemaFilter.cs（枚举 Schema 中文增强）
│   ├── Properties（运行调试属性目录）
│   │   └── launchSettings.json（本地调试启动配置）
│   ├── Program.cs（应用入口与 Host 构建流程）
│   ├── Zeye.Sorting.Hub.Host.csproj（Host 项目定义）
│   ├── nlog.config（NLog 日志配置）
│   ├── appsettings.Development.json（开发环境配置）
│   └── appsettings.json（默认运行配置，含 WebRequestAuditLog Body 采集开关与 AuditReadOnlyApi:Enabled）
├── Zeye.Sorting.Hub.Host.Tests（自动调优行为测试工程）
│   ├── AutoTuningProductionControlTests.cs（自动调优生产可控能力测试：dry-run/隔离器/告警恢复/普通与严重回归/探针双路径/闭环链路；含分表策略评估与 PerDay 预建守卫联动测试；新增 WebRequestAuditLog 治理解耦/保留治理三态/逻辑表索引分发/配置错误键指向回归；配置键拼装参数化覆盖（Theory））
│   ├── AlwaysExistsShardingPhysicalTableProbe.cs（物理表探测测试桩：始终存在场景，支撑分表守卫探测调用断言）
│   ├── BatchSelectiveMissingShardingPhysicalTableProbe.cs（批量物理表探测测试桩：选择性缺失与 schema 透传断言）
│   ├── CountingPlanProbe.cs（执行计划探针测试桩：记录调用次数）
│   ├── DomainEventArgsTests.cs（领域事件载荷单元测试：验证 ParcelScannedEventArgs/ParcelChuteAssignedEventArgs 业务字段赋值与值语义）
│   ├── EmptyServiceScope.cs（最小服务作用域测试桩）
│   ├── EmptyServiceScopeFactory.cs（最小服务作用域工厂测试桩）
│   ├── FakeParcelRepository.cs（Parcel 只读/管理端 API 复用仓储测试替身）
│   ├── FixedPlanProbe.cs（执行计划探针测试桩：固定返回可用快照）
│   ├── LocalTimeTestConstraint.cs（测试层本地时间语义约束工具类：提供 CreateLocalTime/AssertIsLocalTime/AssertNotUtc，防止测试引入 UTC 语义）
│   ├── LogCleanupServiceTests.cs（日志清理服务测试：验证目录栈递归扫描所有子目录 *.log 并仅删除过期文件）
│   ├── ConfigChangeHistoryStoreTests.cs（配置变更历史存储器测试：空历史、单条记录、多条按序排列、超容量环形覆盖、热加载联动快照记录）
│   ├── MissingIndexShardingPhysicalTableProbe.cs（索引缺失探测测试桩：按表返回缺失索引）
│   ├── ConfigurableShardingPhysicalTableProbe.cs（可配置分表物理对象探测测试桩）
│   ├── ShardingInspectionTests.cs（分表巡检、预建计划与健康检查回归测试）
│   ├── NullScope.cs（通用测试日志空作用域单例）
│   ├── ObservabilityEntry.cs（自动调优观测记录模型）
│   ├── HostingOptionsTests.cs（Hosting 配置拼装测试：监听地址拆分、Swagger 地址拼装、显式地址优先级与无效监听地址兜底）
│   ├── ParcelAdminApiTests.cs（Parcel 管理端写接口测试：新增/更新状态/删除成功路径 + cleanup-expired 三态 + 参数非法校验）
│   ├── AuditReadOnlyApiTests.cs（Web 请求审计日志只读 API 端点测试：分页、过滤、参数校验、详情全字段、写读联动）
│   ├── ParcelReadOnlyApiTests.cs（Parcel 只读 API 端点测试：列表/详情/404/邻近参数异常）
│   ├── DatabaseConnectionDiagnosticsTests.cs（数据库连接诊断测试：配置合法性、失败快照、健康检查阈值与 Data 输出）
│   ├── ParcelQueryServicesTests.cs（Parcel 应用层查询服务测试：列表/详情/邻近查询映射与最小校验；多重过滤条件联合成功路径；ExceptionType 筛选覆盖）
│   ├── ParcelRepositoryTests.cs（Parcel 仓储第一阶段能力测试：分页过滤、详情与邻近查询、写操作与过期清理；含阻断/dry-run/显式放开的危险动作治理回归）
│   ├── SelectiveMissingShardingPhysicalTableProbe.cs（物理表探测测试桩：选择性缺失场景）
│   ├── SortingHubTestDbContextFactory.cs（Host.Tests 通用 InMemory DbContextFactory，供查询服务/仓储测试复用）
│   ├── WebRequestAuditLogRepositoryTests.cs（Web 请求审计日志仓储写入测试：DI 解析、冷热一对一落库与应用服务写入入口）
│   ├── TestDialect.cs（通用数据库方言测试桩）
│   ├── TestHostEnvironment.cs（IHostEnvironment 测试桩）
│   ├── TestLogger.cs（通用泛型日志测试桩）
│   ├── TestMySqlDialect.cs（MySQL ProviderName 方言测试桩）
│   ├── TestObservability.cs（自动调优观测测试桩：收集指标与事件）
│   ├── TestOptionsMonitor.cs（IOptionsMonitor<T> 测试桩：支持 Update 方法触发热加载）
│   ├── OptionsMonitorSubscription.cs（IOptionsMonitor.OnChange 订阅句柄，Dispose 时取消订阅）
│   ├── TestSqlServerDialect.cs（SQL Server ProviderName 方言测试桩）
│   ├── ThrowOnReadStream.cs（读取即抛异常的请求体流测试桩）
│   └── Zeye.Sorting.Hub.Host.Tests.csproj（xUnit 测试项目定义）
├── Zeye.Sorting.Hub.Infrastructure（基础设施层）
│   ├── DependencyInjection（依赖注入扩展目录）
│   │   └── PersistenceServiceCollectionExtensions.cs（持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、数据库连接诊断/预热、批量缓冲写入与分表规则守卫注册；Parcel 主表始终按 CreatedTime 路由，时间/容量/混合策略决策由统一评估器驱动））
│   ├── EntityConfigurations（EF Core 映射配置目录）
│   │   ├── BagInfoEntityTypeConfiguration.cs（BagInfo 映射配置）
│   │   ├── ParcelEntityTypeConfiguration.cs（Parcel 映射配置）
│   │   ├── WebRequestAuditLogEntityTypeConfiguration.cs（Web 请求审计热表映射配置）
│   │   └── WebRequestAuditLogDetailEntityTypeConfiguration.cs（Web 请求审计冷表映射配置）
│   ├── Persistence（持久化核心目录）
│   │   ├── AutoTuning（自动调谐核心目录）
│   │   │   ├── AutoTuningConfigurationReader.cs（配置读取与本地时间语义归一化/配置键拼装工具类，统一 AutoTuning 键名与时间语义）
│   │   │   ├── MySqlSessionBootstrapConnectionInterceptor.cs（MySQL 连接会话初始化拦截器，直连类型判断，无额外转发）
│   │   │   ├── SlowQueryAutoTuningPipeline.cs（慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道；新增主表提取公共方法供 AutoTuning 主链路复用）
│   │   │   ├── SlowQueryCommandInterceptor.cs（EF Core 慢查询采集拦截器）
│   │   │   └── SlowQuerySample.cs（慢查询采样记录模型）
│   │   ├── Diagnostics（数据库连接诊断目录）
│   │   │   ├── DatabaseConnectionDiagnosticsOptions.cs（数据库连接诊断配置模型：预热开关、探测超时、失败/恢复阈值）
│   │   │   ├── DatabaseConnectionHealthSnapshot.cs（数据库连接诊断快照：记录最近一次探测结果与连续成功/失败计数）
│   │   │   ├── IDatabaseConnectionDiagnostics.cs（数据库连接诊断服务抽象）
│   │   │   ├── DatabaseConnectionDiagnosticsService.cs（数据库连接诊断服务：执行短生命周期探测并缓存快照）
│   │   │   └── DatabaseConnectionWarmupService.cs（数据库连接预热服务：启动期按配置预热多个连接）
│   │   ├── Sharding（分表策略与治理决策目录）
│   │   │   ├── ParcelShardingStrategyEvaluator.cs（Parcel 分表策略评估器：配置解析、结构化校验、容量观测输入收敛、阈值决策、finer-granularity 扩展规划与统一决策快照）
│   │   │   ├── ShardingCapacitySnapshotService.cs（分表容量与热点风险快照服务）
│   │   │   ├── ShardingIndexInspectionService.cs（分表关键索引巡检服务）
│   │   │   ├── ShardingInspectionReport.cs（分表巡检报告模型）
│   │   │   ├── ShardingPhysicalTablePlanBuilder.cs（分表物理表规划构建器）
│   │   │   ├── ShardingPrebuildOptions.cs（分表预建计划配置模型）
│   │   │   ├── ShardingPrebuildPlan.cs（分表预建计划模型）
│   │   │   ├── ShardingRuntimeInspectionOptions.cs（分表运行期巡检配置模型）
│   │   │   ├── ShardingTableInspectionService.cs（分表物理表巡检服务）
│   │   │   ├── ShardingTablePrebuildService.cs（分表预建 dry-run 计划服务）
│   │   │   └── Enums（分表策略枚举目录）
│   │   │       ├── ParcelFinerGranularityMode.cs（PerDay 仍过热时下一层细粒度模式枚举：None/PerHour/BucketedPerDay）
│   │   │       ├── ParcelFinerGranularityPlanLifecycle.cs（finer-granularity 扩展规划生命周期枚举：PlanOnly/AlertOnly/FutureExecutable）
│   │   │       ├── ParcelAggregateShardingRuleKind.cs（Parcel 聚合分表规则类别枚举：Date/Hash）
│   │   │       ├── ParcelShardingStrategyMode.cs（分表模式枚举：Time/Volume/Hybrid）
│   │   │       ├── ParcelTimeShardingGranularity.cs（时间粒度枚举：PerMonth/PerDay）
│   │   │       └── ParcelVolumeThresholdAction.cs（容量阈值动作枚举：AlertOnly/SwitchToPerDay）
│   │   ├── DatabaseDialects（数据库方言目录）
│   │   │   ├── DatabaseProviderOperations.cs（数据库提供器操作类：异常错误码提取、WHERE 列归一化、稳定索引名构造）
│   │   │   ├── DatabaseIdentifierPolicy.cs（数据库名安全校验与标识符转义工具，统一防注入边界）
│   │   │   ├── DatabaseConnectionOpenCoordinator.cs（数据库连接打开共享辅助，统一连接状态处理）
│   │   │   ├── IDatabaseDialect.cs（数据库方言接口）
│   │   │   ├── IShardingPhysicalTableProbe.cs（分表物理对象探测接口：支持物理表存在性与关键索引缺失探测（仅探测，不执行 DDL））
│   │   │   ├── IBatchShardingPhysicalTableProbe.cs（批量分表物理表探测接口，支持缺失探测与按逻辑表枚举物理分表）
│   │   │   ├── MySqlDialect.cs（MySQL 方言实现：自动调优 + 分表探测 + 启动期数据库存在性探测/建库执行）
│   │   │   └── SqlServerDialect.cs（SQL Server 方言实现：自动调优 + 分表探测 + 启动期数据库存在性探测/建库执行）
│   │   ├── DesignTime（EF 设计时支持目录）
│   │   │   ├── DesignTimeConfigurationLocator.cs（设计时配置目录定位器：统一 appsettings.json 查找与加载逻辑，消除 MySql/SqlServer 工厂重复代码）
│   │   │   ├── MySqlContextFactory.cs（统一设计时 DbContext 工厂，支持 --provider 切换 MySql/SqlServer；告警输出通过 NLog 落盘）
│   │   │   └── SqlServerContextFactory.cs（SQL Server 设计时 DbContext 构建器）
│   │   ├── Migrations（EF Core 迁移文件目录）
│   │   │   ├── 20260324094539_RebuildBaseline20260324.cs（全新基线迁移：空库初始化全量建表/索引/约束）
│   │   │   ├── 20260324094539_RebuildBaseline20260324.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── MigrationSchemaResolver.cs（迁移 schema 解析器）
│   │   │   └── SortingHubDbContextModelSnapshot.cs（当前模型快照，自动生成）
│   │   ├── WriteBuffering（批量缓冲写入基础设施目录）
│   │   │   ├── BatchWriteMetricsSnapshot.cs（批量缓冲写入指标快照模型）
│   │   │   ├── BoundedWriteChannel.cs（批量缓冲写入有界 Channel 封装）
│   │   │   ├── BufferedParcelWriteItem.cs（Parcel 缓冲写入通道项）
│   │   │   ├── DeadLetterWriteEntry.cs（Parcel 死信记录模型）
│   │   │   ├── DeadLetterWriteStore.cs（Parcel 死信有界存储）
│   │   │   ├── ParcelBatchWriteFlushService.cs（Parcel 缓冲写入后台批量 Flush 服务）
│   │   │   └── ParcelBufferedWriteService.cs（Parcel 缓冲写入服务实现）
│   │   └── SortingHubDbContext.cs（EF Core DbContext）
│   │   ├── DbProviderNames.cs（EF Core 运行时/迁移 providerName 常量）
│   │   ├── ConfiguredProviderNames.cs（配置层 provider key 常量：Persistence:Provider / ConnectionStrings key / CLI --provider）
│   │   └── WebRequestAuditLogIndexNames.cs（Web 请求审计日志关键索引名称常量）
│   ├── Repositories（仓储基类与结果模型目录）
│   │   ├── MemoryCacheRepositoryBase.cs（带内存缓存失效的仓储基类，使用 NLog 日志）
│   │   ├── ParcelCursorQueryExtensions.cs（Parcel 游标分页查询扩展：统一稳定排序下的游标条件拼接）
│   │   ├── ParcelRepository.cs（Parcel 仓储第一阶段实现，使用静态 NLog logger，无需 MEL ILogger 构造注入；BarCodeKeyword 检索按 Provider 分支：MySQL 走 FULLTEXT Boolean，其他 Provider 回退 Contains）
│   │   ├── RepositoryBase.cs（通用仓储基类，接受 NLog.ILogger 构造参数，由派生类传入确保日志来源类名正确）
│   │   └── WebRequestAuditLogRepository.cs（Web 请求审计日志仓储实现：热表+冷表写入与只读查询）
│   └── Zeye.Sorting.Hub.Infrastructure.csproj（Infrastructure 项目定义）
├── Zeye.Sorting.Hub.Realtime（实时通信子域，占位工程）
│   └── Zeye.Sorting.Hub.Realtime.csproj（Realtime 项目定义）
├── Zeye.Sorting.Hub.RuleEngine（规则引擎子域，占位工程）
│   └── Zeye.Sorting.Hub.RuleEngine.csproj（RuleEngine 项目定义）
├── Zeye.Sorting.Hub.SharedKernel（共享内核）
│   ├── Utilities（共享工具目录）
│   │   ├── SafeExecutor.cs（安全执行器：使用 NLog 静态 logger，不再依赖 MEL ILogger 构造注入；隔离任何异常，Execute/ExecuteAsync 确保副作用不会导致宿主崩溃）
│   │   ├── ConfigChangeEntry.cs（配置变更历史记录条目：保存单次配置变更快照，含变更序号/前值/后值/本地生效时间/变更字段摘要）
│   │   ├── ConfigChangeHistoryStore.cs（配置变更历史存储器：线程安全环形缓冲，保留最近 N 次配置快照，支持前后值审计与回滚查询）
│   │   └── LineBreakNormalizer.cs（换行标准化工具：将 CR/LF 归一化为空格，仅在存在换行时分配新字符串）
│   └── Zeye.Sorting.Hub.SharedKernel.csproj（SharedKernel 项目定义）
├── Zeye.Sorting.Hub.sln（.NET 解决方案入口）
├── EFCore数据库迁移指南.md（EF Core CodeFirst 迁移使用说明文档）
├── EFCore9升级计划.md（EF Core 8 → 9 升级记录：已完成，EFCore 9.0.14 / Pomelo 9.0.0 / HasPendingModelChanges 守卫已集成）
├── 新数据库提供程序接入指南.md（接入新数据库提供器（如 SQLite / PostgreSQL）的逐步操作指南）
├── 数据库读写压力测试计划.md（MySQL + EFCore.Sharding 分表架构的读写压测方案与验收模板）
├── Parcel属性新增操作指南.md（Parcel 聚合新增属性时的文件修改操作指南）
└── 项目完成度与推进计划.md（项目阶段评估与路线图文档）
```

## Copilot 维护规定

- 每次新增文件或删除文件后，必须同步更新本 README 的“仓库文件结构（当前）”章节，保证结构清单与仓库实际内容一致。
- 每次新增文件或删除文件后，必须同步更新本 README 的“各层级与各文件作用说明（逐项）”章节，保证职责说明与仓库实际内容一致。
- 硬性规则：全项目禁止使用 UTC 时间（如 `DateTime.UtcNow`、`DateTimeOffset.UtcNow`、`DateTimeKind.Utc`、`ToUniversalTime` 等），统一使用本地时间语义（如 `DateTime.Now`、`DateTimeKind.Local`）。

## 各层级与各文件作用说明（逐项）

### 根目录（`.`）

- `.`：解决方案根目录，承载多项目分层结构（Host、Domain、Infrastructure、Application、Contracts 等）。
- `.github/`：Copilot 仓库级指令目录。
- `.gitattributes`：Git 属性配置（如行尾规范）。
- `.gitignore`：Git 忽略规则（如 `bin/`、`obj/`、IDE 临时文件）。
- `README.md`：仓库总览、结构清单与维护规范文档。
- `更新记录.md`：更新记录，按时间倒序记录每次 PR 更新内容（从 README 独立拆分）。
- `待完善事项.md`：待完善事项列表，仅记录代码中尚未实现的可完善点（从 README 独立拆分，已实现项不记录）。
- `Zeye.Sorting.Hub.sln`：.NET 解决方案入口，聚合全部项目。
- `Parcel属性新增操作指南.md`：当 Parcel 聚合需要新增属性时，需要修改哪些文件、如何修改的操作指南（含三种情形：主表标量属性、现有值对象属性、新增值对象）。
- `项目完成度与推进计划.md`：项目阶段评估与路线图文档。
- `EFCore数据库迁移指南.md`：EF Core CodeFirst 迁移使用说明（迁移架构总览、运行时自动迁移、CLI 命令、设计时工厂、分表与迁移关系、常见问题）。
- `EFCore9升级计划.md`：EF Core 8 → 9 升级记录（**已完成**：EF Core 9.0.14、Pomelo 9.0.0、EFCore.Sharding 9.0.10），包含已升级包清单、版本对照表、`HasPendingModelChanges()` 集成说明及核查清单。
- `新数据库提供程序接入指南.md`：新数据库提供器接入指南（MySQL / SQL Server 切换、设计时工厂、方言扩展点）。
- `数据库读写压力测试计划.md`：针对 MySQL + EFCore.Sharding 分表架构的数据库读写压力测试计划，覆盖纯写入、纯读取、混合读写、长时稳定性 4 大场景，含梯度加压方案、通过/失败验收矩阵、监控采集命令与结果记录模板。
- `长期运行优化与热更新支持清单.md`：面向一年无人值守运行的长期治理清单，覆盖稳定运行底座、热更新、热替换、阶段化落地与年度验收门禁。
- `逐文件检查方案.md`：逐文件质量审查执行方案（面向 Copilot 的检查流程），覆盖影分身代码、过度设计、冗余、性能、逻辑、并发竞态与规则违规检查，同时给出多 PR 分批与防遗漏机制。
- `drill-records/`：季度/年度稳定性演练记录目录；每次演练后新增 `.md` 或 `.txt` 记录文件，供 `stability-gates.yml` 中 `drill-record-gate`（门禁 8）强制检查，缺少记录文件将阻断 PR 合并。
- `检查台账/`：逐文件全量审查台账目录；存放全仓库文件清单基线（`文件清单基线.txt`）与各批次检查结果 Markdown，按 PR-A/B/C… 分批递增，供检查进度追踪与对账使用。
  - `文件清单基线.txt`：全仓库受版本控制文件基线清单，由 `git ls-files` 生成，作为逐文件全量审查防遗漏对账基准，每轮全量审查生成一次。
  - `PR-A-检查台账.md`：PR-A 批次检查台账，覆盖根目录、`.github/`、文档与脚本共 21 个文件的审查结论、问题清单（P0/P1/P2）与后续修复建议。
  - `PR-B-检查台账.md`：PR-B 批次检查台账，覆盖 `Zeye.Sorting.Hub.Domain/` 共 67 个文件的审查结论、问题清单（0 P0 / 10 类 P1 / 9 类 P2）与修复 PR 规划（PR-FIX-B1～B4）。
  - `PR-C-检查台账.md`：PR-C 批次检查台账，覆盖 `Zeye.Sorting.Hub.Application/` + `Zeye.Sorting.Hub.Contracts/` 共 45 个文件的审查结论、问题清单（0 P0 / 3 P1 / 5 P2）与修复 PR 规划（PR-FIX-C1～C3）。
  - `PR-D-检查台账.md`：PR-D 批次检查台账，覆盖 `Zeye.Sorting.Hub.Infrastructure/` 共 63 个文件的审查结论、问题清单（0 P0 / 1 P1 / 11 P2）与修复 PR 规划（PR-FIX-D1～D2）。
  - `PR-E-检查台账.md`：PR-E 批次检查台账，覆盖 `Zeye.Sorting.Hub.Host/` 共 43 个文件的审查结论、问题清单（0 P0 / 3 P1 / 13 P2）与修复 PR 规划（PR-FIX-E1～E4）。
  - `PR-F-检查台账.md`：PR-F 批次检查台账（最终批次），覆盖 `Zeye.Sorting.Hub.SharedKernel/`、`Zeye.Sorting.Hub.Host.Tests/` 及占位子域共 45 个文件的审查结论、问题清单（0 P0 / 0 P1 / 12 P2）与修复 PR 规划（PR-FIX-F1～F2）；同时提供全量 287 文件 100% 覆盖的总对账结果。
  - `PR-长期数据库底座A-检查台账.md`：长期数据库底座 PR-A 实施台账；记录多 PR 路线图现状核对、数据库连接诊断切片交付清单、验证结果与下一 PR 入口。
  - `PR-长期数据库底座B-检查台账.md`：长期数据库底座 PR-B 实施台账；记录查询保护与游标分页交付清单、验证结果与下一 PR 入口。
  - `PR-长期数据库底座C-检查台账.md`：长期数据库底座 PR-C 实施台账；记录批量缓冲写入、死信隔离、健康检查交付清单、验证结果与下一 PR 入口。
  - `PR-长期数据库底座D-检查台账.md`：长期数据库底座 PR-D 实施台账；记录分表巡检、预建计划、索引检查、健康检查交付清单、验证结果与下一 PR 入口。

### `.github/`：Copilot 仓库级指令目录
- `DDD分层接口与实现放置规范.md`：DDD 分层接口定义与实现放置规范文档；明确依赖方向（Host→Infrastructure→Application→Domain）、接口定义归属规则（领域能力/应用编排/基础设施内部三类）、实现类放置约束、目录结构建议与禁止事项清单，供 Copilot 与开发人员统一执行。
- `copilot-instructions.md`：Copilot 自定义指令，硬性要求禁止 UTC 时间 API，统一使用本地时间语义。
- `scripts/validate-copilot-rules.sh`：Copilot 限制规则校验脚本；从 `copilot-instructions.md` 解析“Copilot 限制规则”逐条执行，已映射规则做自动校验，未映射规则直接失败，确保规则文档更新后 CI 校验逻辑同步更新。

### `.github/workflows/`：CI 工作流目录
- `copilot-instructions-validation.yml`：Copilot 限制规则校验流水线；每次 PR 触发并执行 `validate-copilot-rules.sh`，对规则自动门禁。
- `ef-migration-validation.yml`：EF 迁移验收流水线（MySQL + SQL Server 容器环境），真实执行 `dotnet ef migrations list`、`dotnet ef database update`、`dotnet ef migrations script` 三项门禁命令。
- `stability-gates.yml`：长期运行稳定性门禁流水线；包含构建+单元测试、配置合法性验证（含分表预建配置检查）、隔离器边界检查、回滚资产检查、健康探针端点注册检查、契约兼容性验证、蓝绿/滚动部署验证、演练记录门禁（强制阻断）、分表预建校验门禁、迁移脚本归档验证门禁共 10 个 job/门禁；其中 `deploy-validation` 依赖 `build-and-test`、`health-probe`、`rollback-asset`，并非全部 job 同时并行执行。

### `Zeye.Sorting.Hub.Analytics/`：分析与报表子域（当前为占位工程）
- `Zeye.Sorting.Hub.Analytics.csproj`：Analytics 项目定义。
- `Class1.cs`：占位类，预留统计指标/报表能力实现位置。

### `Zeye.Sorting.Hub.Application/`：应用层（Use Case 编排层）
- `Zeye.Sorting.Hub.Application.csproj`：Application 项目定义（引用 Domain + Contracts，承载应用服务实现）。

#### `Zeye.Sorting.Hub.Application/Utilities/`：应用层内部共享工具目录
- `EnumGuard.cs`：枚举值合法性校验工具；统一封装 `Enum.IsDefined` 判断、Warn 日志记录与 `ArgumentOutOfRangeException` 抛出，消除各应用服务中重复的枚举验证模板代码；提供 `int` 和 `int?` 两个重载。
- `Guard.cs`：基础参数边界守卫工具；提供 `ThrowIfZeroOrNegative`（Id 正数校验，有 long/int 两个重载）和 `ThrowIfNegative`（可选数量非负校验），统一记录 Warn 日志并抛出 `ArgumentOutOfRangeException`。

#### `Zeye.Sorting.Hub.Application/Services/AuditLogs/`：审计日志应用服务目录
- `GetWebRequestAuditLogPagedQueryService.cs`：Web 请求审计日志分页查询应用服务（参数校验、过滤映射、分页结果映射）。
- `GetWebRequestAuditLogByIdQueryService.cs`：按 Id 查询 Web 请求审计日志详情应用服务（Id 校验、读模型映射）。
- `WebRequestAuditLogContractMapper.cs`：Web 请求审计日志读模型到 Contracts 响应映射器（列表项/详情）。
- `WriteWebRequestAuditLogCommandService.cs`：Web 请求审计日志最小写入应用服务入口，负责将聚合写入委托给仓储。

#### `Zeye.Sorting.Hub.Application/Services/Parcels/`：Parcel 应用服务目录（查询 + 管理端写命令）
- `GetParcelByIdQueryService.cs`：按 Id 查询 Parcel 详情应用服务（仓储调用 + 合同映射 + 最小参数校验）。
- `GetParcelPagedQueryService.cs`：分页查询 Parcel 列表应用服务（请求校验、默认最近 24 小时时间窗口、最大页码保护、分页结果映射）。
- `GetParcelCursorPagedQueryService.cs`：游标分页查询 Parcel 列表应用服务（游标解码、请求校验、默认最近 24 小时时间窗口、游标结果映射）。
- `GetAdjacentParcelsQueryService.cs`：按包裹 Id 查询邻近 Parcel 应用服务（数量归一化至 `IParcelRepository.MaxAdjacentCountPerSide`、响应映射；锚点不存在抛 KeyNotFoundException 供 Host 映射 404）。
- `ParcelContractMapper.cs`：Parcel 领域模型/读模型到 Contracts 模型的统一映射器，避免 Host 层重复映射。
- `ParcelQueryRequestMapper.cs`：Parcel 查询请求映射器，统一普通分页与游标分页的过滤条件构建和默认最近 24 小时时间窗口。
- `CreateParcelCommandService.cs`：管理端新增包裹应用服务（复用 `ParcelCreateRequestMapper` 构建聚合、仓储 AddAsync、合同映射）。
- `UpdateParcelStatusCommandService.cs`：管理端更新包裹状态应用服务（仅支持 MarkCompleted/MarkSortingException/UpdateRequestStatus 三种领域方法，不允许任意字段修改）。
- `DeleteParcelCommandService.cs`：管理端删除单个包裹应用服务（先加载聚合根，不存在返回 false，再调用 RemoveAsync）。
- `CleanupExpiredParcelsCommandService.cs`：过期包裹清理应用服务（治理型，调用仓储 RemoveExpiredAsync，不绕过隔离器，映射 DangerousBatchActionResult 为外部合同响应）。

#### `Zeye.Sorting.Hub.Application/Mappers/Parcels/`：应用层 Parcel 映射目录
- `ParcelCreateRequestMapper.cs`：Parcel 新增请求映射器，统一同步新增与批量缓冲写入的聚合构建与枚举校验。

#### `Zeye.Sorting.Hub.Application/Services/WriteBuffers/`：批量缓冲写入应用抽象目录
- `BufferedWriteOptions.cs`：批量缓冲写入配置模型，定义开关、容量、批次、重试与死信容量范围。
- `BufferedWriteResult.cs`：批量缓冲写入结果模型，统一 accepted/rejected/queueDepth/backpressure 响应。
- `IBufferedWriteService.cs`：批量缓冲写入服务契约，定义 Parcel 批量入队入口。

### `Zeye.Sorting.Hub.Contracts/`：契约层（对外 DTO / 接口模型）
- `Zeye.Sorting.Hub.Contracts.csproj`：Contracts 项目定义。

#### `Zeye.Sorting.Hub.Contracts/Models/Parcels/`：Parcel 对外查询合同目录
- `ParcelListRequest.cs`：Parcel 列表查询请求合同（分页 + 过滤参数）。
- `ParcelListItemResponse.cs`：Parcel 列表项响应合同（扁平化字段，不暴露领域聚合根）。
- `ParcelListResponse.cs`：Parcel 列表分页响应合同。
- `ParcelCursorListRequest.cs`：Parcel 游标分页查询请求合同（游标 + 过滤参数）。
- `ParcelCursorListResponse.cs`：Parcel 游标分页响应合同（Items、PageSize、HasMore、NextCursor）。
- `ParcelCursorToken.cs`：Parcel 游标令牌合同，编码上一页最后一条记录的 `ScannedTime` 与 `Id`。
- `ParcelDetailResponse.cs`：Parcel 详情响应合同（继承列表项扁平字段，并包含所有联表值对象内容）。
- `ParcelAdjacentRequest.cs`：Parcel 邻近查询请求合同。
- `ParcelAdjacentResponse.cs`：Parcel 邻近查询响应合同。

#### `Zeye.Sorting.Hub.Contracts/Models/AuditLogs/WebRequests/`：Web 请求审计日志查询合同目录
- `WebRequestAuditLogListRequest.cs`：Web 请求审计日志列表查询请求合同（分页 + 可选过滤条件）。
- `WebRequestAuditLogListItemResponse.cs`：Web 请求审计日志列表项响应合同（高频展示字段）。
- `WebRequestAuditLogListResponse.cs`：Web 请求审计日志列表分页响应合同。
- `WebRequestAuditLogDetailResponse.cs`：Web 请求审计日志详情响应合同（热表字段 + 冷表详情字段）。

#### `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/`：Parcel 值对象响应合同目录
- `ApiRequestInfoResponse.cs`：外部接口请求记录响应合同。
- `BagInfoResponse.cs`：集包信息响应合同。
- `BarCodeInfoResponse.cs`：条码明细响应合同。
- `ChuteInfoResponse.cs`：格口信息响应合同。
- `CommandInfoResponse.cs`：通信指令记录响应合同。
- `GrayDetectorInfoResponse.cs`：灰检信息响应合同。
- `ImageInfoResponse.cs`：图片信息响应合同。
- `ParcelDeviceInfoResponse.cs`：包裹设备信息响应合同。
- `ParcelPositionInfoResponse.cs`：包裹坐标信息响应合同。
- `SorterCarrierInfoResponse.cs`：小车信息响应合同。
- `StickingParcelInfoResponse.cs`：叠包信息响应合同。
- `VideoInfoResponse.cs`：视频信息响应合同。
- `VolumeInfoResponse.cs`：体积信息响应合同。
- `WeightInfoResponse.cs`：称重明细响应合同。

#### `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/`：管理端写接口合同目录
- `ParcelBatchBufferedCreateRequest.cs`：Parcel 批量缓冲写入请求合同，承载多个 `ParcelCreateRequest`。
- `ParcelBatchBufferedCreateResponse.cs`：Parcel 批量缓冲写入响应合同，返回 acceptedCount、rejectedCount、queueDepth、isBackpressureTriggered 与 message。
- `ParcelCreateRequest.cs`：管理端新增包裹请求合同（含调用方传入的包裹 Id，要求大于 0 且全局唯一；时间字段为本地时间字符串，由 API 层统一解析并拒绝 UTC/offset）。
- `ParcelUpdateRequest.cs`：管理端更新包裹状态请求合同（Operation 枚举决定操作类型，对应 CompletedTime/ExceptionType/RequestStatus）。
- `ParcelCleanupExpiredRequest.cs`：过期清理治理接口请求合同（CreatedBefore 本地时间字符串，API 层强制解析校验）。
- `ParcelCleanupExpiredResponse.cs`：过期清理治理接口响应合同（ActionName/Decision/PlannedCount/ExecutedCount/IsDryRun/IsBlockedByGuard/CompensationBoundary）。

### `Zeye.Sorting.Hub.Domain/`：核心领域层，存放聚合根、值对象、领域事件、枚举与仓储接口
- `Zeye.Sorting.Hub.Domain.csproj`：Domain 项目定义。

#### `Zeye.Sorting.Hub.Domain/Abstractions/`：领域抽象接口层
- `IEntity.cs`：实体通用接口（定义主键契约）。
- `IParcelSummaryView.cs`：Parcel 摘要视图接口；抽象聚合根与摘要读模型的公共可读属性，供 Application 层映射器统一消费，消除影分身映射代码。

#### `Zeye.Sorting.Hub.Domain/Aggregates/`：领域聚合目录

##### `Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/`：审计日志聚合目录

###### `Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/WebRequests/`：Web 请求审计聚合目录
- `WebRequestAuditLog.cs`：Web 请求审计日志热数据聚合根（高频写入与高频筛选字段）。
- `WebRequestAuditLogDetail.cs`：Web 请求审计日志冷数据详情实体（一对一承载大文本与低频字段）。

##### `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/`：包裹聚合目录
- `Parcel.cs`：包裹聚合根，承载包裹生命周期状态与行为。

###### `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/`：包裹聚合值对象目录
- `ApiRequestInfo.cs`：外部接口请求/响应信息值对象。
- `BagInfo.cs`：袋笼/集包信息值对象。
- `BarCodeInfo.cs`：条码识别信息值对象。
- `ChuteInfo.cs`：格口分配信息值对象。
- `CommandInfo.cs`：设备命令交互信息值对象。
- `GrayDetectorInfo.cs`：灰度检测结果值对象。
- `ImageInfo.cs`：图片元数据值对象（路径、类型、时间等）。
- `ParcelDeviceInfo.cs`：包裹相关设备信息值对象。
- `ParcelPositionInfo.cs`：包裹空间/轨迹位置信息值对象。
- `SorterCarrierInfo.cs`：分拣小车/载体信息值对象。
- `StickingParcelInfo.cs`：叠包检测结果值对象。
- `VideoInfo.cs`：视频信息值对象。
- `VolumeInfo.cs`：体积信息值对象。
- `WeightInfo.cs`：重量信息值对象。

#### `Zeye.Sorting.Hub.Domain/Events/`：领域事件载荷目录

##### `Zeye.Sorting.Hub.Domain/Events/Parcels/`：包裹相关领域事件载荷目录
- `ParcelChuteAssignedEventArgs.cs`：包裹分配格口事件载荷（`readonly record struct`，不可变值语义；携带 ParcelId/TargetChuteId/ActualChuteId/ScannedTime 业务字段）。
- `ParcelScannedEventArgs.cs`：包裹扫描事件载荷（`readonly record struct`，不可变值语义；携带 ParcelId/BarCodes/WorkstationName/ScannedTime/BagCode/TargetChuteId 业务字段）。

#### `Zeye.Sorting.Hub.Domain/Enums/`：领域枚举与业务语义常量目录
- `ActionType.cs`：动作类型枚举定义。
- `ActionIsolationDecision.cs`：自动调优危险动作隔离决策枚举定义。
- `ApiRequestStatus.cs`：接口请求状态枚举定义。
- `ApiRequestType.cs`：接口请求类型枚举定义。
- `AutoTuningClosedLoopStage.cs`：自动调优闭环阶段枚举定义。
- `AutoTuningUnavailableReason.cs`：自动调优 unavailable 原因枚举与统一标签映射扩展，避免自由字符串漂移。
- `BarCodeType.cs`：条码类型枚举定义。
- `CommandDirection.cs`：命令方向枚举定义。
- `ImageCaptureType.cs`：图像采集方式枚举定义。
- `ImageType.cs`：图像类型枚举定义。
- `NoReadType.cs`：无码/难码类型枚举定义。
- `ParcelExceptionType.cs`：包裹异常类型枚举定义（分拣异常细分原因）。
- `ParcelStatus.cs`：包裹状态枚举定义。
- `ParcelType.cs`：包裹类别枚举定义。
- `VideoNodeType.cs`：视频节点类型枚举定义。
- `VolumeSourceType.cs`：体积来源类型枚举定义。
- `MigrationFailureMode.cs`：数据库迁移失败策略枚举。
- `ParcelUpdateOperation.cs`：包裹状态更新操作枚举。

#### `Zeye.Sorting.Hub.Domain/Enums/Sharding/`：分表治理枚举子目录
- `ParcelShardingStrategyMode.cs`：分表策略模式枚举。
- `ParcelTimeShardingGranularity.cs`：时间分表粒度枚举。
- `ParcelVolumeThresholdAction.cs`：容量阈值动作枚举。
- `ParcelFinerGranularityMode.cs`：细粒度扩展模式枚举。
- `ParcelFinerGranularityPlanLifecycle.cs`：细粒度扩展生命周期枚举。
- `ParcelAggregateShardingRuleKind.cs`：聚合分表规则类别枚举。

#### `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/`：审计日志枚举子目录
- `AuditResourceType.cs`：审计资源类型枚举。
- `FileOperationType.cs`：文件操作类型枚举。
- `WebRequestPayloadType.cs`：Web 请求载荷类型枚举。
- `WebResponsePayloadType.cs`：Web 响应载荷类型枚举。

#### `Zeye.Sorting.Hub.Domain/Primitives/`：领域基础类型目录
- `AuditableEntity.cs`：可审计实体基类（创建/修改信息等）。

#### `Zeye.Sorting.Hub.Domain/Repositories/`：领域仓储契约目录
- `IParcelRepository.cs`：包裹仓储接口（第一阶段可落地契约：基础读写、偏移分页、游标分页、按 Id 邻近查询、过期清理危险动作治理结果返回；同时定义 `MaxAdjacentCountPerSide = 200` 常量，为 Application 层与 Infrastructure 层提供唯一权威数字来源，禁止各自硬编码）。
- `IWebRequestAuditLogQueryRepository.cs`：Web 请求审计日志只读查询仓储契约（分页列表与按 Id 详情）。
- `IWebRequestAuditLogRepository.cs`：Web 请求审计日志仓储最小写入契约（`AddAsync`）。

##### `Zeye.Sorting.Hub.Domain/Repositories/Models/`：Parcel 仓储查询模型目录

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/`：查询过滤模型目录
- `ParcelQueryFilter.cs`：Parcel 第一阶段列表过滤参数模型（BagCode、WorkstationName、Status、Chute、扫码时间范围等），并通过特性限制时间跨度默认不超过 3 个月。
- `WebRequestAuditLogQueryFilter.cs`：Web 请求审计日志列表过滤参数模型（startedAt 区间、statusCode、isSuccess、traceId、correlationId、requestPathKeyword）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/`：通用分页模型目录
- `CursorPageRequest.cs`：游标分页请求参数（页大小、上一页最后记录的 `ScannedTime` 与 `Id`）。
- `CursorPageResult.cs`：游标分页结果模型（Items、HasMore 与下一页游标位置）。
- `PageRequest.cs`：通用分页请求参数（含页码/页大小归一化）。
- `PageResult.cs`：通用分页结果模型（Items、页码、页大小、总数）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/`：查询读模型目录
- `ParcelSummaryReadModel.cs`：Parcel 列表摘要读模型（包含 Parcel 全部扁平化字段，用于分页列表）。
- `WebRequestAuditLogSummaryReadModel.cs`：Web 请求审计日志列表摘要读模型（高频查询字段）。
- `WebRequestAuditLogDetailReadModel.cs`：Web 请求审计日志详情读模型（热表字段 + 冷表详情字段，含 `WebRequestAuditLogId` 外键镜像字段）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/`：仓储结果模型目录
- `RepositoryResult.cs`：非泛型仓储结果模型。
- `RepositoryResultOfT.cs`：泛型仓储结果模型。
- `RepositoryErrorCodes.cs`：仓储层稳定错误码。
- `DangerousBatchActionResult.cs`：危险批量动作治理结果模型。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Validation/`：查询校验模型目录
- `MaxTimeRangeAttribute.cs`：时间范围校验特性（限制起止时间跨度，默认不超过 3 个月）。

### `Zeye.Sorting.Hub.Host/`：宿主层（程序入口、后台服务、启动配置）
- `Program.cs`：应用入口与 Host 构建流程（按 `AuditReadOnlyApi:Enabled` 显式开关控制审计只读路由映射，并注册 Parcel 游标分页查询服务、批量缓冲写入后台 Flush 服务、分表巡检/预建托管服务与健康检查）。
- `Routing/ParcelReadOnlyApiRouteExtensions.cs`：Parcel 只读路由注册与处理逻辑；新增 `/api/parcels/cursor` 游标分页接口，并为普通分页补充默认最近 24 小时与页码保护说明。
- `Routing/ParcelAdminApiRouteExtensions.cs`：Parcel 管理端路由扩展（普通写接口 + cleanup-expired 治理接口 + `/api/admin/parcels/batch-buffer` 批量缓冲写入接口）。
- `Routing/AuditReadOnlyApiRouteExtensions.cs`：Web 请求审计日志只读路由扩展（`GET /api/audit/web-requests`、`GET /api/audit/web-requests/{id}`）。
- `QueryParameters/ParcelListQueryParameters.cs`：Parcel 列表查询参数模型（AsParameters 绑定）。
- `QueryParameters/ParcelCursorListQueryParameters.cs`：Parcel 游标分页查询参数模型（AsParameters 绑定）。
- `QueryParameters/ParcelAdjacentQueryParameters.cs`：Parcel 邻近查询参数模型（AsParameters 绑定）。
- `QueryParameters/WebRequestAuditLogListQueryParameters.cs`：审计日志列表查询参数模型（AsParameters 绑定）。
- `Options/HostingOptions.cs`：`Hosting` 主配置模型及地址/Swagger 拼装逻辑。
- `Options/SwaggerOptions.cs`：Swagger 子配置模型。
- `Options/BrowserAutoOpenOptions.cs`：Development 浏览器自动打开配置模型。
- `Options/AuditReadOnlyApiOptions.cs`：审计只读 API 开关配置模型（`AuditReadOnlyApi:Enabled`）。
- `Options/ResourceThresholdsOptions.cs`：运行时资源阈值告警配置模型（`ResourceThresholds:MaxConnectionPoolSize`、`MemoryWarningThresholdMB` 等）。
- `HealthChecks/BufferedWriteQueueHealthCheck.cs`：批量缓冲写入队列健康检查，输出队列深度、死信数量、背压状态与最近 Flush 时间。
- `HealthChecks/ShardingGovernanceHealthCheck.cs`：分表治理健康检查，输出缺表、缺索引、容量风险、热表/详情表一致性与预建计划状态。
- `HealthChecks/DatabaseConnectionDetailedHealthCheck.cs`：数据库详细健康检查探针，当前挂载于 `/health/ready`，输出 provider、database、连续失败/成功次数与恢复状态。
- `HealthChecks/DatabaseReadinessHealthCheck.cs`：数据库基础就绪健康检查探针，保留原始直接连通性探测实现。
- `HealthChecks/HealthCheckResponseWriter.cs`：健康检查 JSON 响应序列化工具，输出结构化 JSON，并支持附加 Data 诊断数据。
- `Utilities/LocalDateTimeParsing.cs`：本地时间解析与 API 问题响应工厂共享工具。
- `Middleware/WebRequestAuditLogOptions.cs`：Web 请求审计中间件配置模型。
- `Middleware/WebRequestAuditLogMiddleware.cs`：Web 请求审计中间件实现（主请求零阻塞：仅负责采集与入队，不等待写库；补齐 Request/Response Body 采集与可回放 Curl 拼装）。
- `Middleware/WebRequestAuditBackgroundEntry.cs`：审计后台队列项值类型。
- `Middleware/WebRequestAuditBackgroundQueue.cs`：审计有界后台队列（超限丢弃保护与丢弃计数日志）。
- `Middleware/WebRequestAuditBackgroundWorkerHostedService.cs`：审计后台消费服务（单消费者写库）。
- `Middleware/WebRequestAuditLogMiddlewareExtensions.cs`：中间件依赖注册与管线接线扩展。
- `Middleware/ResponseCaptureTeeStream.cs`：响应双写采集流。
- `Middleware/ResponseCaptureResult.cs`：响应正文采集结果值类型。
- `Zeye.Sorting.Hub.Host.csproj`：Host 项目定义。
- `nlog.config`：NLog 日志配置。
- `appsettings.json`：默认运行配置（含 `WebRequestAuditLog.IncludeRequestBody/IncludeResponseBody`、`AuditReadOnlyApi:Enabled` 显式开关、`ResourceThresholds:MaxConnectionPoolSize/MemoryWarningThresholdMB` 资源阈值节、`Persistence:Diagnostics` 数据库连接诊断配置、`Persistence:WriteBuffering` 批量缓冲写入配置、`Persistence:Sharding:RuntimeInspection/Prebuild` 分表巡检与预建配置、`Persistence:AutoTuning:MonthlyReportDay` 月报日期配置）。
- `appsettings.Development.json`：开发环境配置覆盖文件。

#### `Zeye.Sorting.Hub.Host/Swagger/`：Swagger 扩展目录
- `EnumDescriptionSchemaFilter.cs`：枚举 Schema 中文增强过滤器。

#### `Zeye.Sorting.Hub.Host/HostedServices/`：启动/常驻托管服务目录
- `AutoTuningLoggerObservability.cs`：自动调优观测默认日志实现（已移除 `ConvertLogLevel` 转换方法，直接接收 `NLog.LogLevel`）。
- `DatabaseAutoTuningHostedService.cs`：数据库自动调谐托管服务主流程（已移除 `ILogger<>` 注入，改为静态 `NLog.Logger`）。
- `PendingRollbackAction.cs` / `TableCapacitySnapshot.cs` / `EvidenceContext.cs` / `PolicyDecision.cs`：自动调谐内部模型与决策类型。
- `DatabaseInitializerHostedService.cs`：数据库初始化与迁移托管服务主流程（迁移前执行自动建库检查，复用隔离器输出治理审计并衔接 FailFast/Degraded 失败策略）。
- `DatabaseConnectionWarmupHostedService.cs`：数据库连接预热托管服务，启动期调用基础设施诊断服务完成非阻塞预热并兜底异常日志。
- `ParcelBatchWriteFlushHostedService.cs`：Parcel 批量缓冲写入后台 Flush 托管服务，持续消费有界队列并批量落库。
- `ShardingInspectionHostedService.cs`：分表运行期巡检托管服务，按配置周期执行缺表、缺索引与容量风险巡检。
- `ShardingPrebuildHostedService.cs`：分表预建计划托管服务，启动期生成未来窗口 dry-run 预建计划。
- `ShardingGovernanceGuardException.cs`：分表治理守卫异常类型。
- `DevelopmentBrowserLauncherHostedService.cs`：Development 浏览器启动隔离器。

#### `Zeye.Sorting.Hub.Host/Properties/`：项目运行调试属性目录
- `launchSettings.json`：本地调试启动配置。

### `Zeye.Sorting.Hub.Infrastructure/`：基础设施层（EF Core 持久化、仓储实现、DI 注册、数据库方言）
- `Zeye.Sorting.Hub.Infrastructure.csproj`：Infrastructure 项目定义。

#### `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/`：依赖注入扩展目录
- `PersistenceServiceCollectionExtensions.cs`：持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、数据库连接诊断/预热、批量缓冲写入、分表运行期巡检与预建服务注册、Parcel 主表保持按 `CreatedTime` 分表；分表时间粒度由 Time/Volume/Hybrid 统一策略决策驱动，Parcel 关联值对象规则继续复用声明式清单与覆盖守卫）。

#### `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/`：EF Core 实体映射配置目录
- `BagInfoEntityTypeConfiguration.cs`：BagInfo 映射配置。
- `ParcelEntityTypeConfiguration.cs`：Parcel 聚合映射配置（Parcel 主键 Id 改为 `ValueGeneratedNever`，由应用层显式赋值；owned/value-object 子表影子主键继续保持自动生成）。
- `WebRequestAuditLogEntityTypeConfiguration.cs`：Web 请求审计热数据主表映射配置（写优化索引与一对一关系）。
- `WebRequestAuditLogDetailEntityTypeConfiguration.cs`：Web 请求审计冷数据详情表映射配置（大字段落冷表）。

#### `Zeye.Sorting.Hub.Infrastructure/Persistence/`：持久化核心目录（DbContext、方言、设计时工厂）
- `SortingHubDbContext.cs`：EF Core DbContext（实体集与模型构建入口）。
- `DbProviderNames.cs`：EF Core 运行时/迁移 providerName 常量（`Pomelo.EntityFrameworkCore.MySql` / `Microsoft.EntityFrameworkCore.SqlServer`），用于 `DbContext.Database.ProviderName` 识别与迁移分支判断。
- `ConfiguredProviderNames.cs`：配置层 provider key 常量（`MySql` / `SqlServer`），用于 `Persistence:Provider`、`ConnectionStrings` key 与设计时 CLI `--provider` 参数值，避免配置语义与 EF providerName 语义混用。
- `ParcelIndexNames.cs`：Parcel 关键索引名称常量（供分表治理审计与测试复用，避免多处硬编码漂移；包含 BagCode/ActualChuteId/TargetChuteId 三条 ScannedTime 复合索引及 MySQL FULLTEXT 索引名）。
- `WebRequestAuditLogIndexNames.cs`：Web 请求审计日志关键索引名称常量（供关键索引审计与映射复用）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/`：分表策略与运行期治理目录
- `ParcelShardingStrategyEvaluator.cs`：Parcel 分表策略评估器。
- `ShardingRuntimeInspectionOptions.cs`：分表运行期巡检配置模型。
- `ShardingPrebuildOptions.cs`：分表预建计划配置模型。
- `ShardingInspectionReport.cs`：分表巡检报告模型。
- `ShardingPrebuildPlan.cs`：分表预建计划模型。
- `ShardingPhysicalTablePlanBuilder.cs`：分表物理表规划构建器。
- `ShardingCapacitySnapshotService.cs`：分表容量与热点风险快照服务。
- `ShardingIndexInspectionService.cs`：分表关键索引巡检服务。
- `ShardingTableInspectionService.cs`：分表物理表巡检服务。
- `ShardingTablePrebuildService.cs`：分表预建 dry-run 计划服务。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/WriteBuffering/`：批量缓冲写入目录
- `BatchWriteMetricsSnapshot.cs`：批量缓冲写入运行时指标快照，提供队列深度、死信数量、Flush 成败与背压状态。
- `BoundedWriteChannel.cs`：批量缓冲写入有界通道封装，统一容量、深度与丢弃计数维护。
- `BufferedParcelWriteItem.cs`：Parcel 缓冲写入通道项，记录入队时间、重试次数与最近失败信息。
- `DeadLetterWriteEntry.cs`：Parcel 死信记录模型，记录失败时间、重试次数与失败消息。
- `DeadLetterWriteStore.cs`：Parcel 死信有界存储，超过容量时覆盖最旧记录并记录告警。
- `ParcelBatchWriteFlushService.cs`：Parcel 批量缓冲写入后台 Flush 服务，按批次聚合、批量落库并处理重试与死信。
- `ParcelBufferedWriteService.cs`：Parcel 缓冲写入服务实现，负责背压判断与无数据库访问的快速入队。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Diagnostics/`：数据库连接诊断目录
- `DatabaseConnectionDiagnosticsOptions.cs`：数据库连接诊断配置模型，约束预热开关、连接数、探测超时、失败阈值与恢复阈值。
- `DatabaseConnectionHealthSnapshot.cs`：数据库连接诊断快照，记录最近一次探测时间、耗时、成功/失败状态与连续计数。
- `IDatabaseConnectionDiagnostics.cs`：数据库连接诊断抽象，提供主动探测与最近一次快照读取能力。
- `DatabaseConnectionDiagnosticsService.cs`：数据库连接诊断服务，使用 `IDbContextFactory<SortingHubDbContext>` 执行短生命周期探测并缓存最近一次快照。
- `DatabaseConnectionWarmupService.cs`：数据库连接预热服务，按配置在启动期并发预热多个短生命周期连接。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/`：数据库方言抽象与实现目录
- `DatabaseProviderOperations.cs`：数据库提供器操作类（异常错误码提取 `TryGetProviderErrorNumber`、WHERE 列归一化 `NormalizeWhereColumns`、稳定索引名构造 `BuildIndexName`）。
- `DatabaseIdentifierPolicy.cs`：数据库名安全守卫（数据库名格式校验、MySQL/SQL Server 标识符转义）。
- `DatabaseConnectionOpenCoordinator.cs`：数据库连接打开共享工具（统一处理 Open/Broken 状态）。
- `IDatabaseDialect.cs`：数据库方言抽象接口。
- `IShardingPhysicalTableProbe.cs`：分表物理对象探测抽象（最小职责：判断目标物理表是否存在 + 探测目标表缺失索引名集合；仅探测，不执行 DDL）。
- `IBatchShardingPhysicalTableProbe.cs`：分表物理表批量探测抽象（返回缺失集合，并支持按逻辑表名前缀枚举已存在物理分表）。
- `MySqlDialect.cs`：MySQL 方言实现（自动调优 SQL + 分表探测 + 启动期数据库存在性探测与建库执行）。
- `SqlServerDialect.cs`：SQL Server 方言实现（自动调优 SQL + 分表探测 + 启动期数据库存在性探测与建库执行）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/`：自动调谐核心目录
- `IAutoTuningObservability.cs`：自动调优观测输出抽象接口（`EmitEvent` 参数已改为 `NLog.LogLevel`，移除 MEL 依赖）。
- `NullAutoTuningObservability.cs`：观测空实现，未注入观测器时保持兼容（已同步更新为 `NLog.LogLevel`）。
- `ActionIsolationPolicy.cs`：危险动作隔离策略引擎。
- `AutoRollbackDecisionEngine.cs`：自动回滚判定引擎。
- `AutoTuningClosedLoopTracker.cs`：闭环阶段跟踪器。
- `AutoTuningVerificationResultBuilder.cs`：自动验证标准化结果构造器。
- `IExecutionPlanRegressionProbe.cs` / `IProviderAwareExecutionPlanRegressionProbe.cs`：执行计划探针抽象。
- `ExecutionPlanProbeRequest.cs` / `PlanRegressionSnapshot.cs`：执行计划探针请求与结果模型。
- `LoggingOnlyExecutionPlanRegressionProbe.cs`：默认 logging-only 计划探针实现。
- `AutoTuningConfigurationReader.cs`：配置读取工具类，集中提供 `GetPositiveIntOrDefault`、`GetNonNegativeIntOrDefault`、`GetNonNegativeDecimalOrDefault`、`GetDecimalInRangeOrDefault`、`GetDecimalClampedOrDefault`、`GetBoolOrDefault`、`GetPositiveSecondsAsTimeSpanOrDefault`、`GetTimeOfDayOrDefault`，并统一 `BuildAutoTuningKey`、`BuildAutonomousKey` 与 `NormalizeToLocalTime`，消除重复键拼装与时间归一化实现。
- `MySqlSessionBootstrapConnectionInterceptor.cs`：MySQL 连接会话初始化拦截器（类型判断逻辑内联，移除无意义 helper）。
- `SlowQueryAutoTuningPipeline.cs`：慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道（配置键拼装复用 `AutoTuningConfigurationReader`，并提供主表提取公共方法供 HostedService 与建议编排共用）。
- `SlowQueryCommandInterceptor.cs`：EF Core 慢查询采集拦截器。
- `SlowQuerySample.cs`：慢查询采样记录模型。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/`：分表策略与治理决策目录
- `ParcelShardingStrategyEvaluator.cs`：Parcel 分表策略评估器（分表模式/时间粒度/容量阈值/阈值动作配置解析，结构化校验，容量观测输入统一收敛为 Observation 对象，输出含 finer-granularity 扩展规划的统一决策结果，复用于注册入口与启动审计守卫）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/`：EF 设计时支持目录
- `DesignTimeConfigurationLocator.cs`：设计时配置目录定位器，统一 appsettings.json 查找与加载逻辑，消除工厂间重复代码。
- `MySqlContextFactory.cs`：MySQL 设计时 DbContext 工厂（告警通过 NLog 落盘，配置加载统一委托 DesignTimeConfigurationLocator）。
- `SqlServerContextFactory.cs`：SQL Server 设计时 DbContext 构建器（由统一设计时工厂按 provider 分发调用），连接字符串 key 使用 `ConfiguredProviderNames.SqlServer`，提供 SQL Server 连接字符串搜索与 `DbContextOptions` 组装能力。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/`：EF Core 迁移文件目录
- `20260324094539_RebuildBaseline20260324.cs`：全新基线迁移（清空历史后重建，支持空库初始化）。
- `20260324094539_RebuildBaseline20260324.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `MigrationSchemaResolver.cs`：迁移共享 schema 解析器。
- `SortingHubDbContextModelSnapshot.cs`：当前模型快照（自动生成，勿手动修改）。

#### `Zeye.Sorting.Hub.Infrastructure/Repositories/`：仓储基类与结果模型目录
- `RepositoryBase.cs`：通用仓储基类（增删改查 + 自动持久化实现）；接受 `NLog.ILogger` 构造参数，由派生类传入，确保日志来源类名为实际仓储类而非基类名。
- `MemoryCacheRepositoryBase.cs`：带内存缓存失效逻辑的仓储基类，继承 `RepositoryBase`，同样使用 NLog 日志。
- `ParcelCursorQueryExtensions.cs`：Parcel 游标分页查询扩展，集中封装 `ScannedTime DESC, Id DESC` 稳定排序下的游标条件，避免仓储内重复拼接。
- `ParcelRepository.cs`：Parcel 仓储第一阶段实现（复用 `RepositoryBase`、`IDbContextFactory`，使用静态 `NLog.ILogger`，已移除 MEL `ILogger<ParcelRepository>` 构造依赖；提供基础读写、偏移分页、游标分页、按 Id 邻近查询与过期清理；条码检索按 Provider 分支（MySQL FULLTEXT Boolean、其他 Provider Contains）；过期清理纳入隔离器开关 + dry-run + 审计 + 补偿边界声明）。
- `WebRequestAuditLogRepository.cs`：Web 请求审计日志仓储实现，负责热表与冷表详情同事务写入，以及分页列表/按 Id 详情只读查询。

### `Zeye.Sorting.Hub.Realtime/`：实时通信子域（当前为占位工程）
- `Zeye.Sorting.Hub.Realtime.csproj`：Realtime 项目定义。

### `Zeye.Sorting.Hub.RuleEngine/`：规则引擎子域（当前为占位工程）
- `Zeye.Sorting.Hub.RuleEngine.csproj`：RuleEngine 项目定义。

### `Zeye.Sorting.Hub.SharedKernel/`：跨模块共享内核
- `Zeye.Sorting.Hub.SharedKernel.csproj`：SharedKernel 项目定义（已将 `Microsoft.Extensions.Logging.Abstractions` 替换为 `NLog`，与全局日志规范一致）。

#### `Zeye.Sorting.Hub.SharedKernel/Utilities/`：共享工具目录
- `SafeExecutor.cs`：安全执行器；使用 NLog 静态 logger（`LogManager.GetCurrentClassLogger()`），移除了 MEL `ILogger<SafeExecutor>` 构造依赖；提供 `Execute`、`ExecuteAsync`（void）、`ExecuteAsync<T>`（带返回值）三个重载，确保任何异常都不会导致宿主崩溃。
- `ConfigChangeEntry.cs`：配置变更历史记录条目；`sealed record` 值类型，携带变更序号（单调递增）、前值快照、后值快照、本地生效时间与变更字段摘要，供配置变更审计使用。
- `ConfigChangeHistoryStore.cs`：配置变更历史存储器；泛型环形缓冲实现，线程安全，保留最近 N 次配置快照（默认 10 条）；暴露 `Record(previous, current, changedFields)` 写入快照、`GetHistory()` 获取历史与 `GetLatest()` 获取最新快照，供配置变更审计与回滚查询使用。
- `LineBreakNormalizer.cs`：换行标准化工具；统一将 CR/LF 归一化为空格且仅在存在换行符时分配新字符串，供日志路径/标签清洗复用，避免同义逻辑分散实现。

### `Zeye.Sorting.Hub.Host.Tests/`：API 与应用层测试层
- `Zeye.Sorting.Hub.Host.Tests.csproj`：xUnit 测试项目定义。
- `AutoTuningProductionControlTests.cs`：覆盖 dry-run、危险动作隔离、告警防抖与恢复、普通/严重回归、unavailable 指标处理、执行计划探针 available/unavailable 双路径、闭环链路与分表覆盖守卫校验、迁移失败策略分环境解析、结构化扩容计划解析、Time/Volume/Hybrid 分表策略评估、PerDay 预建守卫（配置+物理探测）与分表观测口径/自动索引过滤规则回归；新增 WebRequestAuditLog 治理解耦与历史保留治理语义回归；含配置键拼装参数化（Theory）覆盖。
- `AlwaysExistsShardingPhysicalTableProbe.cs`：物理表探测测试桩，始终返回存在并记录调用次数。
- `BatchSelectiveMissingShardingPhysicalTableProbe.cs`：批量物理表探测测试桩，支持选择性缺失结果与 schema 透传断言。
- `CountingPlanProbe.cs`：执行计划探针测试桩，记录探针调用次数并返回固定快照。
- `DomainEventArgsTests.cs`：领域事件载荷单元测试，验证 `ParcelScannedEventArgs`/`ParcelChuteAssignedEventArgs` 业务字段赋值、值语义相等与不等、本地时间约束。
- `EmptyServiceScope.cs`：最小服务作用域测试桩，提供基础 `ServiceProvider`。
- `EmptyServiceScopeFactory.cs`：最小服务作用域工厂测试桩。
- `FakeParcelRepository.cs`：Parcel 仓储测试替身，提供只读/写入/过期清理三态结果用于 API 回归测试。
- `FixedPlanProbe.cs`：执行计划探针测试桩，固定返回“探针可用且无回归”。
- `LocalTimeTestConstraint.cs`：测试层本地时间语义约束工具类，提供 `CreateLocalTime`/`AssertIsLocalTime`/`AssertNotUtc` 方法，防止测试代码引入 UTC 语义。
- `LogCleanupServiceTests.cs`：日志清理服务回归测试，验证目录栈递归扫描子目录日志并仅清理超过保留天数的旧日志文件。
- `ConfigChangeHistoryStoreTests.cs`：配置变更历史存储器单元测试，覆盖空历史、单条记录、多条按序排列、超容量环形覆盖与 LogCleanupService 热加载联动快照记录五个场景。
- `MissingIndexShardingPhysicalTableProbe.cs`：关键索引缺失探测测试桩，按物理表返回缺失索引。
- `ConfigurableShardingPhysicalTableProbe.cs`：可配置分表物理对象探测测试桩，支持按需模拟缺表与缺索引。
- `ShardingInspectionTests.cs`：分表巡检、预建计划与健康检查回归测试，覆盖物理表规划、dry-run 预建、缺索引、缺表与健康检查状态。
- `NullScope.cs`：通用测试日志空作用域单例。
- `ObservabilityEntry.cs`：自动调优观测记录模型，承载名称/值/标签快照。
- `ParcelAdminApiTests.cs`：Parcel 管理端写接口测试，覆盖新增成功路径、创建请求 `id<=0` 返回 400、重复 Id 返回 409、UTC 时间拒绝、更新状态成功路径 + 不存在 404 + 非法操作码 400、删除成功路径 + 不存在 404、cleanup-expired blocked/dry-run/execute 三态 + UTC 时间与非法参数拒绝。
- `ParcelReadOnlyApiTests.cs`：Parcel 只读 API 端点测试，覆盖列表查询、详情查询、详情不存在返回 404、`/api/parcels/adjacent` 按 `id` 查询的 400/404/稳定排序回归。
- `ParcelCursorQueryTests.cs`：Parcel 游标分页与查询保护回归测试，覆盖首页、翻页、非法游标、页大小归一化、默认最近 24 小时窗口、普通分页页码保护与仓储稳定排序。
- `ParcelBufferedWriteTests.cs`：Parcel 批量缓冲写入回归测试，覆盖入队、回压、单批 Flush、死信隔离、队列健康检查与 batch-buffer 接口。
- `DatabaseConnectionDiagnosticsTests.cs`：数据库连接诊断回归测试，覆盖默认配置、非法配置、失败快照、失败阈值、本地时间语义与健康检查 Data 输出。
- `AuditReadOnlyApiTests.cs`：Web 请求审计日志只读 API 端点测试，覆盖默认分页、过滤组合、非法分页 400、非法时间格式 400、详情 200/404 全字段断言、中间件写读联动。
- `SortingHubTestDbContextFactory.cs`：Host.Tests 通用 InMemory `DbContextFactory`，供查询服务测试与仓储测试复用。
- `WebRequestAuditLogRepositoryTests.cs`：Web 请求审计日志仓储测试，覆盖 DI 解析、冷热一对一落库与最小写入服务入口。
- `WebRequestAuditLogMiddlewareTests.cs`：Web 请求审计中间件回归测试，覆盖开关/采样、正常与异常链路、请求响应体截断、采集异常隔离、Curl 可回放断言与真实仓储冷热落库。
- `InMemoryWebRequestAuditLogRepository.cs`：Web 请求审计日志内存仓储测试替身，支持成功/失败/异常三种写入行为。
- `ParcelQueryServicesTests.cs`：Parcel 应用层查询服务测试（列表/详情/邻近查询映射与最小参数校验）；新增邻近查询锚点不存在异常场景；多重过滤条件联合成功路径（bagCode + workstationName + actualChuteId + status）；ExceptionType 筛选成功路径与非法值校验。
- `ParcelRepositoryTests.cs`：Parcel 仓储第一阶段能力测试，覆盖分页过滤、详情与按 Id 邻近查询、新增/更新/删除、过期清理与批量新增；新增同一扫描时间稳定排序、锚点不存在、重复主键冲突语义回归，并验证危险清理动作的 blocked/dry-run/executed 三态。
- `SelectiveMissingShardingPhysicalTableProbe.cs`：物理表探测测试桩，模拟指定分表缺失场景。
- `HostingOptionsTests.cs`：Hosting 配置单元测试，覆盖监听地址分号拆分去重、`0.0.0.0` 归一化为 `localhost` 的 Swagger 地址拼装、`BrowserAutoOpen:Url` 显式配置优先级与无效监听地址返回 null 的兜底行为。
- `SwaggerDocumentationTests.cs`：Swagger 文档增强回归测试，覆盖管理端更新请求、值对象响应枚举字段与审计日志只读端点声明。
- `TestDialect.cs`：通用数据库方言测试桩，提供默认 ProviderName 测试分支。
- `TestHostEnvironment.cs`：`IHostEnvironment` 测试桩，注入环境名与最小内容根配置。
- `TestLogger.cs`：通用泛型日志测试桩，收集日志消息供断言。
- `TestMySqlDialect.cs`：MySQL ProviderName 方言测试桩。
- `TestObservability.cs`：自动调优观测测试桩，收集指标与事件输出。
- `TestOptionsMonitor.cs`：`IOptionsMonitor<T>` 测试桩，允许测试中直接注入配置值并通过 `Update` 方法模拟热加载变更。
- `OptionsMonitorSubscription.cs`：`IOptionsMonitor.OnChange` 订阅句柄，`Dispose` 时执行取消订阅回调，供 `TestOptionsMonitor<T>` 复用。
- `TestSqlServerDialect.cs`：SQL Server ProviderName 方言测试桩。
- `ThrowOnReadStream.cs`：读取即抛异常的请求体流测试桩，用于验证中间件请求体采集异常隔离不影响主链路。


## 本次更新内容

- 继续实施《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》，当前 PR 累计覆盖连接诊断/预热、游标分页、批量缓冲写入、死信隔离与分表治理切片。
- 新增 Parcel 游标分页查询、普通分页默认最近 24 小时窗口与页码上限保护。
- 新增 Parcel 批量缓冲写入、有界通道、后台 Flush、失败重试、死信隔离、队列健康检查与管理端批量入队接口。
- 新增数据库连接诊断/预热、分表物理表规划、运行期巡检、预建 dry-run 计划、关键索引巡检、容量风险快照与分表治理健康检查。
- 同步新增 PR-A 至 PR-D 长期数据库底座检查台账，记录各切片现状核对、交付清单与下一 PR 入口。

## 后续可完善点

- 下一切片可按《Zeye.Sorting.Hub-长期数据库底座多PR实施方案与Copilot严格门禁.md》进入 PR-E，补齐数据归档与冷热分层。
- 可在后续“检查结果 PR”中按目录拆分台账附件，形成可直接追踪到文件与行号的持续治理闭环。
- 可将其它日志输入清洗路径（如 query/header 维度）逐步迁移至 `LineBreakNormalizer`，进一步压缩重复实现面并统一观测口径。
- 可为日志清理服务补充“扫描目录无权限/文件被占用”场景测试，进一步验证失败计数与日志观测一致性。
- 可扩展递归扫描的可观测指标（扫描目录数、扫描文件数、跳过数），便于大规模日志目录治理调优。

## Parcel API 发布门禁 / 使用边界说明

> **以下为最小化发布门禁准则，在正式环境上线前必须逐条确认。**

### 一、只读接口（可先行开放）

| 接口 | 路径 | 可开放条件 |
|------|------|-----------|
| 分页列表查询 | `GET /api/parcels` | 无需鉴权，可先行对内部系统开放 |
| 详情查询 | `GET /api/parcels/{id}` | 无需鉴权，可先行对内部系统开放 |
| 邻近查询 | `GET /api/parcels/adjacent` | 无需鉴权，可先行对内部系统开放 |

- 只读接口无副作用，建议先行开放并验证查询链路可用性。
- 若有多租户/数据隔离需求，应在开放前完成数据范围过滤的鉴权接入（添加 `.RequireAuthorization("ReadPolicy")`）。

### 二、管理端写接口（默认仅内网/受控开放）

| 接口 | 路径 | 开放条件 |
|------|------|---------|
| 新增 Parcel | `POST /api/admin/parcels` | 须接入鉴权（AdminPolicy）+ 内网限制 |
| 更新 Parcel 状态 | `PUT /api/admin/parcels/{id}` | 须接入鉴权（AdminPolicy）+ 内网限制 |
| 删除 Parcel | `DELETE /api/admin/parcels/{id}` | 须接入鉴权（AdminPolicy）+ 内网限制 |

- 管理端写接口默认仅在受控内网环境开放，**禁止直接暴露在公网**。
- 正式上线前须在 `MapParcelAdminApis` 的 `MapGroup` 上追加 `.RequireAuthorization("AdminPolicy")`。
- 鉴权方案（JWT / API-Key / RBAC）选型确定后一并实施。

### 三、危险治理接口（必须结合配置、审计和权限）

| 接口 | 路径 | 开放条件 |
|------|------|---------|
| 过期清理 | `POST /api/admin/parcels/cleanup-expired` | 必须结合配置开关 + dry-run + 审计 + 权限 |

- **当前状态**：隔离器默认配置为守卫阻断 + dry-run 模式，真实执行需显式调整 `appsettings.json`。
- **上线前必须满足**：
  1. `Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator` 配置已审核并锁定。
  2. 接口追加 `.RequireAuthorization("DangerousActionPolicy")` 严格限制调用方。
  3. 审计日志（BarCodes、PlannedCount、ExecutedCount、Decision）已落盘且可查询。
  4. 有明确的回滚/补偿预案（当前为"此操作不可逆，回滚需从备份恢复"）。

### 四、后续应补充的上线保障

1. **鉴权/授权体系**：引入 JWT 或 API-Key，区分只读权限（ReadPolicy）、管理员权限（AdminPolicy）、危险操作权限（DangerousActionPolicy）。
2. **限流策略**：为写接口和危险接口配置速率限制（Rate Limiting），防止误操作大量触发。
3. **审计看板**：建立清理接口调用记录可视化看板，显示 blocked / dry-run / execute 次数趋势。
4. **回滚/补偿资产**：为危险删除操作建立可执行的数据归档方案，将当前文本边界升级为可执行治理资产。
