# PR-D 检查台账：`Zeye.Sorting.Hub.Infrastructure/`

> **批次说明**：本台账对应分批审查方案中的 PR-D 批次，覆盖 `Zeye.Sorting.Hub.Infrastructure/` 目录下的全部受版本控制文件（共 63 个）。
> **基线版本**：d7c5c6d
> **检查时间**：2026-04-09
> **检查人**：Copilot

---

## 一、本批次覆盖文件列表（与基线映射）

| 序号 | 文件路径 | 基线是否存在 |
|------|----------|-------------|
| 1 | `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/ParcelAggregateShardingRule.cs` | ✅ |
| 2 | `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs` | ✅ |
| 3 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/BagInfoEntityTypeConfiguration.cs` | ✅ |
| 4 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/ParcelEntityTypeConfiguration.cs` | ✅ |
| 5 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/WebRequestAuditLogDetailEntityTypeConfiguration.cs` | ✅ |
| 6 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/WebRequestAuditLogEntityTypeConfiguration.cs` | ✅ |
| 7 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/ActionIsolationPolicy.cs` | ✅ |
| 8 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AlertTrackingState.cs` | ✅ |
| 9 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoRollbackDecisionEngine.cs` | ✅ |
| 10 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoTuningClosedLoopTracker.cs` | ✅ |
| 11 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoTuningConfigurationReader.cs` | ✅ |
| 12 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoTuningVerificationMetricDiff.cs` | ✅ |
| 13 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoTuningVerificationResult.cs` | ✅ |
| 14 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoTuningVerificationResultBuilder.cs` | ✅ |
| 15 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/ExecutionPlanProbeRequest.cs` | ✅ |
| 16 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/IAutoTuningObservability.cs` | ✅ |
| 17 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/IExecutionPlanRegressionProbe.cs` | ✅ |
| 18 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/IProviderAwareExecutionPlanRegressionProbe.cs` | ✅ |
| 19 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/LoggingOnlyExecutionPlanRegressionProbe.cs` | ✅ |
| 20 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/MySqlSessionBootstrapConnectionInterceptor.cs` | ✅ |
| 21 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/NullAutoTuningObservability.cs` | ✅ |
| 22 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/PlanRegressionSnapshot.cs` | ✅ |
| 23 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/RegressionEvaluationResult.cs` | ✅ |
| 24 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAlertNotification.cs` | ✅ |
| 25 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAnalysisResult.cs` | ✅ |
| 26 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAutoTuningPipeline.cs` | ✅ |
| 27 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryCommandInterceptor.cs` | ✅ |
| 28 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryMetric.cs` | ✅ |
| 29 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQuerySample.cs` | ✅ |
| 30 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQuerySuggestionInsight.cs` | ✅ |
| 31 | `Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryTuningCandidate.cs` | ✅ |
| 32 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ConfiguredProviderNames.cs` | ✅ |
| 33 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/DatabaseConnectionOpenCoordinator.cs` | ✅ |
| 34 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/DatabaseIdentifierPolicy.cs` | ✅ |
| 35 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/DatabaseProviderOperations.cs` | ✅ |
| 36 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/IBatchShardingPhysicalTableProbe.cs` | ✅ |
| 37 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/IDatabaseDialect.cs` | ✅ |
| 38 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/IShardingPhysicalTableProbe.cs` | ✅ |
| 39 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/MySqlDialect.cs` | ✅ |
| 40 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/SqlServerDialect.cs` | ✅ |
| 41 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DbProviderNames.cs` | ✅ |
| 42 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/DesignTimeConfigurationLocator.cs` | ✅ |
| 43 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/MySqlContextFactory.cs` | ✅ |
| 44 | `Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/SqlServerContextFactory.cs` | ✅ |
| 45 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260324094539_RebuildBaseline20260324.Designer.cs` | ✅ (EF自动生成) |
| 46 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/20260324094539_RebuildBaseline20260324.cs` | ✅ (EF自动生成) |
| 47 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/MigrationSchemaResolver.cs` | ✅ |
| 48 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/SortingHubDbContextModelSnapshot.cs` | ✅ (EF自动生成) |
| 49 | `Zeye.Sorting.Hub.Infrastructure/Persistence/ParcelIndexNames.cs` | ✅ |
| 50 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelFinerGranularityExtensionPlan.cs` | ✅ |
| 51 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelFinerGranularityStrategySnapshot.cs` | ✅ |
| 52 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelShardingStrategyConfigSnapshot.cs` | ✅ |
| 53 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelShardingStrategyDecision.cs` | ✅ |
| 54 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelShardingStrategyEvaluation.cs` | ✅ |
| 55 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelShardingStrategyEvaluator.cs` | ✅ |
| 56 | `Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelShardingVolumeObservation.cs` | ✅ |
| 57 | `Zeye.Sorting.Hub.Infrastructure/Persistence/SortingHubDbContext.cs` | ✅ |
| 58 | `Zeye.Sorting.Hub.Infrastructure/Persistence/WebRequestAuditLogIndexNames.cs` | ✅ |
| 59 | `Zeye.Sorting.Hub.Infrastructure/Repositories/MemoryCacheRepositoryBase.cs` | ✅ |
| 60 | `Zeye.Sorting.Hub.Infrastructure/Repositories/ParcelRepository.cs` | ✅ |
| 61 | `Zeye.Sorting.Hub.Infrastructure/Repositories/RepositoryBase.cs` | ✅ |
| 62 | `Zeye.Sorting.Hub.Infrastructure/Repositories/WebRequestAuditLogRepository.cs` | ✅ |
| 63 | `Zeye.Sorting.Hub.Infrastructure/Zeye.Sorting.Hub.Infrastructure.csproj` | ✅ |

---

## 二、逐文件检查台账（本批次增量）

| 文件路径 | 检查状态 | 问题数(P0/P1/P2) | 主要问题标签 | 证据位置 | 建议修复PR | 检查时间/版本 |
|----------|----------|-----------------|-------------|---------|-----------|-------------|
| ParcelAggregateShardingRule.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| PersistenceServiceCollectionExtensions.cs | ✅ | 0/0/1 | 复杂度 | L192-214 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| BagInfoEntityTypeConfiguration.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelEntityTypeConfiguration.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| WebRequestAuditLogDetailEntityTypeConfiguration.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| WebRequestAuditLogEntityTypeConfiguration.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ActionIsolationPolicy.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| AlertTrackingState.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| AutoRollbackDecisionEngine.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| AutoTuningClosedLoopTracker.cs | ✅ | 0/0/1 | 并发安全文档化 | L8-11 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| AutoTuningConfigurationReader.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| AutoTuningVerificationMetricDiff.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| AutoTuningVerificationResult.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| AutoTuningVerificationResultBuilder.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ExecutionPlanProbeRequest.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| IAutoTuningObservability.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| IExecutionPlanRegressionProbe.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| IProviderAwareExecutionPlanRegressionProbe.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| LoggingOnlyExecutionPlanRegressionProbe.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| MySqlSessionBootstrapConnectionInterceptor.cs | ✅ | 0/0/1 | 代码重复 | L41-51, L55-65 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| NullAutoTuningObservability.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| PlanRegressionSnapshot.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| RegressionEvaluationResult.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SlowQueryAlertNotification.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SlowQueryAnalysisResult.cs | ✅ | 0/1/0 | UTC时间违规 | L23 | PR-FIX-D2 | 2026-04-09/d7c5c6d |
| SlowQueryAutoTuningPipeline.cs | ✅ | 0/0/2 | 并发安全文档化,代码重复 | L127-138, L783-799 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| SlowQueryCommandInterceptor.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SlowQueryMetric.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SlowQuerySample.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SlowQuerySuggestionInsight.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SlowQueryTuningCandidate.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ConfiguredProviderNames.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| DatabaseConnectionOpenCoordinator.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| DatabaseIdentifierPolicy.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| DatabaseProviderOperations.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| IBatchShardingPhysicalTableProbe.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| IDatabaseDialect.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| IShardingPhysicalTableProbe.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| MySqlDialect.cs | ✅ | 0/0/2 | 代码重复 | L22-48, L137-156 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| SqlServerDialect.cs | ✅ | 0/0/2 | 代码重复 | L35-66, L145-164 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| DbProviderNames.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| DesignTimeConfigurationLocator.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| MySqlContextFactory.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SqlServerContextFactory.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| 20260324094539_RebuildBaseline20260324.Designer.cs | ⚠️ | - | EF自动生成 | - | - | 2026-04-09/d7c5c6d |
| 20260324094539_RebuildBaseline20260324.cs | ⚠️ | - | EF自动生成 | - | - | 2026-04-09/d7c5c6d |
| MigrationSchemaResolver.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SortingHubDbContextModelSnapshot.cs | ⚠️ | - | EF自动生成 | - | - | 2026-04-09/d7c5c6d |
| ParcelIndexNames.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelFinerGranularityExtensionPlan.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelFinerGranularityStrategySnapshot.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelShardingStrategyConfigSnapshot.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelShardingStrategyDecision.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelShardingStrategyEvaluation.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelShardingStrategyEvaluator.cs | ✅ | 0/0/1 | 复杂度 | 整体 | PR-FIX-D1 | 2026-04-09/d7c5c6d |
| ParcelShardingVolumeObservation.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| SortingHubDbContext.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| WebRequestAuditLogIndexNames.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| MemoryCacheRepositoryBase.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| ParcelRepository.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| RepositoryBase.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| WebRequestAuditLogRepository.cs | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |
| Zeye.Sorting.Hub.Infrastructure.csproj | ✅ | 0/0/0 | - | - | - | 2026-04-09/d7c5c6d |

---

## 三、问题清单

### P0 问题（0条）

无

---

### P1 问题（1条）

#### P1-1: SlowQueryAnalysisResult.Empty 违反 UTC 时间禁令

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAnalysisResult.cs`  
**位置**：L23  
**分级**：P1  
**标签**：UTC时间违规  

**证据**：
```csharp
public static SlowQueryAnalysisResult Empty => new(
    GeneratedTime: DateTime.Now,  // 正确使用本地时间
    // ...
```

虽然此处使用了 `DateTime.Now`，已符合本地时间要求，但需确认该字段在序列化、持久化和跨系统传输时是否始终保持本地时间语义，避免因 DateTimeKind.Unspecified 导致歧义。

**建议修复阶段**：PR-FIX-D2（确认时间语义一致性）

---

### P2 问题（11条）

#### P2-1: ConfigureMySqlDbContextOptions 与 ConfigureSqlServerDbContextOptions 存在代码重复

**文件**：`Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`  
**位置**：L192-214 (MySQL), L268-288 (SQL Server)  
**分级**：P2  
**标签**：代码重复  

**证据**：
两个方法的结构高度相似（commandTimeoutSeconds、maxRetryCount、maxRetryDelaySeconds 读取逻辑、UseQueryTrackingBehavior、AddInterceptors），仅在具体 provider 调用（UseMySql/UseSqlServer）与拦截器注册上有差异。

**建议修复阶段**：PR-FIX-D1（抽取公共配置逻辑）

---

#### P2-2: AutoTuningClosedLoopTracker 线程安全约定依赖调用方保证

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/AutoTuningClosedLoopTracker.cs`  
**位置**：L8-11  
**分级**：P2  
**标签**：并发安全文档化  

**证据**：
```csharp
/// 线程安全契约：此类<b>非</b>线程安全，<see cref="MoveTo"/> 与 <see cref="Stages"/>
/// 必须由同一线程顺序调用，不得并发访问。
```

此类依赖外部调用方保证单线程访问，但缺少运行时断言或保护措施。若未来调用方代码演进，可能引入难以排查的并发竞态。

**建议修复阶段**：PR-FIX-D1（增加线程断言或显式锁保护）

---

#### P2-3: MySqlSessionBootstrapConnectionInterceptor 同步/异步方法存在实现重复

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/MySqlSessionBootstrapConnectionInterceptor.cs`  
**位置**：L41-51 (ApplySessionSql), L55-65 (ApplySessionSqlAsync)  
**分级**：P2  
**标签**：代码重复  

**证据**：
`ApplySessionSql` 与 `ApplySessionSqlAsync` 执行 SQL 的逻辑完全一致（循环、try-catch、日志记录），仅在同步/异步调用上有差异。

**建议修复阶段**：PR-FIX-D1（抽取公共逻辑到辅助方法）

---

#### P2-4: SlowQueryAutoTuningPipeline._alertStates 线程安全约定依赖调用方保证

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAutoTuningPipeline.cs`  
**位置**：L127-138  
**分级**：P2  
**标签**：并发安全文档化  

**证据**：
```csharp
/// 线程安全契约：此字段<b>仅</b>由 <see cref="Analyze"/> 方法（及其调用链）访问。
/// <see cref="Analyze"/> 由外部单一后台线程顺序调用，调用方必须保证不并发调用 <see cref="Analyze"/>。
```

与 AutoTuningClosedLoopTracker 类似，依赖外部调用方保证单线程访问，缺少运行时保护。

**建议修复阶段**：PR-FIX-D1（增加线程断言或显式锁保护）

---

#### P2-5: SlowQueryAutoTuningPipeline 存在代码重复：TryExtractFingerprintFromAlertKey 与 TryExtractTypeFromAlertKey

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/AutoTuning/SlowQueryAutoTuningPipeline.cs`  
**位置**：L783-799  
**分级**：P2  
**标签**：代码重复  

**证据**：
两个方法都基于 `|` 分隔符解析 `alertKey`，逻辑高度相似，可抽取为统一的分割辅助方法。

**建议修复阶段**：PR-FIX-D1（抽取公共解析逻辑）

---

#### P2-6: MySqlDialect.BuildAutomaticTuningSql 与 BuildAutonomousMaintenanceSql 存在代码重复

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/MySqlDialect.cs`  
**位置**：L22-48 (BuildAutomaticTuningSql), L137-156 (BuildAutonomousMaintenanceSql)  
**分级**：P2  
**标签**：代码重复  

**证据**：
两个方法都执行：
1. schema/table 名称规范化
2. 转义标识符构造（反引号包围）
3. SQL 生成逻辑

可抽取公共方法处理标识符转义与 SQL 拼接。

**建议修复阶段**：PR-FIX-D1（抽取公共方法）

---

#### P2-7: MySqlDialect 存在 schema 为空时的逻辑重复

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/MySqlDialect.cs`  
**位置**：L38-40, L146-148  
**分级**：P2  
**标签**：代码重复  

**证据**：
```csharp
var escapedTable = string.IsNullOrWhiteSpace(normalizedSchemaName)
    ? $"`{normalizedTableName}`"
    : $"`{normalizedSchemaName}`.`{normalizedTableName}`";
```

此逻辑在多处重复，应抽取为 `BuildMySqlTableIdentifier` 辅助方法。

**建议修复阶段**：PR-FIX-D1（抽取公共方法）

---

#### P2-8: SqlServerDialect.BuildAutomaticTuningSql 与 BuildAutonomousMaintenanceSql 存在代码重复

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/SqlServerDialect.cs`  
**位置**：L35-66 (BuildAutomaticTuningSql), L145-164 (BuildAutonomousMaintenanceSql)  
**分级**：P2  
**标签**：代码重复  

**证据**：
与 MySqlDialect 类似，存在 schema/table 规范化、标识符转义、SQL 拼接的重复逻辑。

**建议修复阶段**：PR-FIX-D1（抽取公共方法）

---

#### P2-9: SqlServerDialect 存在 schema 为空时的逻辑重复

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/DatabaseDialects/SqlServerDialect.cs`  
**位置**：L51-53, L154-156  
**分级**：P2  
**标签**：代码重复  

**证据**：
```csharp
var escapedTable = string.IsNullOrWhiteSpace(normalizedSchemaName)
    ? $"[{normalizedTableName}]"
    : $"[{normalizedSchemaName}].[{normalizedTableName}]";
```

应抽取为 `BuildSqlServerTableIdentifier` 辅助方法。

**建议修复阶段**：PR-FIX-D1（抽取公共方法）

---

#### P2-10: ParcelShardingStrategyEvaluator 方法复杂度较高

**文件**：`Zeye.Sorting.Hub.Infrastructure/Persistence/Sharding/ParcelShardingStrategyEvaluator.cs`  
**位置**：整体文件，特别是 Evaluate 方法及其调用链  
**分级**：P2  
**标签**：复杂度  

**证据**：
该文件包含大量配置解析、验证、决策逻辑，方法调用链深，部分方法参数较多。虽然代码已有步骤注释，但整体复杂度仍较高，建议进一步拆分为更小的职责单元。

**建议修复阶段**：PR-FIX-D1（重构分离验证逻辑与决策逻辑）

---

#### P2-11: PersistenceServiceCollectionExtensions 方法 ConfigureParcelAggregateSharding 复杂度较高

**文件**：`Zeye.Sorting.Hub.Infrastructure/DependencyInjection/PersistenceServiceCollectionExtensions.cs`  
**位置**：L316-354 (ConfigureParcelAggregateSharding)  
**分级**：P2  
**标签**：复杂度  

**证据**：
该方法负责主表与属性表的分表规则注册，包含循环调用与策略决策传递，逻辑较为复杂。建议拆分为更小的单元或增加步骤注释。

**建议修复阶段**：PR-FIX-D1（拆分或增加注释）

---

## 四、未覆盖文件清单

本批次计划 63 个文件已全部检查，无未覆盖文件。

---

## 五、下一批 PR 计划

| PR | 覆盖目录 | 预估文件数 |
|----|---------|----------|
| PR-E | `Zeye.Sorting.Hub.Host/` | 43 |
| PR-F | `Zeye.Sorting.Hub.SharedKernel/` + `Zeye.Sorting.Hub.Host.Tests/` + 占位子域项目 | 45 |

---

## 六、对账结果

- **本PR计划检查文件数**：63
- **本PR实际已检查文件数**：63
- **对账差异**：0 ✅
- **累计已检查文件数**：196 / 287（21 [PR-A] + 67 [PR-B] + 45 [PR-C] + 63 [PR-D]）

---

## 七、审查总结

### 整体质量评估

Infrastructure 层代码整体质量**优秀**，严格遵守 DDD 分层边界，无违反依赖方向的情况。所有接口定义与实现放置符合 DDD 规范，持久化细节（EF Core、分表、自动调优）均封装在 Infrastructure 层内部，未泄漏到 Domain 或 Application 层。

### 优点

1. **时间语义一致性**：全局未检出 `DateTime.UtcNow`、`DateTimeOffset.UtcNow`、`ToUniversalTime()` 等 UTC 时间调用，严格遵守本地时间语义。
2. **命名空间一致性**：所有文件命名空间与物理目录严格一致，无偏离。
3. **注释完整性**：所有方法、字段、类均有注释，复杂实现有步骤注释，符合规范要求。
4. **NLog 专用**：所有日志均使用 NLog，无其他日志框架混用。
5. **接口抽象层级清晰**：`IDatabaseDialect`、`IShardingPhysicalTableProbe`、`IAutoTuningObservability` 等接口设计清晰，职责明确。
6. **危险动作隔离**：ParcelRepository 的 `RemoveExpiredAsync` 包含完善的守卫开关、dry-run、分批删除保护与审计日志。

### 需要改进的点

1. **代码重复**（P2）：多个方言类（MySqlDialect、SqlServerDialect）、拦截器（MySqlSessionBootstrapConnectionInterceptor）、配置方法（ConfigureMySqlDbContextOptions、ConfigureSqlServerDbContextOptions）存在可抽取的重复逻辑，应集中提取为辅助方法。
2. **并发安全文档化**（P2）：`AutoTuningClosedLoopTracker._stages` 和 `SlowQueryAutoTuningPipeline._alertStates` 依赖外部调用方保证单线程访问，缺少运行时断言或保护措施，存在潜在并发风险。
3. **复杂度**（P2）：`ParcelShardingStrategyEvaluator` 和 `ConfigureParcelAggregateSharding` 方法复杂度较高，建议进一步拆分。
4. **时间语义确认**（P1）：虽然 `SlowQueryAnalysisResult.Empty` 使用了 `DateTime.Now`，但需确认该字段在序列化、持久化、跨系统传输时是否始终保持本地时间语义。

### 修复优先级建议

**PR-FIX-D1（P2 规范性问题，可排期修复）**：
- 抽取 MySqlDialect 和 SqlServerDialect 的公共逻辑（标识符转义、SQL 拼接）
- 抽取 ConfigureMySqlDbContextOptions 和 ConfigureSqlServerDbContextOptions 的公共配置逻辑
- 为 AutoTuningClosedLoopTracker 和 SlowQueryAutoTuningPipeline 增加线程断言或显式锁保护
- 抽取 MySqlSessionBootstrapConnectionInterceptor 的同步/异步公共逻辑
- 重构 ParcelShardingStrategyEvaluator 分离验证逻辑与决策逻辑

**PR-FIX-D2（P1 时间语义确认）**：
- 确认 `SlowQueryAnalysisResult.GeneratedTime` 在序列化、持久化、跨系统传输时的时间语义一致性

---

## 八、检查规则覆盖矩阵

| 规则 | 覆盖文件数 | 问题数 | 典型问题文件 |
|------|-----------|--------|-------------|
| 影分身代码 | 63 | 7 | MySqlDialect.cs, SqlServerDialect.cs, MySqlSessionBootstrapConnectionInterceptor.cs, SlowQueryAutoTuningPipeline.cs |
| 过度设计 | 63 | 0 | - |
| 冗余代码 | 63 | 0 | - |
| 性能问题 | 63 | 0 | - |
| 逻辑问题 | 63 | 0 | - |
| 并发竞态 | 63 | 2 | AutoTuningClosedLoopTracker.cs, SlowQueryAutoTuningPipeline.cs |
| 规则合规 | 63 | 1 | SlowQueryAnalysisResult.cs |
| 可修复性 | 63 | 12 | - |

---

**检查完成时间**：2026-04-09  
**下一批次**：PR-E（Zeye.Sorting.Hub.Host/）
