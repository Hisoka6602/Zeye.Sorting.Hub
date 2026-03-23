# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .github（Copilot 仓库级指令目录）
│   ├── copilot-instructions.md（Copilot 自定义指令：禁止 UTC、统一本地时间）
│   └── workflows（CI 工作流目录）
│       └── ef-migration-validation.yml（EF 迁移验收流水线：MySQL+SQL Server 双 Provider 执行 dotnet ef list/update/script）
├── .gitattributes（Git 属性配置）
├── .gitignore（Git 忽略规则）
├── 待完善事项.md（待完善事项列表，仅记录代码中尚未实现的可完善点）
├── 更新记录.md（更新记录，按时间倒序记录每次 PR 更新内容）
├── README.md（仓库总览、结构清单与维护规范）
├── Zeye.Sorting.Hub.Analytics（分析与报表子域，占位工程）
│   └── Zeye.Sorting.Hub.Analytics.csproj（Analytics 项目定义）
├── Zeye.Sorting.Hub.Application（应用层）
│   ├── Services（应用服务目录）
│   │   └── Parcels（Parcel 查询应用服务目录）
│   │       ├── CleanupExpiredParcelsCommandService.cs（过期包裹清理应用服务（治理型，调用仓储隔离器，不可绕过））
│   │       ├── CreateParcelCommandService.cs（管理端新增包裹应用服务）
│   │       ├── DeleteParcelCommandService.cs（管理端删除单个包裹应用服务）
│   │       ├── GetAdjacentParcelsQueryService.cs（Parcel 邻近查询应用服务）
│   │       ├── GetParcelByIdQueryService.cs（Parcel 详情查询应用服务）
│   │       ├── GetParcelPagedQueryService.cs（Parcel 分页查询应用服务）
│   │       ├── ParcelContractMapper.cs（Parcel 领域模型到 Contracts 模型映射器）
│   │       └── UpdateParcelStatusCommandService.cs（管理端更新包裹状态应用服务（仅支持领域允许的状态转换））
│   ├── Utilities（应用层内部共享工具目录）
│   │   ├── EnumGuard.cs（枚举值合法性校验工具：统一封装 Enum.IsDefined + Warn 日志 + 异常抛出）
│   │   └── Guard.cs（基础参数边界守卫工具：ThrowIfZeroOrNegative / ThrowIfNegative，消除各服务重复检查代码）
│   └── Zeye.Sorting.Hub.Application.csproj（Application 项目定义）
├── Zeye.Sorting.Hub.Contracts（契约层）
│   ├── Enums（契约层枚举目录）
│   │   └── Parcels（Parcel 枚举目录）
│   │       ├── ParcelExceptionType.cs（包裹异常类型对外合同枚举：与 Domain.ParcelExceptionType 数值一一对应，供 API 客户端按语义筛选）
│   │       └── ParcelUpdateOperation.cs（Parcel 更新操作类型枚举：MarkCompleted/MarkSortingException/UpdateRequestStatus）
│   ├── Models（对外合同模型目录）
│   │   └── Parcels（Parcel 合同目录）
│   │       ├── Admin（管理端写接口合同目录）
│   │       │   ├── ParcelCleanupExpiredRequest.cs（过期清理治理接口请求合同）
│   │       │   ├── ParcelCleanupExpiredResponse.cs（过期清理治理接口响应合同（含决策/计划量/执行量/补偿边界））
│   │       │   ├── ParcelCreateRequest.cs（管理端新增包裹请求合同）
│   │       │   └── ParcelUpdateRequest.cs（管理端更新包裹状态请求合同）
│   │       ├── ParcelAdjacentRequest.cs（Parcel 邻近查询请求合同）
│   │       ├── ParcelAdjacentResponse.cs（Parcel 邻近查询响应合同）
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
│   │   └── IEntity.cs（实体通用接口）
│   ├── Aggregates（领域聚合目录）
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
│   │   └── VolumeSourceType.cs（体积来源类型枚举）
│   ├── Primitives（领域基础类型目录）
│   │   └── AuditableEntity.cs（可审计实体基类）
│   ├── Repositories（领域仓储契约目录）
│   │   ├── IParcelRepository.cs（包裹仓储接口，含过期清理危险动作治理结果契约）
│   │   └── Models（Parcel 仓储查询与分页模型目录）
│   │       ├── Filters（查询过滤模型目录）
│   │       │   └── ParcelQueryFilter.cs（Parcel 查询过滤模型）
│   │       ├── Paging（通用分页模型目录）
│   │       │   ├── PageRequest.cs（通用分页请求模型）
│   │       │   └── PageResult.cs（通用分页结果模型）
│   │       ├── ReadModels（查询读模型目录）
│   │       │   └── ParcelSummaryReadModel.cs（Parcel 列表摘要读模型）
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
│   │   ├── DatabaseAutoTuningHostedService.cs（数据库自动调谐托管服务（闭环阶段流转、执行隔离、自动验证标准化输出与回滚审计；分表命中/跨表占比/热点倾斜改为全量慢 SQL 口径，并在自动索引建议前做覆盖/重复/低价值过滤））
│   │   ├── DatabaseInitializerHostedService.cs（数据库初始化与迁移托管服务（含分表治理基线、Runbook 审计、PerDay 手工预建窗口守卫；新增“配置清单 + 物理分表存在性 + 关键索引一致性审计（阻断项与仅审计项分离）”三重校验））
│   │   └── DevelopmentBrowserLauncherHostedService.cs（Development 启动浏览器隔离器：仅 Development + 配置开启 + 交互式/本机/非容器/非CI 场景生效；在 ApplicationStarted 后再打开 Swagger，异常由 SafeExecutor 隔离）
│   ├── Program.cs（应用入口与 Host 构建流程；运行地址/Swagger 路径由 appsettings 的 Hosting 配置驱动；接入 XML 注释与枚举中文说明增强）
│   ├── HostingOptions.cs（Hosting 配置模型与 Swagger/浏览器自动打开地址拼装逻辑）
│   ├── LocalDateTimeParsing.cs（本地时间解析与 API 响应工厂共享工具：TryParseLocalDateTime/TryParseOptionalLocalDateTime/CreateBadRequestProblem/CreateParcelMissingProblem，供所有路由扩展复用）
│   ├── ParcelAdminApiRouteExtensions.cs（Parcel 管理端 API 路由扩展：POST/PUT/DELETE 普通写接口 + cleanup-expired 治理接口）
│   ├── Swagger（Swagger 扩展目录）
│   │   └── EnumDescriptionSchemaFilter.cs（枚举 Schema 中文增强：真实 enum 与 Contracts 中枚举数值 int 字段均显示“数值=枚举名（中文描述）”）
│   ├── Properties（运行调试属性目录）
│   │   └── launchSettings.json（本地调试启动配置：Development 环境变量 + 本地 applicationUrl，浏览器自动打开由运行时隔离器负责）
│   ├── Worker.cs（后台轮询任务示例服务）
│   ├── Zeye.Sorting.Hub.Host.csproj（Host 项目定义）
│   ├── nlog.config（NLog 日志配置：双路落盘，低开销异步写盘）
│   ├── appsettings.Development.json（开发环境配置）
│   └── appsettings.json（默认运行配置（含分表策略结构化 Observation、PerDay 预建日期清单与仓储危险动作隔离默认策略））
├── Zeye.Sorting.Hub.Host.Tests（自动调优行为测试工程）
│   ├── AutoTuningProductionControlTests.cs（自动调优生产可控能力测试：dry-run/隔离器/告警恢复/普通与严重回归/探针双路径/闭环链路；含分表策略评估与 PerDay 预建守卫联动测试；配置键拼装参数化覆盖（Theory））
│   ├── AlwaysExistsShardingPhysicalTableProbe.cs（物理表探测测试桩：始终存在场景，支撑分表守卫探测调用断言）
│   ├── BatchSelectiveMissingShardingPhysicalTableProbe.cs（批量物理表探测测试桩：选择性缺失与 schema 透传断言）
│   ├── CaptureNullScope.cs（Warning 日志捕获器专用空作用域单例）
│   ├── CaptureWarningLogger.cs（Warning 日志捕获测试桩：收集告警消息供断言）
│   ├── CountingPlanProbe.cs（执行计划探针测试桩：记录调用次数）
│   ├── DomainEventArgsTests.cs（领域事件载荷单元测试：验证 ParcelScannedEventArgs/ParcelChuteAssignedEventArgs 业务字段赋值与值语义）
│   ├── EmptyServiceScope.cs（最小服务作用域测试桩）
│   ├── EmptyServiceScopeFactory.cs（最小服务作用域工厂测试桩）
│   ├── FakeParcelRepository.cs（Parcel 只读/管理端 API 复用仓储测试替身）
│   ├── FixedPlanProbe.cs（执行计划探针测试桩：固定返回可用快照）
│   ├── LocalTimeTestConstraintHelper.cs（测试层本地时间语义约束工具类：提供 CreateLocalTime/AssertIsLocalTime/AssertNotUtc，防止测试引入 UTC 语义）
│   ├── MissingIndexShardingPhysicalTableProbe.cs（索引缺失探测测试桩：按表返回缺失索引）
│   ├── NullScope.cs（通用测试日志空作用域单例）
│   ├── ObservabilityEntry.cs（自动调优观测记录模型）
│   ├── HostingOptionsTests.cs（Hosting 配置拼装测试：监听地址拆分、Swagger 地址拼装、显式地址优先级与无效监听地址兜底）
│   ├── ParcelAdminApiTests.cs（Parcel 管理端写接口测试：新增/更新状态/删除成功路径 + cleanup-expired 三态 + 参数非法校验）
│   ├── ParcelReadOnlyApiTests.cs（Parcel 只读 API 端点测试：列表/详情/404/邻近参数异常）
│   ├── SortingHubTestDbContextFactory.cs（Host.Tests 通用 InMemory DbContextFactory，供查询服务/仓储测试复用）
│   ├── ParcelQueryServicesTests.cs（Parcel 应用层查询服务测试：列表/详情/邻近查询映射与最小校验；多重过滤条件联合成功路径；ExceptionType 筛选覆盖）
│   ├── ParcelRepositoryTests.cs（Parcel 仓储第一阶段能力测试：分页过滤、详情与邻近查询、写操作与过期清理；含阻断/dry-run/显式放开的危险动作治理回归）
│   ├── SelectiveMissingShardingPhysicalTableProbe.cs（物理表探测测试桩：选择性缺失场景）
│   └── Zeye.Sorting.Hub.Host.Tests.csproj（xUnit 测试项目定义）
│   ├── TestDialect.cs（通用数据库方言测试桩）
│   ├── TestHostEnvironment.cs（IHostEnvironment 测试桩）
│   ├── TestLogger.cs（通用泛型日志测试桩）
│   ├── TestMySqlDialect.cs（MySQL ProviderName 方言测试桩）
│   ├── TestObservability.cs（自动调优观测测试桩：收集指标与事件）
│   └── TestSqlServerDialect.cs（SQL Server ProviderName 方言测试桩）
├── Zeye.Sorting.Hub.Infrastructure（基础设施层）
│   ├── DependencyInjection（依赖注入扩展目录）
│   │   └── PersistenceServiceCollectionExtensions.cs（持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、分表规则与覆盖守卫；Parcel 主表始终按 CreatedTime 路由，时间/容量/混合策略决策由统一评估器驱动））
│   ├── EntityConfigurations（EF Core 映射配置目录）
│   │   ├── BagInfoEntityTypeConfiguration.cs（BagInfo 映射配置）
│   │   └── ParcelEntityTypeConfiguration.cs（Parcel 映射配置）
│   ├── Persistence（持久化核心目录）
│   │   ├── AutoTuning（自动调谐核心目录）
│   │   │   ├── AutoTuningConfigurationHelper.cs（配置读取与本地时间语义归一化/配置键拼装公共辅助类，统一 AutoTuning 键名与时间语义）
│   │   │   ├── MySqlSessionBootstrapConnectionInterceptor.cs（MySQL 连接会话初始化拦截器，直连类型判断，无额外转发）
│   │   │   ├── SlowQueryAutoTuningPipeline.cs（慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道；新增主表提取公共方法供 AutoTuning 主链路复用）
│   │   │   ├── SlowQueryCommandInterceptor.cs（EF Core 慢查询采集拦截器）
│   │   │   └── SlowQuerySample.cs（慢查询采样记录模型）
│   │   ├── Sharding（分表策略与治理决策目录）
│   │   │   ├── ParcelShardingStrategyEvaluator.cs（Parcel 分表策略评估器：配置解析、结构化校验、容量观测输入收敛、阈值决策、finer-granularity 扩展规划与统一决策快照）
│   │   │   └── Enums（分表策略枚举目录）
│   │   │       ├── ParcelFinerGranularityMode.cs（PerDay 仍过热时下一层细粒度模式枚举：None/PerHour/BucketedPerDay）
│   │   │       ├── ParcelFinerGranularityPlanLifecycle.cs（finer-granularity 扩展规划生命周期枚举：PlanOnly/AlertOnly/FutureExecutable）
│   │   │       ├── ParcelAggregateShardingRuleKind.cs（Parcel 聚合分表规则类别枚举：Date/Hash）
│   │   │       ├── ParcelShardingStrategyMode.cs（分表模式枚举：Time/Volume/Hybrid）
│   │   │       ├── ParcelTimeShardingGranularity.cs（时间粒度枚举：PerMonth/PerDay）
│   │   │       └── ParcelVolumeThresholdAction.cs（容量阈值动作枚举：AlertOnly/SwitchToPerDay）
│   │   ├── DatabaseDialects（数据库方言目录）
│   │   │   ├── DatabaseProviderExceptionHelper.cs（数据库异常错误码提取与方言共享索引构造辅助类）
│   │   │   ├── IDatabaseDialect.cs（数据库方言接口）
│   │   │   ├── IShardingPhysicalTableProbe.cs（分表物理对象探测接口：支持物理表存在性与关键索引缺失探测（仅探测，不执行 DDL））
│   │   │   ├── IBatchShardingPhysicalTableProbe.cs（批量分表物理表存在性探测接口，支持一次性探测多张表是否存在）
│   │   │   ├── MySqlDialect.cs（MySQL 方言实现：自动调优 SQL + 物理分表存在性探测 + 关键索引缺失探测）
│   │   │   └── SqlServerDialect.cs（SQL Server 方言实现：自动调优 SQL + 物理分表存在性探测 + 关键索引缺失探测）
│   │   ├── DesignTime（EF 设计时支持目录）
│   │   │   ├── MySqlContextFactory.cs（统一设计时 DbContext 工厂，支持 --provider 切换 MySql/SqlServer）
│   │   │   └── SqlServerContextFactory.cs（SQL Server 设计时 DbContext 构建器）
│   │   ├── Migrations（EF Core 迁移文件目录）
│   │   │   ├── 20260316184030_InitialCreate.cs（初始迁移：全部表建表与回滚逻辑）
│   │   │   ├── 20260316184030_InitialCreate.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260317024345_UseAttributeBasedIndexesAndPrecision.cs（索引/精度特征标记对齐迁移）
│   │   │   ├── 20260317024345_UseAttributeBasedIndexesAndPrecision.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260317062930_SplitParcelStatusAndExceptionType.cs（Parcel 状态拆分与异常类型字段迁移）
│   │   │   ├── 20260317062930_SplitParcelStatusAndExceptionType.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260318024421_OptimizeParcelAggregateQueryIndexes.cs（Parcel 聚合高频查询索引优化迁移）
│   │   │   ├── 20260318024421_OptimizeParcelAggregateQueryIndexes.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260322050329_OptimizeBagCodeAndActualChuteIdQueryIndexes.cs（BagCode 单列→复合索引 + ActualChuteId_ScannedTime 新增复合索引迁移）
│   │   │   ├── 20260322050329_OptimizeBagCodeAndActualChuteIdQueryIndexes.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260322072600_AddBarCodesFullTextIndex.cs（BarCodes 列 FULLTEXT 全文索引迁移，仅 MySQL）
│   │   │   ├── 20260322072600_AddBarCodesFullTextIndex.Designer.cs（迁移元数据，自动生成）
│   │   │   ├── 20260323045038_UseExternalProvidedParcelId.cs（Parcel 主表主键改为外部提供，移除主键自动生成策略）
│   │   │   ├── 20260323045038_UseExternalProvidedParcelId.Designer.cs（迁移元数据，自动生成）
│   │   │   └── SortingHubDbContextModelSnapshot.cs（当前模型快照，自动生成）
│   │   └── SortingHubDbContext.cs（EF Core DbContext）
│   │   ├── DbProviderNames.cs（EF Core 运行时/迁移 providerName 常量）
│   │   └── ConfiguredProviderNames.cs（配置层 provider key 常量：Persistence:Provider / ConnectionStrings key / CLI --provider）
│   ├── Repositories（仓储基类与结果模型目录）
│   │   ├── MemoryCacheRepositoryBase.cs（带内存缓存失效的仓储基类，使用 NLog 日志）
│   │   ├── ParcelRepository.cs（Parcel 仓储第一阶段实现，使用静态 NLog logger，无需 MEL ILogger 构造注入；BarCodeKeyword 检索按 Provider 分支：MySQL 走 FULLTEXT Boolean，其他 Provider 回退 Contains）
│   │   └── RepositoryBase.cs（通用仓储基类，接受 NLog.ILogger 构造参数，由派生类传入确保日志来源类名正确）
│   └── Zeye.Sorting.Hub.Infrastructure.csproj（Infrastructure 项目定义）
├── Zeye.Sorting.Hub.Realtime（实时通信子域，占位工程）
│   └── Zeye.Sorting.Hub.Realtime.csproj（Realtime 项目定义）
├── Zeye.Sorting.Hub.RuleEngine（规则引擎子域，占位工程）
│   └── Zeye.Sorting.Hub.RuleEngine.csproj（RuleEngine 项目定义）
├── Zeye.Sorting.Hub.SharedKernel（共享内核）
│   ├── Utilities（共享工具目录）
│   │   └── SafeExecutor.cs（安全执行器：使用 NLog 静态 logger，不再依赖 MEL ILogger 构造注入；隔离任何异常，Execute/ExecuteAsync 确保副作用不会导致宿主崩溃）
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

### `.github/`：Copilot 仓库级指令目录
- `copilot-instructions.md`：Copilot 自定义指令，硬性要求禁止 UTC 时间 API，统一使用本地时间语义。

### `.github/workflows/`：CI 工作流目录
- `ef-migration-validation.yml`：EF 迁移验收流水线（MySQL + SQL Server 容器环境），真实执行 `dotnet ef migrations list`、`dotnet ef database update`、`dotnet ef migrations script` 三项门禁命令。

### `Zeye.Sorting.Hub.Analytics/`：分析与报表子域（当前为占位工程）
- `Zeye.Sorting.Hub.Analytics.csproj`：Analytics 项目定义。
- `Class1.cs`：占位类，预留统计指标/报表能力实现位置。

### `Zeye.Sorting.Hub.Application/`：应用层（Use Case 编排层）
- `Zeye.Sorting.Hub.Application.csproj`：Application 项目定义（引用 Domain + Contracts，承载应用服务实现）。

#### `Zeye.Sorting.Hub.Application/Utilities/`：应用层内部共享工具目录
- `EnumGuard.cs`：枚举值合法性校验工具；统一封装 `Enum.IsDefined` 判断、Warn 日志记录与 `ArgumentOutOfRangeException` 抛出，消除各应用服务中重复的枚举验证模板代码；提供 `int` 和 `int?` 两个重载。
- `Guard.cs`：基础参数边界守卫工具；提供 `ThrowIfZeroOrNegative`（Id 正数校验，有 long/int 两个重载）和 `ThrowIfNegative`（可选数量非负校验），统一记录 Warn 日志并抛出 `ArgumentOutOfRangeException`。

#### `Zeye.Sorting.Hub.Application/Services/Parcels/`：Parcel 应用服务目录（查询 + 管理端写命令）
- `GetParcelByIdQueryService.cs`：按 Id 查询 Parcel 详情应用服务（仓储调用 + 合同映射 + 最小参数校验）。
- `GetParcelPagedQueryService.cs`：分页查询 Parcel 列表应用服务（请求校验、过滤映射、分页结果映射）。
- `GetAdjacentParcelsQueryService.cs`：按包裹 Id 查询邻近 Parcel 应用服务（数量归一化至 `IParcelRepository.MaxAdjacentCountPerSide`、响应映射；锚点不存在抛 KeyNotFoundException 供 Host 映射 404）。
- `ParcelContractMapper.cs`：Parcel 领域模型/读模型到 Contracts 模型的统一映射器，避免 Host 层重复映射。
- `CreateParcelCommandService.cs`：管理端新增包裹应用服务（枚举验证、领域工厂 Parcel.Create、仓储 AddAsync、合同映射）。
- `UpdateParcelStatusCommandService.cs`：管理端更新包裹状态应用服务（仅支持 MarkCompleted/MarkSortingException/UpdateRequestStatus 三种领域方法，不允许任意字段修改）。
- `DeleteParcelCommandService.cs`：管理端删除单个包裹应用服务（先加载聚合根，不存在返回 false，再调用 RemoveAsync）。
- `CleanupExpiredParcelsCommandService.cs`：过期包裹清理应用服务（治理型，调用仓储 RemoveExpiredAsync，不绕过隔离器，映射 DangerousBatchActionResult 为外部合同响应）。

### `Zeye.Sorting.Hub.Contracts/`：契约层（对外 DTO / 接口模型）
- `Zeye.Sorting.Hub.Contracts.csproj`：Contracts 项目定义。

#### `Zeye.Sorting.Hub.Contracts/Models/Parcels/`：Parcel 对外查询合同目录
- `ParcelListRequest.cs`：Parcel 列表查询请求合同（分页 + 过滤参数）。
- `ParcelListItemResponse.cs`：Parcel 列表项响应合同（扁平化字段，不暴露领域聚合根）。
- `ParcelListResponse.cs`：Parcel 列表分页响应合同。
- `ParcelDetailResponse.cs`：Parcel 详情响应合同（继承列表项扁平字段，并包含所有联表值对象内容）。
- `ParcelAdjacentRequest.cs`：Parcel 邻近查询请求合同。
- `ParcelAdjacentResponse.cs`：Parcel 邻近查询响应合同。

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
- `ParcelCreateRequest.cs`：管理端新增包裹请求合同（含调用方传入的包裹 Id，要求大于 0 且全局唯一；时间字段为本地时间字符串，由 API 层统一解析并拒绝 UTC/offset）。
- `ParcelUpdateRequest.cs`：管理端更新包裹状态请求合同（Operation 枚举决定操作类型，对应 CompletedTime/ExceptionType/RequestStatus）。
- `ParcelCleanupExpiredRequest.cs`：过期清理治理接口请求合同（CreatedBefore 本地时间字符串，API 层强制解析校验）。
- `ParcelCleanupExpiredResponse.cs`：过期清理治理接口响应合同（ActionName/Decision/PlannedCount/ExecutedCount/IsDryRun/IsBlockedByGuard/CompensationBoundary）。

### `Zeye.Sorting.Hub.Domain/`：核心领域层，存放聚合根、值对象、领域事件、枚举与仓储接口
- `Zeye.Sorting.Hub.Domain.csproj`：Domain 项目定义。

#### `Zeye.Sorting.Hub.Domain/Abstractions/`：领域抽象接口层
- `IEntity.cs`：实体通用接口（定义主键契约）。

#### `Zeye.Sorting.Hub.Domain/Aggregates/`：领域聚合目录

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

#### `Zeye.Sorting.Hub.Domain/Primitives/`：领域基础类型目录
- `AuditableEntity.cs`：可审计实体基类（创建/修改信息等）。

#### `Zeye.Sorting.Hub.Domain/Repositories/`：领域仓储契约目录
- `IParcelRepository.cs`：包裹仓储接口（第一阶段可落地契约：基础读写、分页查询、按 Id 邻近查询、过期清理危险动作治理结果返回；同时定义 `MaxAdjacentCountPerSide = 200` 常量，为 Application 层与 Infrastructure 层提供唯一权威数字来源，禁止各自硬编码）。

##### `Zeye.Sorting.Hub.Domain/Repositories/Models/`：Parcel 仓储查询模型目录

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/`：查询过滤模型目录
- `ParcelQueryFilter.cs`：Parcel 第一阶段列表过滤参数模型（BagCode、WorkstationName、Status、Chute、扫码时间范围等），并通过特性限制时间跨度默认不超过 3 个月。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/`：通用分页模型目录
- `PageRequest.cs`：通用分页请求参数（含页码/页大小归一化）。
- `PageResult.cs`：通用分页结果模型（Items、页码、页大小、总数）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/`：查询读模型目录
- `ParcelSummaryReadModel.cs`：Parcel 列表摘要读模型（包含 Parcel 全部扁平化字段，用于分页列表）。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/`：仓储结果模型目录
- `RepositoryResult.cs`：非泛型仓储结果模型。
- `RepositoryResultOfT.cs`：泛型仓储结果模型。
- `RepositoryErrorCodes.cs`：仓储层稳定错误码。
- `DangerousBatchActionResult.cs`：危险批量动作治理结果模型。

###### `Zeye.Sorting.Hub.Domain/Repositories/Models/Validation/`：查询校验模型目录
- `MaxTimeRangeAttribute.cs`：时间范围校验特性（限制起止时间跨度，默认不超过 3 个月）。

### `Zeye.Sorting.Hub.Host/`：宿主层（程序入口、后台服务、启动配置）
- `Program.cs`：应用入口与 Host 构建流程。
- `ParcelReadOnlyApiRouteExtensions.cs`：Parcel 只读路由注册与处理逻辑。
- `ParcelListQueryParameters.cs`：只读列表查询参数模型。
- `ParcelAdjacentQueryParameters.cs`：只读邻近查询参数模型。
- `HostingOptions.cs`：`Hosting` 主配置模型及地址/Swagger 拼装逻辑。
- `SwaggerOptions.cs`：Swagger 子配置模型。
- `BrowserAutoOpenOptions.cs`：开发期浏览器自动打开配置模型。
- `LocalDateTimeParsing.cs`：本地时间解析与 API 响应工厂共享工具（`TryParseLocalDateTime`、`TryParseOptionalLocalDateTime`、`IsUtcKind`、`CreateBadRequestProblem`、`CreateParcelMissingProblem`），统一供各路由扩展类复用，避免重复实现（其中“包裹不存在”统一返回 404）。
- `ParcelAdminApiRouteExtensions.cs`：Parcel 管理端 API 路由扩展（`MapParcelAdminApis`），注册 `POST /api/admin/parcels`、`PUT /api/admin/parcels/{id}`、`DELETE /api/admin/parcels/{id}` 普通写接口及 `POST /api/admin/parcels/cleanup-expired` 危险治理接口，并补齐中文 Summary/Description；新增创建接口 `id` 参数校验与重复 Id 冲突映射（409）。
- `Worker.cs`：后台轮询任务示例服务。
- `Zeye.Sorting.Hub.Host.csproj`：Host 项目定义。
- `nlog.config`：NLog 日志配置，双路落盘（`logs/app-*.log` 全量 + `logs/database-*.log` 数据库专属），低开销设计（异步队列 + keepFileOpen + optimizeBufferReuse），保留 30 天。
- `appsettings.json`：默认运行配置（新增 `Hosting` 段用于驱动监听地址、Swagger 路径与 Development 浏览器自动打开；并包含连接字符串、迁移失败策略分环境配置、分表治理守卫、Time/Volume/Hybrid 双策略配置、结构化容量观测入口 Observation、PerDay 预建日期清单、仓储危险动作隔离开关、结构化扩容计划、日志级别与自动调优参数）。
- `appsettings.Development.json`：开发环境配置覆盖文件。

#### `Zeye.Sorting.Hub.Host/Swagger/`：Swagger 扩展目录
- `EnumDescriptionSchemaFilter.cs`：枚举 Schema 中文增强过滤器，保留真实 enum 增强逻辑，并扩展 Contracts 中“枚举数值 int 字段”的全量映射，向 Swagger 输出“数值 = 枚举名（Description 中文）”可选值说明。

#### `Zeye.Sorting.Hub.Host/HostedServices/`：启动/常驻托管服务目录
- `AutoTuningLoggerObservability.cs`：自动调优观测默认日志实现（统一日志 + 指标抽象默认落地）。
- `DatabaseAutoTuningHostedService.cs`：数据库自动调谐托管服务主流程。
- `PendingRollbackAction.cs` / `TableCapacitySnapshot.cs` / `EvidenceContext.cs` / `PolicyDecision.cs`：自动调谐内部模型与决策类型。
- `DatabaseInitializerHostedService.cs`：数据库初始化与迁移托管服务主流程。
- `PrebuiltPerDayShardDatesResolution.cs`：日分表预建日期解析结果模型。
- `ShardingGovernanceGuardException.cs`：分表治理守卫异常类型。
- `DevelopmentBrowserLauncherHostedService.cs`：Development 浏览器启动隔离器，仅在 Development + `Hosting:BrowserAutoOpen:Enabled=true` + 交互式/本机/非容器/非 CI 场景触发；通过 `IHostApplicationLifetime.ApplicationStarted` 确保服务可访问后再尝试打开，并持续使用 `SafeExecutor` 隔离异常，避免影响宿主启动。

#### `Zeye.Sorting.Hub.Host/Properties/`：项目运行调试属性目录
- `launchSettings.json`：本地调试启动配置（Development Profile、环境变量、本地 `applicationUrl`）；`launchBrowser=false`，避免与运行时浏览器隔离器重复打开窗口。

### `Zeye.Sorting.Hub.Infrastructure/`：基础设施层（EF Core 持久化、仓储实现、DI 注册、数据库方言）
- `Zeye.Sorting.Hub.Infrastructure.csproj`：Infrastructure 项目定义。

#### `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/`：依赖注入扩展目录
- `PersistenceServiceCollectionExtensions.cs`：持久化服务注册扩展（数据库提供器选择、连接字符串校验、DbContext 注册、Parcel 主表保持按 `CreatedTime` 分表；分表时间粒度由 Time/Volume/Hybrid 统一策略决策驱动，Parcel 关联值对象规则继续复用声明式清单与覆盖守卫）。

#### `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/`：EF Core 实体映射配置目录
- `BagInfoEntityTypeConfiguration.cs`：BagInfo 映射配置。
- `ParcelEntityTypeConfiguration.cs`：Parcel 聚合映射配置（Parcel 主键 Id 改为 `ValueGeneratedNever`，由应用层显式赋值；owned/value-object 子表影子主键继续保持自动生成）。

#### `Zeye.Sorting.Hub.Infrastructure/Persistence/`：持久化核心目录（DbContext、方言、设计时工厂）
- `SortingHubDbContext.cs`：EF Core DbContext（实体集与模型构建入口）。
- `DbProviderNames.cs`：EF Core 运行时/迁移 providerName 常量（`Pomelo.EntityFrameworkCore.MySql` / `Microsoft.EntityFrameworkCore.SqlServer`），用于 `DbContext.Database.ProviderName` 识别与迁移分支判断。
- `ConfiguredProviderNames.cs`：配置层 provider key 常量（`MySql` / `SqlServer`），用于 `Persistence:Provider`、`ConnectionStrings` key 与设计时 CLI `--provider` 参数值，避免配置语义与 EF providerName 语义混用。
- `ParcelIndexNames.cs`：Parcel 关键索引名称常量（供分表治理审计与测试复用，避免多处硬编码漂移；包含 BagCode/ActualChuteId/TargetChuteId 三条 ScannedTime 复合索引及 MySQL FULLTEXT 索引名）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/`：数据库方言抽象与实现目录
- `DatabaseProviderExceptionHelper.cs`：数据库异常错误码提取与方言共享索引列归一化/索引名构造辅助类。
- `IDatabaseDialect.cs`：数据库方言抽象接口。
- `IShardingPhysicalTableProbe.cs`：分表物理对象探测抽象（最小职责：判断目标物理表是否存在 + 探测目标表缺失索引名集合；仅探测，不执行 DDL）。
- `IBatchShardingPhysicalTableProbe.cs`：分表物理表批量探测抽象（最小职责：一次性返回缺失物理表集合）。
- `MySqlDialect.cs`：MySQL 方言实现（自动调优 SQL + INFORMATION_SCHEMA.TABLES 物理分表探测 + INFORMATION_SCHEMA.STATISTICS 关键索引缺失探测）。
- `SqlServerDialect.cs`：SQL Server 方言实现（自动调优 SQL + sys.tables/sys.schemas 物理分表探测 + sys.indexes 关键索引缺失探测）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/`：自动调谐核心目录
- `IAutoTuningObservability.cs`：自动调优观测输出抽象接口。
- `NullAutoTuningObservability.cs`：观测空实现，未注入观测器时保持兼容。
- `ActionIsolationPolicy.cs`：危险动作隔离策略引擎。
- `AutoRollbackDecisionEngine.cs`：自动回滚判定引擎。
- `AutoTuningClosedLoopTracker.cs`：闭环阶段跟踪器。
- `AutoTuningVerificationResultBuilder.cs`：自动验证标准化结果构造器。
- `IExecutionPlanRegressionProbe.cs` / `IProviderAwareExecutionPlanRegressionProbe.cs`：执行计划探针抽象。
- `ExecutionPlanProbeRequest.cs` / `PlanRegressionSnapshot.cs`：执行计划探针请求与结果模型。
- `LoggingOnlyExecutionPlanRegressionProbe.cs`：默认 logging-only 计划探针实现。
- `AutoTuningConfigurationHelper.cs`：配置读取公共辅助类，集中提供 `GetPositiveIntOrDefault`、`GetNonNegativeIntOrDefault`、`GetNonNegativeDecimalOrDefault`、`GetDecimalInRangeOrDefault`、`GetDecimalClampedOrDefault`、`GetBoolOrDefault`、`GetPositiveSecondsAsTimeSpanOrDefault`、`GetTimeOfDayOrDefault`，并统一 `BuildAutoTuningKey`、`BuildAutonomousKey` 与 `NormalizeToLocalTime`，消除重复键拼装与时间归一化实现。
- `MySqlSessionBootstrapConnectionInterceptor.cs`：MySQL 连接会话初始化拦截器（类型判断逻辑内联，移除无意义 helper）。
- `SlowQueryAutoTuningPipeline.cs`：慢查询采集、TopN 聚合、阈值告警（含基础防抖）与闭环自治结构化建议编排管道（配置键拼装复用 `AutoTuningConfigurationHelper`，并提供主表提取公共方法供 HostedService 与建议编排共用）。
- `SlowQueryCommandInterceptor.cs`：EF Core 慢查询采集拦截器。
- `SlowQuerySample.cs`：慢查询采样记录模型。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/`：分表策略与治理决策目录
- `ParcelShardingStrategyEvaluator.cs`：Parcel 分表策略评估器（分表模式/时间粒度/容量阈值/阈值动作配置解析，结构化校验，容量观测输入统一收敛为 Observation 对象，输出含 finer-granularity 扩展规划的统一决策结果，复用于注册入口与启动审计守卫）。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/`：EF 设计时支持目录
- `MySqlContextFactory.cs`：MySQL 设计时 DbContext 工厂。
- `DesignTimeConsoleLogger.cs`：设计时日志输出器。
- `NoopScope.cs`：设计时日志空作用域。
- `SqlServerContextFactory.cs`：SQL Server 设计时 DbContext 构建器（由统一设计时工厂按 provider 分发调用），连接字符串 key 使用 `ConfiguredProviderNames.SqlServer`，提供 SQL Server 连接字符串搜索与 `DbContextOptions` 组装能力。

##### `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/`：EF Core 迁移文件目录
- `20260316184030_InitialCreate.cs`：初始迁移主体。
- `MigrationSchemaResolver.cs`：迁移共享 schema 解析器。
- `20260316184030_InitialCreate.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260317024345_UseAttributeBasedIndexesAndPrecision.cs`：索引/精度特征标记对齐迁移（空 `Up/Down`，用于同步模型快照）。
- `20260317024345_UseAttributeBasedIndexesAndPrecision.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260317062930_SplitParcelStatusAndExceptionType.cs`：Parcel 状态三态收敛后新增 `ExceptionType` 可空字段迁移。
- `20260317062930_SplitParcelStatusAndExceptionType.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260318024421_OptimizeParcelAggregateQueryIndexes.cs`：Parcel 聚合高频查询索引优化迁移（离散条件 + 时间范围复合索引）。
- `20260318024421_OptimizeParcelAggregateQueryIndexes.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260322050329_OptimizeBagCodeAndActualChuteIdQueryIndexes.cs`：补齐两处索引覆盖缺口迁移：① 将 `BagCode` 单列索引升级为 `(BagCode, ScannedTime)` 复合索引（覆盖 GetByBagCodeAsync 的等值 + 范围路径，并对 ScannedTime 主排序提供索引支撑）；② 新增 `(ActualChuteId, ScannedTime)` 复合索引（覆盖 GetByChuteAsync 的过滤 + ScannedTime 主排序路径，原有 ActualChuteId_DischargeTime 索引保留）。
- `20260322050329_OptimizeBagCodeAndActualChuteIdQueryIndexes.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260322072600_AddBarCodesFullTextIndex.cs`：为 Parcels.BarCodes 列添加 MySQL FULLTEXT 全文索引（`FTX_Parcels_BarCodes`），仅 MySQL Provider 生效；SQL Server 路径为空操作。当前该索引作为物理分表关键索引一致性审计对象之一（仅探测/记录/阻断，不自动执行危险 DDL）。
- `20260322072600_AddBarCodesFullTextIndex.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `20260323045038_UseExternalProvidedParcelId.cs`：Parcel 主表主键生成策略迁移（移除 Parcels.Id 自动生成，改为外部传入）；MySQL 路径执行 Identity 注解变更，SQL Server 路径保持 no-op（因 SQL Server 不支持通过 ALTER COLUMN 直接切换 IDENTITY）。
- `20260323045038_UseExternalProvidedParcelId.Designer.cs`：迁移元数据文件（自动生成，勿手动修改）。
- `SortingHubDbContextModelSnapshot.cs`：当前模型快照，EF Core 用于计算下次迁移的差量（自动生成，勿手动修改）。

#### `Zeye.Sorting.Hub.Infrastructure/Repositories/`：仓储基类与结果模型目录
- `RepositoryBase.cs`：通用仓储基类（增删改查 + 自动持久化实现）；接受 `NLog.ILogger` 构造参数，由派生类传入，确保日志来源类名为实际仓储类而非基类名。
- `MemoryCacheRepositoryBase.cs`：带内存缓存失效逻辑的仓储基类，继承 `RepositoryBase`，同样使用 NLog 日志。
- `ParcelRepository.cs`：Parcel 仓储第一阶段实现（复用 `RepositoryBase`、`IDbContextFactory`，使用静态 `NLog.ILogger`，已移除 MEL `ILogger<ParcelRepository>` 构造依赖；提供基础读写、分页查询、按 Id 邻近查询与过期清理；条码检索按 Provider 分支（MySQL FULLTEXT Boolean、其他 Provider Contains）；过期清理纳入隔离器开关 + dry-run + 审计 + 补偿边界声明）。

### `Zeye.Sorting.Hub.Realtime/`：实时通信子域（当前为占位工程）
- `Zeye.Sorting.Hub.Realtime.csproj`：Realtime 项目定义。

### `Zeye.Sorting.Hub.RuleEngine/`：规则引擎子域（当前为占位工程）
- `Zeye.Sorting.Hub.RuleEngine.csproj`：RuleEngine 项目定义。

### `Zeye.Sorting.Hub.SharedKernel/`：跨模块共享内核
- `Zeye.Sorting.Hub.SharedKernel.csproj`：SharedKernel 项目定义（已将 `Microsoft.Extensions.Logging.Abstractions` 替换为 `NLog`，与全局日志规范一致）。

#### `Zeye.Sorting.Hub.SharedKernel/Utilities/`：共享工具目录
- `SafeExecutor.cs`：安全执行器；使用 NLog 静态 logger（`LogManager.GetCurrentClassLogger()`），移除了 MEL `ILogger<SafeExecutor>` 构造依赖；提供 `Execute`、`ExecuteAsync`（void）、`ExecuteAsync<T>`（带返回值）三个重载，确保任何异常都不会导致宿主崩溃。

### `Zeye.Sorting.Hub.Host.Tests/`：API 与应用层测试层
- `Zeye.Sorting.Hub.Host.Tests.csproj`：xUnit 测试项目定义。
- `AutoTuningProductionControlTests.cs`：覆盖 dry-run、危险动作隔离、告警防抖与恢复、普通/严重回归、unavailable 指标处理、执行计划探针 available/unavailable 双路径、闭环链路与分表覆盖守卫校验、迁移失败策略分环境解析、结构化扩容计划解析、Time/Volume/Hybrid 分表策略评估、PerDay 预建守卫（配置+物理探测）与分表观测口径/自动索引过滤规则回归；含配置键拼装参数化（Theory）覆盖。
- `AlwaysExistsShardingPhysicalTableProbe.cs`：物理表探测测试桩，始终返回存在并记录调用次数。
- `BatchSelectiveMissingShardingPhysicalTableProbe.cs`：批量物理表探测测试桩，支持选择性缺失结果与 schema 透传断言。
- `CaptureNullScope.cs`：Warning 捕获日志桩使用的空作用域单例。
- `CaptureWarningLogger.cs`：Warning 日志捕获测试桩，收集告警消息用于断言版本解析与回退路径。
- `CountingPlanProbe.cs`：执行计划探针测试桩，记录探针调用次数并返回固定快照。
- `DomainEventArgsTests.cs`：领域事件载荷单元测试，验证 `ParcelScannedEventArgs`/`ParcelChuteAssignedEventArgs` 业务字段赋值、值语义相等与不等、本地时间约束。
- `EmptyServiceScope.cs`：最小服务作用域测试桩，提供基础 `ServiceProvider`。
- `EmptyServiceScopeFactory.cs`：最小服务作用域工厂测试桩。
- `FakeParcelRepository.cs`：Parcel 仓储测试替身，提供只读/写入/过期清理三态结果用于 API 回归测试。
- `FixedPlanProbe.cs`：执行计划探针测试桩，固定返回“探针可用且无回归”。
- `LocalTimeTestConstraintHelper.cs`：测试层本地时间语义约束工具类，提供 `CreateLocalTime`/`AssertIsLocalTime`/`AssertNotUtc` 方法，防止测试代码引入 UTC 语义。
- `MissingIndexShardingPhysicalTableProbe.cs`：关键索引缺失探测测试桩，按物理表返回缺失索引。
- `NullScope.cs`：通用测试日志空作用域单例。
- `ObservabilityEntry.cs`：自动调优观测记录模型，承载名称/值/标签快照。
- `ParcelAdminApiTests.cs`：Parcel 管理端写接口测试，覆盖新增成功路径、创建请求 `id<=0` 返回 400、重复 Id 返回 409、UTC 时间拒绝、更新状态成功路径 + 不存在 404 + 非法操作码 400、删除成功路径 + 不存在 404、cleanup-expired blocked/dry-run/execute 三态 + UTC 时间与非法参数拒绝。
- `ParcelReadOnlyApiTests.cs`：Parcel 只读 API 端点测试，覆盖列表查询、详情查询、详情不存在返回 404、`/api/parcels/adjacent` 按 `id` 查询的 400/404/稳定排序回归。
- `SortingHubTestDbContextFactory.cs`：Host.Tests 通用 InMemory `DbContextFactory`，供查询服务测试与仓储测试复用。
- `ParcelQueryServicesTests.cs`：Parcel 应用层查询服务测试（列表/详情/邻近查询映射与最小参数校验）；新增邻近查询锚点不存在异常场景；多重过滤条件联合成功路径（bagCode + workstationName + actualChuteId + status）；ExceptionType 筛选成功路径与非法值校验。
- `ParcelRepositoryTests.cs`：Parcel 仓储第一阶段能力测试，覆盖分页过滤、详情与按 Id 邻近查询、新增/更新/删除、过期清理与批量新增；新增同一扫描时间稳定排序、锚点不存在、重复主键冲突语义回归，并验证危险清理动作的 blocked/dry-run/executed 三态。
- `SelectiveMissingShardingPhysicalTableProbe.cs`：物理表探测测试桩，模拟指定分表缺失场景。
- `HostingOptionsTests.cs`：Hosting 配置单元测试，覆盖监听地址分号拆分去重、`0.0.0.0` 归一化为 `localhost` 的 Swagger 地址拼装、`BrowserAutoOpen:Url` 显式配置优先级与无效监听地址返回 null 的兜底行为。
- `SwaggerDocumentationTests.cs`：Swagger 文档增强回归测试，覆盖管理端更新请求与值对象响应中的枚举型 int 字段，验证均输出“数值 + 枚举名 + 中文描述”。
- `TestDialect.cs`：通用数据库方言测试桩，提供默认 ProviderName 测试分支。
- `TestHostEnvironment.cs`：`IHostEnvironment` 测试桩，注入环境名与最小内容根配置。
- `TestLogger.cs`：通用泛型日志测试桩，收集日志消息供断言。
- `TestMySqlDialect.cs`：MySQL ProviderName 方言测试桩。
- `TestObservability.cs`：自动调优观测测试桩，收集指标与事件输出。
- `TestSqlServerDialect.cs`：SQL Server ProviderName 方言测试桩。


## 本次更新内容

- 收敛测试结构尾项：`AutoTuningProductionControlTests.cs` 与 `ParcelReadOnlyApiTests.cs` 中的测试替身/辅助类型全部拆分到同名独立文件，保持测试行为不变。
- 同步完成测试侧同类问题收口：拆分 `ParcelQueryServicesTests.cs` 与 `ParcelRepositoryTests.cs` 中内嵌 `TestDbContextFactory`，消除“一个文件多个类型”。
- 为新拆分测试替身字段/属性/单例补齐高信息量 XML 注释（含观测集合、日志消息集合、空作用域单例等测试用途说明）。
- 完成 PR2 结构整改：补齐 `DatabaseAutoTuningHostedService` 剩余正则字段 XML 注释。
- 枚举集中到 `Zeye.Sorting.Hub.Domain/Enums`（含 `Sharding` 子目录），并清理 Contracts/Host/Infrastructure 旧枚举定义。
- 生产代码完成“每个类型独立文件”收口：拆分 Host/Domain/Application/Infrastructure 中多类型同文件问题。
- 删除无引用 `AssemblyReference.cs` 空壳文件，避免无效锚点类型残留。
- 将英文命名文档改为中文文件名并更新工作流/README/文档引用路径。

## 更新记录与待完善事项

- 更新记录（CHANGELOG）详见：[更新记录.md](更新记录.md)
- 待完善事项（BACKLOG）详见：[待完善事项.md](待完善事项.md)

### 可继续完善内容

- 后续可考虑为测试替身目录增加更细分命名分组（如 `Fakes/Probes/Logging` 子目录），在保持单类型单文件前提下进一步提升可导航性。
- 后续可补充真实 MySQL / SQL Server 集成用例，分别覆盖 `GetAdjacentByIdAsync` 的同一扫描时间稳定排序与重复主键冲突语义，验证跨 Provider 一致性。
- 后续可在管理端创建接口补充显式的 `CreateConflictProblem` 响应工厂，进一步统一 409 问题详情输出格式。
- 后续可在 SQL Server 场景下补充“主键非自增”历史库平滑切换 runbook（数据校验、灰度、回退步骤）。
- 后续可补充 `20260323045038_UseExternalProvidedParcelId` 在真实 SQL Server 实例上的端到端迁移验证（`dotnet ef database update` + 回滚验证），确保历史库升级链路与注解元数据在实机环境完全一致。
- 后续需为 SQL Server 补充专项迁移（建新表 + 数据回填 + 外键/索引重建）以真正落地 “Parcels.Id 非自增” 结构变更，当前迁移仅保证 SQL Server 路径可安全执行且不失败。
- 后续可补充 Swagger 鉴权策略（如文档访问令牌、角色分级可见性）并统一与 API 鉴权体系联动。
- 后续可细化非开发环境文档暴露治理（例如内网白名单、按环境开关、发布审批审计）。
- 后续可完善反向代理与子路径部署适配（例如 `PathBase`、网关前缀下 Swagger JSON/UI 地址自动拼装），并评估自动打开地址的反向代理本机回环兼容策略。
- 后续可补充 OpenAPI 示例值与示例请求体（含典型成功/失败样例），提升调用方接入效率。
- 后续可为 SQL Server 路径配置 Full-Text Catalog 并改写为 `EF.Functions.Contains()`，彻底消除 SQL Server 侧的 LIKE '%xxx%' 限制。
- 后续可补充真实 MySQL / SQL Server 集成测试，验证在大数据量下“Provider 差异语义（MySQL FULLTEXT Boolean / 其他 Contains）”的性能与执行计划稳定性。
- 后续可在“条码检索语义差异”场景评估按关键字长度/模式的可观测分级策略（开关 + 审计），在召回率与性能之间建立可运营平衡。

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
