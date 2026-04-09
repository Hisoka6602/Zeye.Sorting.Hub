# PR-B 检查台账：`Zeye.Sorting.Hub.Domain/`

> **批次说明**：本台账对应分批审查方案中的 PR-B，覆盖 `Zeye.Sorting.Hub.Domain/` 目录下的全部受版本控制文件（共 67 个）。  
> **基线版本**：`18d5370`（2026-04-09）  
> **检查时间**：2026-04-09  
> **检查人**：Copilot

---

## 一、本批次覆盖文件列表（与基线映射）

| 序号 | 文件路径 | 基线是否存在 |
|------|----------|-------------|
| 1 | `Zeye.Sorting.Hub.Domain/Abstractions/IEntity.cs` | ✅ |
| 2 | `Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/WebRequests/WebRequestAuditLog.cs` | ✅ |
| 3 | `Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/WebRequests/WebRequestAuditLogDetail.cs` | ✅ |
| 4 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/Parcel.cs` | ✅ |
| 5 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ApiRequestInfo.cs` | ✅ |
| 6 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/BagInfo.cs` | ✅ |
| 7 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/BarCodeInfo.cs` | ✅ |
| 8 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ChuteInfo.cs` | ✅ |
| 9 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/CommandInfo.cs` | ✅ |
| 10 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/GrayDetectorInfo.cs` | ✅ |
| 11 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ImageInfo.cs` | ✅ |
| 12 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ParcelDeviceInfo.cs` | ✅ |
| 13 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ParcelPositionInfo.cs` | ✅ |
| 14 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/SorterCarrierInfo.cs` | ✅ |
| 15 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/StickingParcelInfo.cs` | ✅ |
| 16 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/VideoInfo.cs` | ✅ |
| 17 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/VolumeInfo.cs` | ✅ |
| 18 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/WeightInfo.cs` | ✅ |
| 19 | `Zeye.Sorting.Hub.Domain/Enums/ActionIsolationDecision.cs` | ✅ |
| 20 | `Zeye.Sorting.Hub.Domain/Enums/ActionType.cs` | ✅ |
| 21 | `Zeye.Sorting.Hub.Domain/Enums/ApiRequestStatus.cs` | ✅ |
| 22 | `Zeye.Sorting.Hub.Domain/Enums/ApiRequestType.cs` | ✅ |
| 23 | `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/AuditResourceType.cs` | ✅ |
| 24 | `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/FileOperationType.cs` | ✅ |
| 25 | `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/WebRequestPayloadType.cs` | ✅ |
| 26 | `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/WebResponsePayloadType.cs` | ✅ |
| 27 | `Zeye.Sorting.Hub.Domain/Enums/AutoTuningClosedLoopStage.cs` | ✅ |
| 28 | `Zeye.Sorting.Hub.Domain/Enums/AutoTuningUnavailableReason.cs` | ✅ |
| 29 | `Zeye.Sorting.Hub.Domain/Enums/AutoTuningUnavailableReasonExtensions.cs` | ✅ |
| 30 | `Zeye.Sorting.Hub.Domain/Enums/BarCodeType.cs` | ✅ |
| 31 | `Zeye.Sorting.Hub.Domain/Enums/CommandDirection.cs` | ✅ |
| 32 | `Zeye.Sorting.Hub.Domain/Enums/ImageCaptureType.cs` | ✅ |
| 33 | `Zeye.Sorting.Hub.Domain/Enums/ImageType.cs` | ✅ |
| 34 | `Zeye.Sorting.Hub.Domain/Enums/MigrationFailureMode.cs` | ✅ |
| 35 | `Zeye.Sorting.Hub.Domain/Enums/NoReadType.cs` | ✅ |
| 36 | `Zeye.Sorting.Hub.Domain/Enums/ParcelExceptionType.cs` | ✅ |
| 37 | `Zeye.Sorting.Hub.Domain/Enums/ParcelStatus.cs` | ✅ |
| 38 | `Zeye.Sorting.Hub.Domain/Enums/ParcelType.cs` | ✅ |
| 39 | `Zeye.Sorting.Hub.Domain/Enums/ParcelUpdateOperation.cs` | ✅ |
| 40 | `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelAggregateShardingRuleKind.cs` | ✅ |
| 41 | `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelFinerGranularityMode.cs` | ✅ |
| 42 | `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelFinerGranularityPlanLifecycle.cs` | ✅ |
| 43 | `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelShardingStrategyMode.cs` | ✅ |
| 44 | `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelTimeShardingGranularity.cs` | ✅ |
| 45 | `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelVolumeThresholdAction.cs` | ✅ |
| 46 | `Zeye.Sorting.Hub.Domain/Enums/VideoNodeType.cs` | ✅ |
| 47 | `Zeye.Sorting.Hub.Domain/Enums/VolumeSourceType.cs` | ✅ |
| 48 | `Zeye.Sorting.Hub.Domain/Events/Parcels/ParcelChuteAssignedEventArgs.cs` | ✅ |
| 49 | `Zeye.Sorting.Hub.Domain/Events/Parcels/ParcelScannedEventArgs.cs` | ✅ |
| 50 | `Zeye.Sorting.Hub.Domain/Options/LogCleanup/LogCleanupSettings.cs` | ✅ |
| 51 | `Zeye.Sorting.Hub.Domain/Primitives/AuditableEntity.cs` | ✅ |
| 52 | `Zeye.Sorting.Hub.Domain/Repositories/IParcelRepository.cs` | ✅ |
| 53 | `Zeye.Sorting.Hub.Domain/Repositories/IWebRequestAuditLogQueryRepository.cs` | ✅ |
| 54 | `Zeye.Sorting.Hub.Domain/Repositories/IWebRequestAuditLogRepository.cs` | ✅ |
| 55 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/ParcelQueryFilter.cs` | ✅ |
| 56 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/WebRequestAuditLogQueryFilter.cs` | ✅ |
| 57 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/PageRequest.cs` | ✅ |
| 58 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/PageResult.cs` | ✅ |
| 59 | `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/ParcelSummaryReadModel.cs` | ✅ |
| 60 | `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/WebRequestAuditLogDetailReadModel.cs` | ✅ |
| 61 | `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/WebRequestAuditLogSummaryReadModel.cs` | ✅ |
| 62 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/DangerousBatchActionResult.cs` | ✅ |
| 63 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryErrorCodes.cs` | ✅ |
| 64 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryResult.cs` | ✅ |
| 65 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryResultOfT.cs` | ✅ |
| 66 | `Zeye.Sorting.Hub.Domain/Repositories/Models/Validation/MaxTimeRangeAttribute.cs` | ✅ |
| 67 | `Zeye.Sorting.Hub.Domain/Zeye.Sorting.Hub.Domain.csproj` | ✅ |

---

## 二、逐文件检查台账（本批次增量）

| 文件路径 | 检查状态 | 问题数(P0/P1/P2) | 主要问题标签 | 证据位置 | 建议修复PR | 检查时间/版本 |
|----------|----------|-----------------|-------------|---------|-----------|-------------|
| `Zeye.Sorting.Hub.Domain/Abstractions/IEntity.cs` | 已检查 | 0/1/0 | 接口 Id 强制 public set，破坏实体不变性 | L7 | PR-FIX-B2 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/WebRequests/WebRequestAuditLog.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/WebRequests/WebRequestAuditLogDetail.cs` | 已检查 | 0/0/1 | 冗余默认值赋值（bool/int/long 零值多次显式赋值） | L118-L183 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/Parcel.cs` | 已检查 | 0/1/2 | `Create` 方法 23 个参数过多；冗余 using；EF `[Precision]` 残留 | L259-L283；L1-5；L83 等 | PR-FIX-B3，PR-FIX-B2，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ApiRequestInfo.cs` | 已检查 | 0/1/1 | 值对象未用 `readonly record struct`；冗余 using | L16；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/BagInfo.cs` | 已检查 | 0/2/1 | 值对象未用 struct；`[Index]` EF Core 属性侵入 Domain | L12；L14-15 | PR-FIX-B1，PR-FIX-B1 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/BarCodeInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L14；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ChuteInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L12；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/CommandInfo.cs` | 已检查 | 0/2/1 | 值对象未用 struct；`System.Net.Sockets.ProtocolType` 基础设施枚举侵入 Domain | L17；L4，L21 | PR-FIX-B1，PR-FIX-B1 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/GrayDetectorInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L13；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ImageInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L14；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ParcelDeviceInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L13；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ParcelPositionInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L12；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/SorterCarrierInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L13；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/StickingParcelInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L13；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/VideoInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L14；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/VolumeInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L15；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/WeightInfo.cs` | 已检查 | 0/1/1 | 值对象未用 struct；冗余 using | L14；L1-5 | PR-FIX-B1，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ActionIsolationDecision.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ActionType.cs` | 已检查 | 0/0/2 | 冗余 using；残留注释死代码（注释掉的枚举成员） | L1-5；L27-28，L47 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ApiRequestStatus.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ApiRequestType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/AuditResourceType.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/FileOperationType.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/WebRequestPayloadType.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AuditLogs/WebResponsePayloadType.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AutoTuningClosedLoopStage.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AutoTuningUnavailableReason.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/AutoTuningUnavailableReasonExtensions.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/BarCodeType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/CommandDirection.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ImageCaptureType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ImageType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/MigrationFailureMode.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/NoReadType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ParcelExceptionType.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ParcelStatus.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ParcelType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/ParcelUpdateOperation.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelAggregateShardingRuleKind.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelFinerGranularityMode.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelFinerGranularityPlanLifecycle.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelShardingStrategyMode.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelTimeShardingGranularity.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/Sharding/ParcelVolumeThresholdAction.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/VideoNodeType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Enums/VolumeSourceType.cs` | 已检查 | 0/0/1 | 冗余 using | L1-5 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Events/Parcels/ParcelChuteAssignedEventArgs.cs` | 已检查 | 0/1/0 | 事件载荷声明为 `internal`，阻断 Application/Infrastructure 层消费 | L7 | PR-FIX-B2 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Events/Parcels/ParcelScannedEventArgs.cs` | 已检查 | 0/1/0 | 事件载荷声明为 `internal`，阻断 Application/Infrastructure 层消费 | L7 | PR-FIX-B2 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Options/LogCleanup/LogCleanupSettings.cs` | 已检查 | 0/0/1 | 日志清理配置不属于 Domain 层职责（应移至 Host/Options 或 Infrastructure） | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Primitives/AuditableEntity.cs` | 已检查 | 0/2/1 | `ModifyTime`/`ModifyIp` 有 public setter（可伪造审计记录）；冗余 using | L30，L35；L1-5 | PR-FIX-B2，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/IParcelRepository.cs` | 已检查 | 0/1/0 | CQRS 混用：命令与查询方法共存于同一仓储接口 | L28-L94 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/IWebRequestAuditLogQueryRepository.cs` | 已检查 | 0/1/0 | 查询仓储接口不应置于 Domain 层（违反分层规则） | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/IWebRequestAuditLogRepository.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/ParcelQueryFilter.cs` | 已检查 | 0/1/1 | 查询过滤器 DTO 不应置于 Domain 层；类体缩进不一致 | 全文；L6-10 | PR-FIX-B3，PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Filters/WebRequestAuditLogQueryFilter.cs` | 已检查 | 0/1/0 | 查询过滤器 DTO 不应置于 Domain 层 | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/PageRequest.cs` | 已检查 | 0/0/1 | 分页参数置于 Domain 层（查询基础设施抽象，随 P1-07 修复迁出） | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Paging/PageResult.cs` | 已检查 | 0/0/1 | 分页结果置于 Domain 层（随 P1-07 修复迁出） | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/ParcelSummaryReadModel.cs` | 已检查 | 0/1/0 | 查询读模型 DTO 不应置于 Domain 层（违反分层规则） | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/WebRequestAuditLogDetailReadModel.cs` | 已检查 | 0/1/0 | 查询读模型 DTO 不应置于 Domain 层 | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/ReadModels/WebRequestAuditLogSummaryReadModel.cs` | 已检查 | 0/1/0 | 查询读模型 DTO 不应置于 Domain 层 | 全文 | PR-FIX-B3 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/DangerousBatchActionResult.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryErrorCodes.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryResult.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Results/RepositoryResultOfT.cs` | 已检查 | 0/0/0 | 无 | — | — | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Repositories/Models/Validation/MaxTimeRangeAttribute.cs` | 已检查 | 0/0/1 | 类体缩进不一致（属性与特性未相对 namespace 缩进） | L6-8 | PR-FIX-B4 | 2026-04-09 / `18d5370` |
| `Zeye.Sorting.Hub.Domain/Zeye.Sorting.Hub.Domain.csproj` | 已检查 | 0/1/1 | `Polly` 依赖侵入 Domain 层（属于基础设施关注点）；含空占位目录声明 | L17；L11-13 | PR-FIX-B2，PR-FIX-B4 | 2026-04-09 / `18d5370` |

---

## 三、问题清单

### P0 问题（无）

> 本批次未发现 P0 级别问题。

---

### P1 问题（10 类，约 30 个文件）

#### [P1-B-001] 14 个值对象使用 `sealed record class` 而非 `readonly record struct`

- **影响文件**：`ApiRequestInfo.cs`、`BagInfo.cs`、`BarCodeInfo.cs`、`ChuteInfo.cs`、`CommandInfo.cs`、`GrayDetectorInfo.cs`、`ImageInfo.cs`、`ParcelDeviceInfo.cs`、`ParcelPositionInfo.cs`、`SorterCarrierInfo.cs`、`StickingParcelInfo.cs`、`VideoInfo.cs`、`VolumeInfo.cs`、`WeightInfo.cs`
- **行号区间**：各文件值对象声明行（以 `ApiRequestInfo.cs L16` 为例：`public sealed record class ApiRequestInfo`）
- **证据描述**：`.github/copilot-instructions.md` 明确规定"事件载荷需要使用 `readonly record struct`（确保不可变、值语义与更优内存性能）"，此规则同样适用于领域值对象。`sealed record class` 是引用类型，每次传递都在堆上分配对象，热路径（如高频包裹上报）会产生 GC 压力；`readonly record struct` 是值类型，可栈分配或内联，性能更优。
- **分级**：P1（高性能隐患 + 规范性问题）
- **建议修复阶段/PR**：PR-FIX-B1（注意：字段超 64 字节的大值对象接口传递建议加 `in` 参数修饰符）

---

#### [P1-B-002] `BagInfo.cs` 含 `[Index]` EF Core 属性侵入 Domain

- **文件路径**：`Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/BagInfo.cs`
- **行号区间**：L14-L15
- **证据描述**：
  ```csharp
  [Index(nameof(ChuteId), IsUnique = true)]   // Microsoft.EntityFrameworkCore 属性
  [Index(nameof(BagCode), IsUnique = true)]
  public sealed record class BagInfo {
  ```
  `[Index]` 来自 `Microsoft.EntityFrameworkCore`，是数据库索引声明，属于 ORM 映射关注点。文件注释自称"ORM 映射与独立表结构由 Infrastructure 层负责"，代码与注释自相矛盾，且违反硬性规则"Domain 禁止依赖 Infrastructure 实现"。
- **分级**：P1（基础设施依赖侵入 Domain，结构边界违规）
- **建议修复阶段/PR**：PR-FIX-B1（删除 `[Index]`，在 `Infrastructure/Persistence/Configurations/` 的对应 `IEntityTypeConfiguration` 中通过 Fluent API 声明：`builder.HasIndex(x => x.ChuteId).IsUnique()`）

---

#### [P1-B-003] `CommandInfo.cs` 引入 `System.Net.Sockets.ProtocolType`，基础设施枚举侵入 Domain

- **文件路径**：`Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/CommandInfo.cs`
- **行号区间**：L4（using），L21（字段类型）
- **证据描述**：
  ```csharp
  using System.Net.Sockets;
  ...
  public required ProtocolType ProtocolType { get; init; }  // System.Net.Sockets.ProtocolType
  ```
  `System.Net.Sockets.ProtocolType` 是 .NET 底层套接字协议枚举（TCP=6、UDP=17 等），属于网络传输层基础设施关注点，不属于领域模型。Domain 层感知传输协议实现细节违反分层边界。
- **分级**：P1（基础设施类型侵入 Domain）
- **建议修复阶段/PR**：PR-FIX-B1（在 `Domain/Enums/` 下新建 `CommunicationProtocolType.cs` 枚举替换，保留 TCP/UDP/SerialPort 等领域相关值）

---

#### [P1-B-004] 事件载荷声明为 `internal`，阻断 Application/Infrastructure 层消费

- **影响文件**：`ParcelChuteAssignedEventArgs.cs`、`ParcelScannedEventArgs.cs`
- **行号区间**：`ParcelChuteAssignedEventArgs.cs L7`、`ParcelScannedEventArgs.cs L7`
- **证据描述**：
  ```csharp
  internal readonly record struct ParcelChuteAssignedEventArgs { ... }
  internal readonly record struct ParcelScannedEventArgs { ... }
  ```
  事件载荷的设计意图是跨层通知（通过 MediatR 或自研事件总线在 Application/Infrastructure 中订阅处理），`internal` 访问限制导致 Application/Infrastructure 层无法访问，违背事件设计初衷。当前 `InternalsVisibleTo` 仅暴露给 `Host.Tests`，未授权 Application 层。
- **分级**：P1（功能性隐患，跨层消费被阻断）
- **建议修复阶段/PR**：PR-FIX-B2（改为 `public readonly record struct`；若确认仅 Domain 内使用，需在注释中明确说明消费范围并添加 Domain 内事件分发逻辑）

---

#### [P1-B-005] `IEntity<T>` 接口强制 `Id` 具有 public setter，破坏实体不变性

- **文件路径**：`Zeye.Sorting.Hub.Domain/Abstractions/IEntity.cs`
- **行号区间**：L7
- **证据描述**：
  ```csharp
  TPrimaryKey Id { get; set; }   // ❌ set 允许任意代码修改实体唯一标识
  ```
  领域实体 Id 是唯一标识，创建后不应被外部代码随意修改。`public set` 允许任意调用方执行 `entity.Id = xxx`，破坏领域不变性约束。
- **分级**：P1（领域模型不变性被破坏）
- **建议修复阶段/PR**：PR-FIX-B2（改为 `TPrimaryKey Id { get; }` 或 `{ get; init; }`，实现类使用 `protected set`；EF Core 通过反射绕过访问限制，无需 public setter）

---

#### [P1-B-006] `AuditableEntity` 的 `ModifyTime`、`ModifyIp` 有 public setter，可伪造审计记录

- **文件路径**：`Zeye.Sorting.Hub.Domain/Primitives/AuditableEntity.cs`
- **行号区间**：L30（`ModifyTime`），L35（`ModifyIp`）
- **证据描述**：
  ```csharp
  public DateTime ModifyTime { get; set; } = DateTime.Now;  // ❌ 任意代码可伪造修改时间
  public string ModifyIp { get; set; } = string.Empty;      // ❌ 任意代码可伪造来源IP
  ```
  审计字段的 public setter 允许业务代码直接覆写审计信息，导致审计记录失去可信度，丧失审计意义。应由 EF Core SaveChanges 拦截器统一注入审计字段值。
- **分级**：P1（审计可靠性被破坏）
- **建议修复阶段/PR**：PR-FIX-B2（`ModifyTime`/`ModifyIp` 改为 `protected set`；在 Infrastructure 的 `ISaveChangesInterceptor` 实现中统一写入）

---

#### [P1-B-007] 查询读模型 DTO 置于 Domain 层，违反分层规则

- **影响文件**：`Repositories/Models/ReadModels/ParcelSummaryReadModel.cs`、`WebRequestAuditLogDetailReadModel.cs`、`WebRequestAuditLogSummaryReadModel.cs`
- **行号区间**：各文件全文
- **证据描述**：`.github/copilot-instructions.md` 明确规定"**查询接口与查询 DTO 禁止放在 Domain**"。`ParcelSummaryReadModel` 等是面向查询场景的扁平化投影模型，不是领域实体或值对象，不属于 Domain 层职责。
- **分级**：P1（分层边界严重违规）
- **建议修复阶段/PR**：PR-FIX-B3（移至 `Application/Abstractions/Queries/ReadModels/`）

---

#### [P1-B-008] `IWebRequestAuditLogQueryRepository` 置于 Domain 层，违反分层规则

- **文件路径**：`Zeye.Sorting.Hub.Domain/Repositories/IWebRequestAuditLogQueryRepository.cs`
- **行号区间**：全文
- **证据描述**：`.github/copilot-instructions.md` 规定"查询/读服务接口：`I{Name}QueryService` / `I{Name}ReadService`"且应置于 `Application/Abstractions/Queries`。当前接口不仅放错位置，命名也应由 `IWebRequestAuditLogQueryRepository` 改为 `IWebRequestAuditLogQueryService`。
- **分级**：P1（分层边界违规 + 命名不规范）
- **建议修复阶段/PR**：PR-FIX-B3（移至 `Application/Abstractions/Queries/`，重命名为 `IWebRequestAuditLogQueryService`）

---

#### [P1-B-009] `IParcelRepository` CQRS 混用，命令与查询方法共存

- **文件路径**：`Zeye.Sorting.Hub.Domain/Repositories/IParcelRepository.cs`
- **行号区间**：L28-L94（查询方法区间）
- **证据描述**：接口同时包含：
  - 写操作：`AddAsync`、`UpdateAsync`、`RemoveAsync`、`AddRangeAsync`、`RemoveExpiredAsync`
  - 读查询：`GetPagedAsync`、`GetByBagCodeAsync`、`GetByWorkstationNameAsync`、`GetByStatusAsync`、`GetByChuteAsync`、`GetAdjacentByIdAsync`（均返回 `PageResult<ParcelSummaryReadModel>`）
  
  同一接口承担写仓储和读查询两种职责，违反 CQRS 分离原则，且与已分离的 `IWebRequestAuditLogQueryRepository` 不一致，属于影分身模式隐患。
- **分级**：P1（CQRS 职责混用，与现有模式不一致）
- **建议修复阶段/PR**：PR-FIX-B3（将查询方法提取至 `Application/Abstractions/Queries/IParcelQueryService`，`IParcelRepository` 仅保留聚合 CRUD）

---

#### [P1-B-010] `Domain.csproj` 引用 `Polly`，基础设施依赖侵入 Domain

- **文件路径**：`Zeye.Sorting.Hub.Domain/Zeye.Sorting.Hub.Domain.csproj`
- **行号区间**：L17（`<PackageReference Include="Polly" Version="8.6.5" />`）
- **证据描述**：Polly 是重试/熔断/限流弹性框架，属于基础设施关注点。Domain 层不应感知任何重试策略或熔断逻辑，否则违反"Domain 禁止依赖 Infrastructure"的硬性规则。
- **分级**：P1（基础设施依赖侵入 Domain，架构边界违规）
- **建议修复阶段/PR**：PR-FIX-B2（从 `Domain.csproj` 移除 Polly；若 Domain 需要弹性抽象，定义接口而非引用 Polly 具体类型）

---

### P2 问题（9 类，约 35 个文件）

#### [P2-B-001] 大量文件含冗余 using（已启用 ImplicitUsings）

- **影响文件（26+ 个）**：`AuditableEntity.cs`、`ApiRequestInfo.cs`、`BagInfo.cs`、`BarCodeInfo.cs`、`ChuteInfo.cs`、`CommandInfo.cs`、`GrayDetectorInfo.cs`、`ImageInfo.cs`、`ParcelDeviceInfo.cs`、`ParcelPositionInfo.cs`、`SorterCarrierInfo.cs`、`StickingParcelInfo.cs`、`VideoInfo.cs`、`VolumeInfo.cs`、`WeightInfo.cs`、`ActionType.cs`、`ApiRequestStatus.cs`、`ApiRequestType.cs`、`BarCodeType.cs`、`CommandDirection.cs`、`ImageCaptureType.cs`、`ImageType.cs`、`NoReadType.cs`、`ParcelStatus.cs`、`ParcelType.cs`、`VideoNodeType.cs`、`VolumeSourceType.cs`、`Parcel.cs`
- **行号区间**：各文件 L1-L5（冗余 using 集中出现）
- **证据描述**：`Domain.csproj` 启用了 `<ImplicitUsings>enable</ImplicitUsings>`，以下 using 均已由隐式 using 提供或根本未被使用：`using System;`、`using System.Linq;`、`using System.Text;`、`using System.Threading.Tasks;`、`using System.Collections.Generic;`
- **分级**：P2（代码噪音，影响可读性）
- **建议修复阶段/PR**：PR-FIX-B4（低风险批量清理）

---

#### [P2-B-002] `WebRequestAuditLogDetail.cs` 大量冗余默认值赋值

- **文件路径**：`Zeye.Sorting.Hub.Domain/Aggregates/AuditLogs/WebRequests/WebRequestAuditLogDetail.cs`
- **行号区间**：L118-L183
- **证据描述**：以下属性均显式赋值为各自的 C# 类型零值（等同于不赋值），属于无意义冗余：
  ```csharp
  public bool HasFileAccess { get; set; } = false;          // bool 默认即 false
  public int FileCount { get; set; } = 0;                   // int 默认即 0
  public long FileTotalBytes { get; set; } = 0;             // long 默认即 0
  public bool HasImageAccess { get; set; } = false;
  public int ImageCount { get; set; } = 0;
  public bool HasDatabaseAccess { get; set; } = false;
  public int DatabaseAccessCount { get; set; } = 0;
  public long DatabaseDurationMs { get; set; } = 0;
  public long ActionDurationMs { get; set; } = 0;
  public long MiddlewareDurationMs { get; set; } = 0;
  ```
- **分级**：P2（代码冗余，影响可读性）
- **建议修复阶段/PR**：PR-FIX-B4

---

#### [P2-B-003] `ActionType.cs` 含残留注释死代码（注释掉的枚举成员）

- **文件路径**：`Zeye.Sorting.Hub.Domain/Enums/ActionType.cs`
- **行号区间**：L27-L28（`//发送目标格口信息`），L47（`//绑定小车`）
- **证据描述**：枚举值序列出现跳号（1→3，11→12），被注释掉的条目是残留死代码。`.github/copilot-instructions.md` 规则27明确规定"禁止使用过时标记去标记代码，如果代码已过时则必须删除"。
- **分级**：P2（违反规范，残留死代码）
- **建议修复阶段/PR**：PR-FIX-B4（删除注释行；若枚举值已废弃，在 `更新记录.md` 中记录原因）

---

#### [P2-B-004] `Parcel.Create` 工厂方法参数数量达 23 个

- **文件路径**：`Zeye.Sorting.Hub.Domain/Aggregates/Parcels/Parcel.cs`
- **行号区间**：L259-L283
- **证据描述**：工厂方法拥有 23 个形参，严重违反单一职责可读性原则，调用方极易出现参数位置错误（尤其是多个 `decimal`、`bool`、`long` 类型的连续参数）。同时方法体首行缩进与类体 8 空格缩进不一致（`public static Parcel Create(` 处缩进为 4 空格）。
- **分级**：P2（可读性差，易错性高）
- **建议修复阶段/PR**：PR-FIX-B3（引入 `CreateParcelParameters` 参数对象替换过多参数）

---

#### [P2-B-005] `[Precision]`（EF Core Abstractions）属性残留在 Domain 值对象与聚合中

- **影响文件**：`Parcel.cs`（多处 `[Precision(18, 3)]`）、`VolumeInfo.cs`、`WeightInfo.cs`、`SorterCarrierInfo.cs`、`ParcelPositionInfo.cs`
- **行号区间**：`Parcel.cs L83` 等
- **证据描述**：`[Precision]` 来自 `Microsoft.EntityFrameworkCore.Abstractions`，是数据库精度声明。`Parcel.cs` 注释自称"字段精度在 Infrastructure/EntityConfigurations 中通过 Fluent API 统一声明"，但 `[Precision]` 仍残留在域模型中，注释与实现矛盾。`[MaxLength]` 属标准 DataAnnotations 验证注解可保留。
- **分级**：P2（EF 关注点混入 Domain，规范性问题）
- **建议修复阶段/PR**：PR-FIX-B2（将 `[Precision]` 全部迁移到 Infrastructure 的 `IEntityTypeConfiguration` 中）

---

#### [P2-B-006] `ParcelQueryFilter.cs` 和 `MaxTimeRangeAttribute.cs` 类体缩进不一致

- **影响文件**：`Repositories/Models/Filters/ParcelQueryFilter.cs`、`Repositories/Models/Validation/MaxTimeRangeAttribute.cs`
- **行号区间**：`ParcelQueryFilter.cs L6-10`；`MaxTimeRangeAttribute.cs L6-8`
- **证据描述**：类体的 XML 注释和特性处于列 0，未相对 namespace 块缩进 4 空格，与仓库其他文件缩进风格不一致。
- **分级**：P2（代码风格不一致）
- **建议修复阶段/PR**：PR-FIX-B4

---

#### [P2-B-007] `LogCleanupSettings` 置于 Domain 层不符合职责定位

- **文件路径**：`Zeye.Sorting.Hub.Domain/Options/LogCleanup/LogCleanupSettings.cs`
- **行号区间**：全文
- **证据描述**：日志清理是基础设施运维配置，与领域业务无关，不应置于 Domain 层。应迁移至 `Host/Options/LogCleanup/` 或 `Infrastructure/DependencyInjection/Options/`。
- **分级**：P2（职责定位不合规）
- **建议修复阶段/PR**：PR-FIX-B3

---

#### [P2-B-008] `Domain.csproj` 含无意义的空占位目录声明

- **文件路径**：`Zeye.Sorting.Hub.Domain/Zeye.Sorting.Hub.Domain.csproj`
- **行号区间**：L11-L13
- **证据描述**：
  ```xml
  <Folder Include="Projections\" />
  <Folder Include="Attributes\" />
  ```
  空文件夹项仅为 IDE 占位，长期未使用会造成"未完成"误导。
- **分级**：P2（项目文件噪音）
- **建议修复阶段/PR**：PR-FIX-B4

---

#### [P2-B-009] `PageRequest`/`PageResult` 置于 Domain 层（查询基础设施抽象越界）

- **影响文件**：`Repositories/Models/Paging/PageRequest.cs`、`Repositories/Models/Paging/PageResult.cs`
- **行号区间**：各文件全文
- **证据描述**：分页参数与分页结果是查询横切关注点，与 P1-B-007 问题同源，应随查询 DTO 一并迁移至 `Application/Abstractions/Queries/Paging/`。
- **分级**：P2（与 P1-B-007 同源，随其修复迁出）
- **建议修复阶段/PR**：PR-FIX-B3（随 P1-B-007 一并处理）

---

## 四、合规性检查汇总

| 规则 | 状态 | 违规文件数 | 说明 |
|------|------|-----------|------|
| 禁止 UTC 时间（`DateTime.UtcNow` 等） | ✅ 合规 | 0 | 时间字段注释均标注"本地时间语义" |
| 所有枚举在 `Enums/` 子目录下 | ✅ 合规 | 0 | — |
| 枚举项必须有 `Description` 和注释 | ✅ 合规 | 0 | — |
| 事件载荷使用 `readonly record struct` | ✅ struct 形式合规 | 0（但有 internal 问题） | 见 P1-B-004 |
| **值对象使用 `readonly record struct`** | ❌ 违规 | 14 | 见 P1-B-001 |
| 方法必须有注释 | ✅ 合规 | 0 | — |
| 类字段必须有注释 | ✅ 合规 | 0 | — |
| 命名空间与物理路径严格一致 | ✅ 合规 | 0 | — |
| 禁止过时标记（注释残留死代码） | ❌ 违规 | 1 | 见 P2-B-003（ActionType.cs） |
| 每个类独立文件 | ✅ 合规 | 0 | — |
| 分层边界（查询 DTO/接口不在 Domain） | ❌ 违规 | 8 | 见 P1-B-007、P1-B-008、P1-B-009 |
| Domain 不依赖 Infrastructure 实现 | ❌ 违规 | 3 | Polly 引用、`[Index]`、`ProtocolType` |

---

## 五、修复 PR 规划

| PR | 修复内容 | 问题级别 | 预估风险 |
|----|---------|---------|---------|
| **PR-FIX-B1** | 14 个值对象改为 `readonly record struct`；删除 `BagInfo.cs` 的 `[Index]`（移至 Infrastructure Fluent API）；`CommandInfo.cs` 新建领域枚举 `CommunicationProtocolType` 替换 `System.Net.Sockets.ProtocolType` | P1 | 中（涉及 EF Core owned entity struct 配置） |
| **PR-FIX-B2** | `IEntity`/`AuditableEntity` setter 约束；事件载荷改为 `public`；`Domain.csproj` 移除 Polly；`[Precision]` 全部迁至 Infrastructure Fluent API | P1 | 低 |
| **PR-FIX-B3** | 查询读模型/过滤器/分页类迁至 `Application/Abstractions/Queries/`；`IParcelRepository` CQRS 分离（查询方法提取至 `IParcelQueryService`）；`IWebRequestAuditLogQueryRepository` 移至 Application 并重命名；`LogCleanupSettings` 迁至 Host/Infrastructure | P1 | 高（涉及跨层移动，需更新所有调用方引用） |
| **PR-FIX-B4** | 批量清理冗余 using；删除 `WebRequestAuditLogDetail.cs` 冗余默认值；删除 `ActionType.cs` 残留注释；修正缩进；删除 csproj 空占位目录 | P2 | 低（批量规范清理） |

---

## 六、未覆盖文件清单

本批次计划 67 个文件已全部检查，无未覆盖文件。

---

## 七、下一批 PR 计划

| PR | 覆盖目录 | 预估文件数 |
|----|---------|----------|
| PR-C | `Zeye.Sorting.Hub.Application/` + `Zeye.Sorting.Hub.Contracts/` | ~30 |
| PR-D | `Zeye.Sorting.Hub.Infrastructure/` | ~60 |
| PR-E | `Zeye.Sorting.Hub.Host/` | ~50 |
| PR-F | `Zeye.Sorting.Hub.SharedKernel/` + `Zeye.Sorting.Hub.Host.Tests/` + 占位子域项目 | ~50 |

---

## 八、对账结果

- **本PR计划检查文件数**：67
- **本PR实际已检查文件数**：67
- **对账差异**：0 ✅
- **累计已检查文件数**：88 / 286（21 [PR-A] + 67 [PR-B]）
- **剩余待检查文件数**：198
