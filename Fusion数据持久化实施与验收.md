# Fusion数据持久化实施与验收

## 第二阶段实施计划

1. 以Fusion当前事件合同和既有测试场景为输入，固定来源身份、记录身份、量测单位和空值映射；在隔离关系数据库中回放正常、NoRead、拒绝绑定、失败重试及乱序场景。用户已明确本阶段不依赖历史冻结日志。
2. 在隔离MySQL 8.x实例上执行基础迁移、按天/周/月实际建表、写入、跨表检索、并发和回退守卫测试；运行环境不可用时如实记录阻塞，继续完成与MySQL无关的实现与SQLite回归。
3. 定义有界时间范围的包裹运营统计口径，在服务端聚合检测件量、完成件量、异常、NoRead、格口差异、工作台分布和时效；前端报表只展示真实接口数据、空状态和错误状态。
4. 完成后端关系数据库/API与前端测试，再按实际结果更新本文件与README的逐文件职责。

## 第二阶段实施结果

已增加`GET /api/parcels/analytics?fromDate=yyyy-MM-dd&toDate=yyyy-MM-dd`。接口严格接受本地日期，默认预算最多31个包含端点的自然日；数据库执行跨历史物理分表的条件计数、分组和时效汇总。前端P18分析报表移除写死的每日件量、异常和工作台，以及没有真实数据支撑的站点、产线筛选；加载、空数据和错误分别显示。

报表将两个统计总体明确分开：

| 指标 | 时间与总体 | 定义 |
| --- | --- | --- |
| 入库过件、当前完成、当前异常、NoRead、格口不一致、工作台、异常类型 | `CreatedTime`落入半开本地时间窗口，且`SourceParcelId`与`DetectedTime`均存在的来源包裹 | 同一包裹仅计一次；状态取当前快照，不把历史失败当作当前异常。NoRead为显式`NoReadType`或不区分大小写的`NoRead`条码；格口差异要求目标与实际编码均已知且忽略大小写后不同。 |
| 平均分拣时效 | 上述包裹中当前完成、`LifecycleMilliseconds`有值的样本 | 先按有效样本数加权，再换算秒；无样本返回`null`，前端显示“—”。 |
| 处理事实、失败尝试、未绑定DWS事实 | `OccurredAt`落入同一日期窗口的追加记录 | `IsSuccess=false`为失败尝试；`ParcelId=null`且阶段为DWS接收或绑定的记录为未绑定事实。可对一次过机产生多条事实，不与包裹件数混用分母。 |

当前报表的“入库过件”是首次入库批次件量，并非设备检测时刻的吞吐量。来源事件可能晚到，因此不能用入库分表后缀安全裁剪按`OccurredAt`统计的全部事实；本阶段继续用完整目录合并，并由31天窗口和工作台返回行数预算守卫。上线大数据量前仍需在目标规模上压测跨分表聚合，按真实执行计划补索引或离线汇总。

### Fusion来源合同映射

按`Zeye.SortingFusionService.Contracts/Events`当前事件定义和`Zeye.SortingFusionService.Core/Models/DwsMeasurement.cs`核对，使用Hub现有处理事实合同做隔离回放。以下身份字段尚不由这些事件直接提供，未来传输接入时必须由来源实例/可靠会话和稳定消息身份补齐，不能用条码或当前时间伪造。

| Fusion来源字段 | Hub处理事实字段 | 约束 |
| --- | --- | --- |
| 检测事件`ParcelId`、`DetectedAt`、`HasReliableTimestamp` | `SourceParcelId`、`OccurredAt`、`HasReliableTimestamp` | `SourceInstanceId`与`SourceRunId`另外提供；设备计数重置时更新会话，同一事件重试保持`RecordId`不变。 |
| DWS事件`ParcelCode`、`Weight`、`Length/Width/Height` | `Barcode`、`WeightGrams`、`LengthMm/WidthMm/HeightMm` | 重量克、尺寸毫米；直接取原始可空字段。Fusion后续`HandleDwsMessageUseCase`会将缺失量测转成0，不能从该有损快照反推原始值。 |
| DWS事件`Volume` | `VolumetricWeightGrams` | Fusion明确将其定义为体积重量（克）；没有物理体积时`VolumeMm3`保持`null`。 |
| DWS事件`RawPayload`、`ReceivedAt`、`MeasuredAt`、`HasReliableFrameBoundary`、`TriggerBatch`、`ScanSequence`、`CorrelationId` | 同名原文及可靠性字段 | 触发批次和扫描序号保留前导零；拒绝绑定时`SourceParcelId`与`ParcelId`为空，候选身份另存。 |
| 落格事件`ParcelId`、`ActualChuteCode`、`CompletedAt`；异常事件`ExceptionType`、`Message`、`ReceivedAt` | `SortingCompleted`的来源号、实际格口与发生时间；`ParcelException`的原始异常代码、说明和时间 | 上传成功不等于实际落格，失败及重试保留独立事实。 |

`SorterParcelDetectedEventArgs`及其他事件目前没有`SourceRunId`和`RecordId`。因此“可重试、跨进程仍同身份”的生产接入还取决于来源端稳定标识设计；本阶段没有冻结历史日志，也未验证生产Fusion消息链路。隔离样本覆盖重复条码、连续NoRead、显式关联依据、未绑定DWS、失败重试和迟到事实的持久化结果；Fusion自身的关联判定算法不属于这组Hub测试。

### 来源身份合同

- `SourceInstanceId` 是物理计数器/来源实例的稳定配置标识。进程重启、重连及重放时不变，不使用进程ID、条码或当前时间代替。
- `SourceRunId` 是来源包裹计数器的一次连续运行批次。来源端在批次开始时生成并持久化；普通重启、重连、重试保持不变；计数器重置或重新播种时，在产生新包裹事实前切换为新值。不能仅根据收到一个较小编号推断重置，因为事实可能乱序或迟到。
- `SourceInstanceId + SourceRunId + SourceParcelId` 唯一标识一次物理包裹检测。相同`RecordId`及内容的重试返回重复结果；同一三元组的第二条检测使用不同`RecordId`会返回冲突；新批次中复用相同来源号是另一包裹。条码和事件时间不参与身份计算。身份字段禁止首尾空白和控制字符。
- 手工检测登记页使用`SHA-256(UTF-8(JSON.stringify(["detected-v1", SourceInstanceId, SourceRunId, SourceParcelId])))`的十六进制值，并以`detected-v1-`为前缀生成`RecordId`。同一三元组在页面重载后仍得到相同值。其他可能多次发生的阶段不能按三元组生成同一个`RecordId`；来源端须在首次投递前持久化每次事实的`RecordId`和内容，重试复用，新的尝试使用新`RecordId`和递增的`AttemptNumber`。

现有Fusion事件仍缺少上述运行批次和事实身份字段，来源端的可靠发件箱尚未落地；Hub不会从条码或时间猜测这些身份。

### 隔离验证

- MySQL 8.0.46免安装实例只绑定本机`127.0.0.1:34068`，使用独立测试库；完整迁移执行8条历史记录、创建26张基础表。实际写入日表`20260928`/`20260929`、周表`2026W41`和月表`202611`，跨表报表得到3件入库、1件完成、1件当前异常、1件NoRead、1件格口不一致、10秒平均时效、1次失败尝试和1条未绑定DWS事实。
- 相同`RecordId`跨月重试返回重复，内容变化返回冲突；缺失宽度与物理体积保持`null`，1234.567克入根快照为1.234567千克，2200克体积重量独立保存。
- 6个独立进程并发写同一来源记录，启用与生产默认值相同的5次MySQL重试（隔离测试最大延迟1秒，生产默认10秒）后，1次首次写入、5次幂等重复；定位、凭据、处理事实及包裹物理表均仅有1行。未开启重试的诊断运行出现InnoDB死锁，说明该配置是跨进程正确性的必要条件。
- 在有数据的库执行回退脚本，MySQL检查约束报错3819，凭据、目录和迁移记录仍保留；在空库完整执行正向8次迁移、最新迁移回退至7次、再升级至8次成功。生产数据库未执行迁移或清理。
- Host构建0错误；针对报表与HTTP的9项测试通过；修正报表参数错误分类及数据库基础门禁问题后完整后端回归349项通过、0失败、0跳过；前端构建及5项客户端测试通过。结果保存于`.codex-artifacts/test-results/analytics-targeted.trx`与`analytics-gates-final.trx`。SQLite测试使用真实物理表及事务，不以InMemory替代。
- 浏览器连接隔离HTTP后端核对实际报表：2件入库、1件当前异常、1件NoRead、1件格口不一致、7条处理事实及1条未绑定DWS与接口一致；无数据日期显示空状态与未知时效，后端不可用时显示502错误。640像素视口无页面级水平溢出；截图保存在`.codex-artifacts/analytics-browser-qa-crop.png`。
- 编译门禁原先仍要求已删除的`ParcelAggregateShardingRule.cs`。经用户明确授权后，改为核查现行`ParcelPartitionStore`的建表、后缀校验及DDL协调，以及持久化注册覆盖校验；Host默认构建现已通过。
- 数据库基础规则脚本还发现设计时MySQL工厂含明文默认凭据、初始化预建循环缺少显式取消检查。已改为无凭据且指向专用占位库的设计时连接，循环每次建表前检查取消；脚本现通过，MySQL完整幂等迁移脚本重新生成于`.codex-artifacts/mysql-final.sql`。

## 第三阶段：跨分表报表性能与索引治理

已增加`performance/ParcelAnalyticsBenchmark`可参数化基准，限定本机非默认端口和`zeye_bench_`隔离库。工具调用真实处理事实仓储与报表服务，预建分表、写入确定性样本，检查1/7/31天包裹总体和事实总体，并捕获实际参数化SQL执行`EXPLAIN ANALYZE`。目标日均/峰值件量与保留时长尚未提供，以下为隔离样本，不能宣称生产目标规模达标。

首次样本为31天、每日20件、6个周分表，共620件和714条事实；首次写入714条，无失败，稳态写入P95为103.05毫秒。优化前1天报表的包裹查询合并全部6个物理周期，事实统计6张事实表均为表扫描。包裹查询现依据不可变首次入库周期目录，只合并与查询日期重叠的物理表并保留历史基础表；1天样本物理周期从6个降为1个。事实按`OccurredAt`统计，迟到事实可能位于任何历史分表，故不按事件日错误裁剪分表。

新迁移`20260928110851_AddParcelProcessingOccurredAtIndex`为基础事实表新增`OccurredAt`索引，新物理周期建表也自动包含该索引。已有MySQL事实分表使用`performance/scripts/backfill-processing-occurred-index.ps1`默认预览计划与回滚清单，再经显式双开关执行；隔离库6张旧分表补建后复核缺失数为0。补建前1天事实查询6张表均为表扫描，补建后5张使用索引范围扫描，余下1张小表仍由MySQL选择表扫描；31天覆盖全部样本时仍选择全表扫描，符合当前样本的数据分布。

第二组隔离样本为31天、每日200件、6个周分表，共6200件、6852条事实，并发8写入无失败，Debug首次运行的稳态写入约282.86条事实/秒，单次写入P50/P95/P99为16.04/35.91/194.87毫秒。首次15次报表查询中，1天P50/P95为5.83/44.73毫秒，7天为21.57/46.05毫秒，31天为83.77/99.69毫秒；同一数据Debug只读复测的P95分别为17.65/27.90/81.20毫秒，Release只读复测分别为15.42/25.45/76.53毫秒，说明少量迭代会受到本机状态影响。写入计时不包含约12.53秒的分表预建，报表核对含最后一天发生但归档于首周的迟到事实。样本运行时为避免本机CoreCLR Server GC堆初始化失败设置了`DOTNET_gcServer=0`；该结果只作为本机样本基线，不能用于推断生产吞吐或GC行为。原始结果及执行计划在`.codex-artifacts/benchmark-results/`，不会进入版本控制。

生产应用此变更前，需在目标环境先审核迁移与旧物理表索引补建计划，按维护窗口执行；给出真实日均/峰值量、保留时长和延迟预算后再做相应规模的回归。跨全部历史分表的事实统计仍与物理分表数量相关，目标规模下若预算不达标，再按发生日设计增量汇总。SQL Server的独立迁移与隔离验收见第四阶段。

## 第四阶段：SQL Server独立迁移与历史索引补建

旧共享迁移链由MySQL模型生成，完整SQL Server脚本的初始建表含`datetime(6)`和`tinyint(1)`等不受SQL Server支持的列类型，且其共享快照报告260项Provider映射差异。现保留MySQL迁移链，在`Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations`建立SQL Server专用初始迁移和快照，并由运行时、设计时按Provider选择迁移程序集。SQL Server `has-pending-model-changes`现为无差异，新脚本使用`datetime2`、`bit`、`nvarchar`等对应类型。初始迁移仅允许空库；已有表主动拒绝执行，已有旧迁移历史的数据库需要单独制定保留数据的过渡方案。

本机独立LocalDB实例的隔离库已完成基础迁移，核对26张基础表、`OccurredAt`索引与EF历史记录；存在物理分表时回退被51001守卫拒绝，存在业务数据时被51002守卫拒绝。清除仅用于验证的空物理表和目录行后，空库回退至0仅剩EF历史表，再次迁移成功。另两个隔离探针库分别预置`dbo`和非`dbo`旧表各1条数据后，基线升级均被51003守卫拒绝，数据与旧表保持不变。`performance/scripts/backfill-processing-occurred-index.ps1`现同时支持MySQL和SQL Server：在隔离SQL Server测试表上预览1项、显式执行1项、复查缺失0项，并执行生成的回滚SQL；MySQL既有样本复查缺失0项。此后使用真实`ParcelPartitionStore`在隔离库预建日分表，核对物理事实表、`OccurredAt`索引和分表目录记录各1项，回填预演缺失0项。正反向脚本、计划和审计产物在`.codex-artifacts/sqlserver-validation/`，不会提交。CI现分别检查两种Provider的模型漂移，并对SQL Server执行迁移、空库回退和重新迁移。

以上只验证全新SQL Server库、空物理表索引补建与新物理周期预建；尚未证明旧SQL Server业务库可直接升级，也未替代真实峰值、保留时长与报表延迟预算下的性能验收。

## 首批落地内容（历史验收）

本次完成Hub侧的存储、检索和前端展示闭环，接入对象为`D:\WorkSpace\Zeye\Zeye.SortingFusionService`产生的包裹处理数据，暂不实现Fusion通信客户端。

| 顺序 | 已落地能力 | 解决的问题 |
| --- | --- | --- |
| 1 | 来源实例、设备编号会话、来源包裹号组成过机身份；检测即可建档 | 同条码重复过机、NoRead、设备计数重置不会被合并；未知量测和格口保留空值 |
| 2 | 当前快照与追加式阶段事实 | 完整保存DWS原始值及绑定依据、扫描上传、格口分配、分拣指令、实际落格、异常、落格上报、图片登记及上传；失败与重试独立保留 |
| 3 | 去重凭据、全局身份定位及事务写入 | 重复记录无副作用，同一记录身份内容不同返回冲突；快照、事实、定位与凭据一同提交或回滚 |
| 4 | 可配置物理分表与跨表查询 | 支持天、周、月；包裹和附属表共用首次入库周期，晚到事实补写原表，配置切换后历史仍可检索 |
| 5 | 真实包裹页面 | 列表展开完整摘要；详情显示全部已有值对象、阶段轨迹与原始记录；新建检测、批量入队、过期清理接入真实接口 |

## 分表配置

```json
{
  "Persistence": {
    "Sharding": {
      "Strategy": { "Time": { "Granularity": "PerMonth" } },
      "CreateShardingTableOnStarting": false,
      "WriteRouting": { "AllowTableCreation": false, "DryRun": true },
      "Governance": { "PrebuildWindowHours": 72 }
    }
  }
}
```

- `Granularity`仅允许`PerDay`、`PerWeek`、`PerMonth`，未配置使用`PerMonth`。
- 日分表后缀为`yyyyMMdd`，周分表后缀为ISO周所属年份和周编号`yyyyWww`，月分表后缀为`yyyyMM`。周从本地时间周一零点开始，跨年按ISO周所属年份归档。
- 分表依据为不可变首次入库时间，不使用后续测量或落格时间重新路由。配置变更影响新包裹，历史定位仍指向原表。包裹附属表统一采用同周期路由，共享集包表`Bags`保持全局。
- 既有容量策略若命中`SwitchToPerDay`，会采用按天策略；需固定粒度时应检查该容量策略配置。容量与时间策略使用同一个决策入口。
- `AllowTableCreation`范围`true/false`，默认`false`；`DryRun`范围`true/false`，默认`true`。周期已预建时正常写入，缺少周期表时默认阻断并审计DDL。
- `CreateShardingTableOnStarting`范围`true/false`，默认`false`。受控测试首次启动可显式设为`true`，同时设置`AllowTableCreation=true`、`DryRun=false`；启动将预建当前周期、下一个周期及窗口覆盖的周期。`PrebuildWindowHours`为正整数，默认72小时。写入期间缺表也复用同一DDL隔离器。
- 上线先执行基础表迁移，再预建新周期；已有目录及物理表不因切换配置而搬迁。DDL由NLog审计，MySQL和SQL Server使用数据库会话锁协调建表，部分建表失败可按相同结构恢复。

## 写入合同

`POST /api/admin/parcels/processing-records`保存来源事实，与Fusion的设备通信方式无关。

`SourceInstanceId + SourceRunId + SourceParcelId`代表一次过机；`SourceRunId`是设备包裹计数有效会话，不应仅因Hub或Fusion重启就更换。条码和NoRead不参与身份。

`RecordId`代表一次处理结果，重试保持一致；新的尝试使用新的记录标识及递增的`AttemptNumber`。首次保存返回201，重复返回200，同一身份内容冲突返回409，合同非法返回400。未绑定DWS允许没有来源包裹号，并独立保存候选包裹、关联标识、绑定方式、时间差、拒绝原因和原始报文。

Stage取值：0检测、1DWS接收、2DWS绑定、3扫描上传、4格口分配、5分拣指令、6实际落格、7设备异常、8落格上报、9图片登记、10图片上传。接口上传成功与实际落格完成分别保存；不会用上传成功推断包裹已完成。

重量使用克，尺寸使用毫米，物理体积使用立方毫米，体积重量使用克。来源未提供值时保留`null`，不使用零代替。根快照重量仍使用千克，精度扩展为`decimal(21,6)`，保留来源克值的三位小数以及原有整数范围。来源时间仅使用本地时间，拒绝带时区偏移的输入。图片保存路径、相机及完整性信息，文件本体存储不在本次范围。

`GET /api/parcels/{id}`返回全部已有值对象及处理事实；`GET /api/parcels/processing-records/unbound?limit=50`检索未关联事实，limit范围1至200。

## 前端展示与操作

- 包裹台账使用真实筛选和分页，支持展开全部摘要字段，未关联DWS单独检索；宽表可横向滚动，完整原文可在展开行查看。
- 包裹详情包含当前结果、失败与重试时间线、全部处理记录及原始正文，以及条码、称重、体积、格口、外部接口、指令、图片、视频、小车、集包、设备、灰度、叠包、坐标14组既有明细。未知值显示“未提供”。
- 新建包裹仅提交检测事实，来源编号使用字符串保留64位精度；尚未测量的重量、尺寸、格口不要求填写。
- 批量入队使用完整`ParcelCreateRequest`合同，JSON和简单CSV支持64位编号；缺少必填数据时明确拒绝。简单CSV不支持带引号或内嵌逗号的字段，此类数据使用JSON。返回入队计数，不能据此宣称已落库。
- 清理提交到后端隔离器，显示真实计划数、执行数和补偿边界。默认阻断，清理主表及附属数据时保留来源定位和处理事实用于追溯。
- 404、代理错误、网络中断均显示真实错误，不回退演示数据。其他模块仍使用演示数据，不属于本次接入范围。
- 开发代理默认连接Host的5078端口，`ZEYE_API_PROXY`可覆盖；浏览器验收使用4193前端和5098隔离后端。生产部署使用同源反向代理或`VITE_API_BASE_URL`。

## 数据库迁移与回退

新增迁移`20260927203617_PersistFusionProcessing`补齐基础模型、可空量测及结果字段、来源字段、处理事实、周期目录、包裹定位和去重凭据。EF已确认模型与迁移一致。

首批曾生成MySQL与共享迁移链的SQL Server正反向SQL，位于`.codex-artifacts/fusion-mysql-up.sql`、`fusion-mysql-down.sql`、`fusion-sqlserver-up.sql`、`fusion-sqlserver-down.sql`。旧SQL Server脚本不用于发布；现行独立基线脚本见`.codex-artifacts/sqlserver-validation/new-up.sql`和`new-down.sql`。未执行生产迁移。

回退前检查是否存在新事实、定位、目录，或者无法无损回退的空值及重量精度；存在时拒绝回退。SQL Server使用THROW终止批次；MySQL使用CHECK守卫，要求MySQL 8.0.16或更高版本，并且执行器必须遇到首个错误即停止，禁止强制继续。MySQL DDL不提供整批事务回退保障，执行前必须备份并审查脚本。物理周期表仅可在确认无业务数据后人工受控清理，禁止将空值补零后强行回退。

## 验收清单

- [x] 仅检测即可入库，未知事实不会伪造。
- [x] 同条码多次过机、NoRead及设备计数重置保持身份独立。
- [x] DWS原始值、绑定依据和拒绝结果可检索。
- [x] 扫描、格口、指令、落格、异常、图片及失败重试完整保存。
- [x] 去重、内容冲突和事务回滚验证通过。
- [x] 天、周、月实际物理写入、跨年周和晚到补写验证通过。
- [x] 历史粒度变更后仍可跨表分页和按ID查询。
- [x] 附属表与主表共用周期，完整聚合再次更新不会重复插入已有明细。
- [x] 前端真实列表、完整详情、未关联DWS、新建、批量、清理及错误状态验证通过。
- [x] 前后端构建、345项后端回归和5项客户端测试通过。
- [x] MySQL迁移链与SQL Server专用空库基线均可生成正反向脚本；两种Provider的EF模型与各自快照一致。SQL Server运行时隔离验收见第四阶段。

## 验证证据与范围

最终后端回归345项全部通过、0失败、0跳过，其中本次Fusion关系持久化及HTTP测试24项。结果为`.codex-artifacts/test-results/fusion-final.trx`，覆盖真实SQLite物理DDL、事务、跨表查询与真实HTTP路由；未用InMemory替代这些集成验证。

Host最终编译0错误；前端TypeScript检查和Vite生产构建通过。客户端5项测试覆盖64位编号及时间戳精度、未知量测与零值、事实原文、404/409、代理502与网络错误。现有分析器警告仍存在。

浏览器使用隔离后端实际验证：11种阶段记录、失败扫描上传、DWS原文及长关联编号、未知量测、完整详情、新建检测、批量入队后按64位编号查到落库详情、未关联DWS、404，以及清理默认返回`blocked`、计划2条、实际0条。列表在1280和1440宽度检查，本地截图已确认筛选布局无重叠。

外部UI分析被自动审批拒绝，因为可能上传本地页面内容；采用本地参考图与截图、浏览器交互完成验收，未使用外部分析结果。12ui本地截图已保存，最终CLI收尾因凭据文件访问限制返回失败，不将其视为整套设计工具验收成功，也不宣称页面像素完全一致。

首批验收时Docker引擎未运行，数据库运行时证据限于SQLite。后续第二阶段补充了隔离MySQL 8.0实例的迁移、物理分表、跨表查询、并发和回退验证；第四阶段补充了独立LocalDB的SQL Server空库迁移、回退守卫及旧物理表索引补建验证。上线前仍需在目标部署环境执行迁移和目标规模性能验收。

可复现检查：

```powershell
dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj
npm --prefix Zeye.Sorting.Hub.Web run build
npm --prefix Zeye.Sorting.Hub.Web test
```

## 后续可完善点

Fusion通信接入及稳定来源消息身份、消息投递恢复、图片文件对象存储、权限收敛，以及依据真实峰值/保留期的跨分表报表性能验收需在后续任务中落地。真实包裹运营报表已在第二阶段接入，本机隔离样本及可参数化基准已在第三阶段补齐；未修改Fusion项目、生产连接或生产数据库，未提交或推送现有工作区改动。
