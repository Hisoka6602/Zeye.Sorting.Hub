using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>数据库自动调谐后台服务：慢查询分析 + 闭环自治执行/验证/回退 + 审计日志</summary>
    public sealed class DatabaseAutoTuningHostedService : BackgroundService {
        /// <summary>
        /// 性能调优配置节前缀。
        /// </summary>
        private const string PerformanceConfigPrefix = "Persistence:PerformanceTuning";
        /// <summary>
        /// 可跟踪慢查询指纹数量上限，防止状态集合无限增长。
        /// </summary>
        private const int MaxTrackedFingerprintCount = 1000;
        /// <summary>
        /// 可跟踪热点表上限，避免内存无限增长。
        /// </summary>
        private const int MaxTrackedTableCount = 500;
        /// <summary>
        /// 单表容量快照保留上限，用于容量趋势预测。
        /// </summary>
        private const int MaxCapacitySnapshotsPerTable = 64;
        /// <summary>
        /// 不可用标签占位值，用于缺失维度的统一输出。
        /// </summary>
        private const string NotAvailableTag = "n/a";
        // 风险项采用加权叠加后再截断到 [0,1]，刻意不要求权重和为 1。
        /// <summary>
        /// 危险 DDL 动作风险权重。
        /// </summary>
        private const decimal DangerousActionRiskWeight = 0.45m;
        /// <summary>
        /// 高峰时段风险权重。
        /// </summary>
        private const decimal PeakWindowRiskWeight = 0.20m;
        /// <summary>
        /// 高 P99 延迟风险权重。
        /// </summary>
        private const decimal HighP99RiskWeight = 0.15m;
        /// <summary>
        /// 高超时率风险权重。
        /// </summary>
        private const decimal HighTimeoutRiskWeight = 0.15m;
        /// <summary>
        /// 高错误率风险权重。
        /// </summary>
        private const decimal HighErrorRiskWeight = 0.10m;
        /// <summary>
        /// 死锁风险权重。
        /// </summary>
        private const decimal DeadlockRiskWeight = 0.20m;
        /// <summary>
        /// 高 P99 判定阈值（毫秒）。
        /// </summary>
        private const double HighP99ThresholdMilliseconds = 1000d;
        /// <summary>
        /// 高超时率判定阈值（百分比）。
        /// </summary>
        private const decimal HighTimeoutThresholdPercent = 1m;
        /// <summary>
        /// 高错误率判定阈值（百分比）。
        /// </summary>
        private const decimal HighErrorThresholdPercent = 1m;
        /// <summary>
        /// 待回滚动作保留时长，超时后清理。
        /// </summary>
        private static readonly TimeSpan PendingRollbackRetention = TimeSpan.FromHours(24);
        /// <summary>
        /// 匹配 SQL Server 形态的 CREATE INDEX 语句，提取索引名与目标表名，用于自动调优动作审计与回滚语句关联。
        /// </summary>
        private static readonly Regex SqlServerCreateIndexRegex = new(
            @"\bcreate\s+(?:unique\s+)?index\s+\[(?<index>[^\]]+)\]\s+on\s+(?<table>\[[^\]]+\](?:\.\[[^\]]+\])?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        /// <summary>
        /// 匹配 MySQL 形态的 CREATE INDEX 语句，提取索引名与目标表名，用于方言分支下的索引动作识别与审计归档。
        /// </summary>
        private static readonly Regex MySqlCreateIndexRegex = new(
            @"\bcreate\s+(?:unique\s+)?index\s+`(?<index>[^`]+)`\s+on\s+(?<table>(?:`[^`]+`\.)?`[^`]+`)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        /// <summary>
        /// 匹配 SQL 文本前导注释（行注释/块注释），用于在风险分析前剥离注释噪声，避免误判 DDL 或查询结构。
        /// </summary>
        private static readonly Regex LeadingCommentRegex = new(
            @"^\s*(?:(--[^\r\n]*[\r\n]+)|(/\*.*?\*/\s*))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        /// <summary>
        /// 匹配高风险 DDL 关键字（create/alter/drop），用于自动动作隔离器判定动作危险等级与执行门禁。
        /// </summary>
        private static readonly Regex DangerousDdlRegex = new(
            @"\b(create|alter|drop)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 匹配 JOIN 关键字及其修饰词，用于识别多表关联查询场景并参与慢 SQL 复杂度判定。
        /// </summary>
        private static readonly Regex JoinKeywordRegex = new(
            @"\b(?:left|right|inner|full|cross)?\s*join\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 匹配集合运算关键字（union/intersect/except），用于识别跨结果集合并查询并触发更保守的索引建议策略。
        /// </summary>
        private static readonly Regex SetOperatorRegex = new(
            @"\b(union|intersect|except)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 匹配同一 SQL 中多次出现 FROM 的模式，用于检测子查询/嵌套查询结构，避免简单规则误提取主表。
        /// </summary>
        private static readonly Regex MultiFromRegex = new(
            @"\bfrom\b[\s\S]*\bfrom\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 匹配 FROM 子句主体片段（到 where/group/order/limit/having/set operator 为止），用于提取主表候选并做命中统计。
        /// </summary>
        private static readonly Regex FromClauseRegex = new(
            @"\bfrom\b(?<from>.+?)(?:\bwhere\b|\bgroup\s+by\b|\border\s+by\b|\blimit\b|\bhaving\b|\bunion\b|\bintersect\b|\bexcept\b|;|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        /// <summary>
        /// 匹配 CREATE INDEX 动作关键短语，用于在自动执行前识别索引创建动作并应用动作级治理策略。
        /// </summary>
        private static readonly Regex CreateIndexActionRegex = new(
            @"\bcreate\s+(?:unique\s+)?index\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// NLog 静态日志器实例（符合规则15：日志只能使用NLog；避免 ILogger 注入，消除 DI 解析开销）。
        /// </summary>
        private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// 自动调优可观测输出器（指标/事件）。
        /// </summary>
        private readonly IAutoTuningObservability _observability;
        /// <summary>
        /// 字段：_planRegressionProbe。
        /// </summary>
        private readonly IExecutionPlanRegressionProbe _planRegressionProbe;
        /// <summary>
        /// DI 作用域工厂，用于按周期解析仓储依赖。
        /// </summary>
        private readonly IServiceScopeFactory _scopeFactory;
        /// <summary>
        /// 字段：_dialect。
        /// </summary>
        private readonly IDatabaseDialect _dialect;
        /// <summary>
        /// 慢查询采集与分析管道。
        /// </summary>
        private readonly SlowQueryAutoTuningPipeline _pipeline;
        /// <summary>
        /// 字段：_analyzeIntervalSeconds。
        /// </summary>
        private readonly int _analyzeIntervalSeconds;
        /// <summary>
        /// 每轮最多执行的自动动作数。
        /// </summary>
        private readonly int _maxExecuteActionsPerCycle;
        /// <summary>
        /// 字段：_actionExecutionTimeoutSeconds。
        /// </summary>
        private readonly int _actionExecutionTimeoutSeconds;
        /// <summary>
        /// 高峰期是否跳过自动执行。
        /// </summary>
        private readonly bool _skipExecutionDuringPeak;
        /// <summary>
        /// 字段：_peakStartTime。
        /// </summary>
        private readonly TimeSpan _peakStartTime;
        /// <summary>
        /// 高峰时段结束时间（本地时间）。
        /// </summary>
        private readonly TimeSpan _peakEndTime;
        /// <summary>
        /// 字段：_enableAutoRollback。
        /// </summary>
        private readonly bool _enableAutoRollback;
        /// <summary>
        /// 危险动作隔离器总开关。
        /// </summary>
        private readonly bool _enableDangerousActionIsolator;
        /// <summary>
        /// 字段：_allowDangerousActionExecution。
        /// </summary>
        private readonly bool _allowDangerousActionExecution;
        /// <summary>
        /// 自动动作是否启用演练模式。
        /// </summary>
        private readonly bool _enableActionDryRun;
        /// <summary>
        /// 字段：_whitelistedTables。
        /// </summary>
        private readonly HashSet<string> _whitelistedTables;
        /// <summary>
        /// 指纹到待回滚动作的索引表，用于验证后触发回滚。
        /// </summary>
        private readonly Dictionary<string, PendingRollbackAction> _pendingRollbackByFingerprint = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 字段：_baselineCommandTimeoutSeconds。
        /// </summary>
        private readonly int _baselineCommandTimeoutSeconds;
        /// <summary>
        /// 基线重试次数配置。
        /// </summary>
        private readonly int _baselineMaxRetryCount;
        /// <summary>
        /// 字段：_baselineMaxRetryDelaySeconds。
        /// </summary>
        private readonly int _baselineMaxRetryDelaySeconds;
        /// <summary>
        /// 当前命令超时配置（秒）。
        /// </summary>
        private readonly int _configuredCommandTimeoutSeconds;
        /// <summary>
        /// 字段：_configuredMaxRetryCount。
        /// </summary>
        private readonly int _configuredMaxRetryCount;
        /// <summary>
        /// 当前最大重试间隔配置（秒）。
        /// </summary>
        private readonly int _configuredMaxRetryDelaySeconds;
        /// <summary>
        /// 字段：_enableFullAutomation。
        /// </summary>
        private readonly bool _enableFullAutomation;
        /// <summary>
        /// 进入执行策略评估的最小表热度调用次数。
        /// </summary>
        private readonly int _policyMinTableHeatCalls;
        /// <summary>
        /// 字段：_policyMaxRiskScore。
        /// </summary>
        private readonly decimal _policyMaxRiskScore;
        /// <summary>
        /// 高峰窗口最大允许风险分。
        /// </summary>
        private readonly decimal _policyPeakMaxRiskScore;
        /// <summary>
        /// 字段：_policyPeakMaxTableHeatCalls。
        /// </summary>
        private readonly int _policyPeakMaxTableHeatCalls;
        /// <summary>
        /// 自动验证总开关。
        /// </summary>
        private readonly bool _enableAutoValidation;
        /// <summary>
        /// 字段：_validationDelayCycles。
        /// </summary>
        private readonly int _validationDelayCycles;
        /// <summary>
        /// P95 回归判定阈值（百分比）。
        /// </summary>
        private readonly decimal _validationP95IncreasePercent;
        /// <summary>
        /// 字段：_validationP99IncreasePercent。
        /// </summary>
        private readonly decimal _validationP99IncreasePercent;
        /// <summary>
        /// 错误率回归判定阈值（百分比）。
        /// </summary>
        private readonly decimal _validationErrorRateIncreasePercent;
        /// <summary>
        /// 字段：_validationTimeoutRateIncreasePercent。
        /// </summary>
        private readonly decimal _validationTimeoutRateIncreasePercent;
        /// <summary>
        /// 死锁数回归判定阈值（增量计数）。
        /// </summary>
        private readonly int _validationDeadlockIncreaseCount;
        /// <summary>
        /// 字段：_enableCapacityPrediction。
        /// </summary>
        private readonly bool _enableCapacityPrediction;
        /// <summary>
        /// 容量预测天数窗口。
        /// </summary>
        private readonly int _capacityProjectionDays;
        /// <summary>
        /// 字段：_capacityGrowthAlertRows。
        /// </summary>
        private readonly int _capacityGrowthAlertRows;
        /// <summary>
        /// 热点分层建议阈值（行数）。
        /// </summary>
        private readonly int _capacityHotLayeringRows;
        /// <summary>
        /// 字段：_configuredSlowQueryThresholdMilliseconds。
        /// </summary>
        private readonly int _configuredSlowQueryThresholdMilliseconds;
        /// <summary>
        /// 慢查询触发次数配置。
        /// </summary>
        private readonly int _configuredTriggerCount;
        /// <summary>
        /// 字段：_configuredMaxSuggestionsPerCycle。
        /// </summary>
        private readonly int _configuredMaxSuggestionsPerCycle;
        /// <summary>
        /// 告警 P99 阈值配置（毫秒）。
        /// </summary>
        private readonly int _configuredAlertP99Milliseconds;
        /// <summary>
        /// 字段：_configuredAlertTimeoutRatePercent。
        /// </summary>
        private readonly decimal _configuredAlertTimeoutRatePercent;
        /// <summary>
        /// 告警死锁阈值配置（计数）。
        /// </summary>
        private readonly int _configuredAlertDeadlockCount;
        /// <summary>
        /// 字段：_severeRollbackP99IncreasePercent。
        /// </summary>
        private readonly decimal _severeRollbackP99IncreasePercent;
        /// <summary>
        /// 严重回归触发回滚的超时率阈值（百分比）。
        /// </summary>
        private readonly decimal _severeRollbackTimeoutIncreasePercent;
        /// <summary>
        /// 字段：_pauseActionCyclesOnRegression。
        /// </summary>
        private readonly int _pauseActionCyclesOnRegression;
        /// <summary>
        /// 执行计划探针开关。
        /// </summary>
        private readonly bool _enablePlanProbe;
        /// <summary>
        /// 字段：_planProbeSampleRate。
        /// </summary>
        private readonly decimal _planProbeSampleRate;
        /// <summary>
        /// 数据库连接池最大连接数告警阈值（来自 ResourceThresholds 配置）。
        /// </summary>
        private readonly int _resourceMaxConnectionPoolSize;
        /// <summary>
        /// 进程内存告警阈值（MB，来自 ResourceThresholds 配置）。
        /// </summary>
        private readonly int _resourceMemoryWarningThresholdMB;
        /// <summary>
        /// 表级热度计数（用于策略评估）。
        /// </summary>
        private readonly Dictionary<string, int> _tableHeatByTable = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 表级容量快照序列（用于容量趋势预测）。
        /// </summary>
        private readonly Dictionary<string, Queue<TableCapacitySnapshot>> _capacitySnapshotsByTable = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 字段：_modelIndexColumnsByTable。
        /// </summary>
        private readonly Dictionary<string, IReadOnlyList<string[]>> _modelIndexColumnsByTable = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 字段：_analysisCycleCounter。
        /// </summary>
        private int _analysisCycleCounter;
        /// <summary>
        /// 暂停自动执行截止周期（含）。
        /// </summary>
        private int _pauseExecutionUntilCycle;
        /// <summary>
        /// 字段：_currentStage。
        /// </summary>
        private AutoTuningClosedLoopStage _currentStage = AutoTuningClosedLoopStage.Monitor;
        /// <summary>
        /// 闭环阶段追踪器，用于审计阶段迁移链路。
        /// </summary>
        private readonly AutoTuningClosedLoopTracker _closedLoopTracker = new();
        /// <summary>
        /// 月报周期：累计慢查询采样窗口次数。
        /// </summary>
        private int _monthlyAnalysisCycles;
        /// <summary>
        /// 月报周期：累计已尝试执行的自动调优动作数。
        /// </summary>
        private int _monthlyActionsAttempted;
        /// <summary>
        /// 月报周期：累计成功执行的自动调优动作数。
        /// </summary>
        private int _monthlyActionsSucceeded;
        /// <summary>
        /// 月报周期：累计执行失败的自动调优动作数。
        /// </summary>
        private int _monthlyActionsFailed;
        /// <summary>
        /// 月报周期：累计触发回滚的次数。
        /// </summary>
        private int _monthlyRollbackCount;
        /// <summary>
        /// 月报周期：累计告警次数。
        /// </summary>
        private int _monthlyAlertCount;

        /// <summary>初始化数据库自动调谐后台服务及其运行策略参数。</summary>
        public DatabaseAutoTuningHostedService(
            IAutoTuningObservability observability,
            IExecutionPlanRegressionProbe planRegressionProbe,
            IServiceScopeFactory scopeFactory,
            IDatabaseDialect dialect,
            SlowQueryAutoTuningPipeline pipeline,
            IConfiguration configuration) {
            _observability = observability;
            _planRegressionProbe = planRegressionProbe;
            _scopeFactory = scopeFactory;
            _dialect = dialect;
            _pipeline = pipeline;
            _analyzeIntervalSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AnalyzeIntervalSeconds"), 30);
            _maxExecuteActionsPerCycle = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:MaxExecuteActionsPerCycle"), 2);
            _actionExecutionTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:ActionExecutionTimeoutSeconds"), 60);
            _skipExecutionDuringPeak = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:SkipExecutionDuringPeak"), true);
            _peakStartTime = AutoTuningConfigurationHelper.GetTimeOfDayOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:PeakStartLocalTime"), new TimeSpan(8, 0, 0));
            _peakEndTime = AutoTuningConfigurationHelper.GetTimeOfDayOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:PeakEndLocalTime"), new TimeSpan(21, 0, 0));
            _enableDangerousActionIsolator = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:Isolator:EnableGuard"), true);
            _allowDangerousActionExecution = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:Isolator:AllowDangerousActionExecution"), false);
            _enableActionDryRun = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:Isolator:DryRun"), false);
            _enableAutoRollback = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:EnableAutoRollback"), true);
            _whitelistedTables = LoadWhitelistedTables(configuration.GetSection(AutoTuningConfigurationHelper.BuildAutonomousKey("Execution:WhitelistedTables")));
            if (_whitelistedTables.Count == 0) {
                NLogLogger.Warn("自动调优执行白名单为空：当前将阻断所有候选表自动动作。");
            }
            _baselineCommandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("BaselineCommandTimeoutSeconds"), 30);
            _baselineMaxRetryCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("BaselineMaxRetryCount"), 5);
            _baselineMaxRetryDelaySeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("BaselineMaxRetryDelaySeconds"), 10);
            _configuredCommandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, $"{PerformanceConfigPrefix}:CommandTimeoutSeconds", 30);
            _configuredMaxRetryCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, $"{PerformanceConfigPrefix}:MaxRetryCount", 5);
            _configuredMaxRetryDelaySeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, $"{PerformanceConfigPrefix}:MaxRetryDelaySeconds", 10);
            _configuredSlowQueryThresholdMilliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("SlowQueryThresholdMilliseconds"), 500);
            _configuredTriggerCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("TriggerCount"), 3);
            _configuredMaxSuggestionsPerCycle = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("MaxActionsPerCycle"), 3);
            _configuredAlertP99Milliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertP99Milliseconds"), 500);
            _configuredAlertTimeoutRatePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertTimeoutRatePercent"), 1m);
            _configuredAlertDeadlockCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertDeadlockCount"), 1);
            _enableFullAutomation = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("EnableFullAutomation"), true);
            _policyMinTableHeatCalls = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Policy:MinTableHeatCalls"), 10);
            _policyMaxRiskScore = AutoTuningConfigurationHelper.GetDecimalInRangeOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Policy:MaxRiskScore"), 0.85m, 0m, 1m);
            _policyPeakMaxRiskScore = AutoTuningConfigurationHelper.GetDecimalInRangeOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Policy:PeakMaxRiskScore"), 0.45m, 0m, 1m);
            _policyPeakMaxTableHeatCalls = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Policy:PeakMaxTableHeatCalls"), 50);
            _enableAutoValidation = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:EnableAutoValidation"), true);
            _validationDelayCycles = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:DelayCycles"), 1);
            _validationP95IncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:P95IncreasePercent"), 5m);
            _validationP99IncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:P99IncreasePercent"), 10m);
            _validationErrorRateIncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:ErrorRateIncreasePercent"), 0.5m);
            _validationTimeoutRateIncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:TimeoutRateIncreasePercent"), 0.5m);
            _validationDeadlockIncreaseCount = AutoTuningConfigurationHelper.GetNonNegativeIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:DeadlockIncreaseCount"), 1);
            _enableCapacityPrediction = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("CapacityPrediction:EnableCapacityPrediction"), true);
            _capacityProjectionDays = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("CapacityPrediction:ProjectionDays"), 7);
            _capacityGrowthAlertRows = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("CapacityPrediction:GrowthAlertRows"), 50000);
            _capacityHotLayeringRows = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("CapacityPrediction:HotLayeringRows"), 200000);
            _severeRollbackP99IncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:SevereRollback:P99IncreasePercent"), 25m);
            _severeRollbackTimeoutIncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:SevereRollback:TimeoutRateIncreasePercent"), 2m);
            _pauseActionCyclesOnRegression = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:PauseActionCyclesOnRegression"), 2);
            _enablePlanProbe = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:PlanProbe:Enable"), true);
            _planProbeSampleRate = AutoTuningConfigurationHelper.GetDecimalClampedOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:PlanProbe:SampleRate"), 1m, 0m, 1m);
            _resourceMaxConnectionPoolSize = Math.Max(1, configuration.GetValue<int>("ResourceThresholds:MaxConnectionPoolSize", 100));
            _resourceMemoryWarningThresholdMB = Math.Max(0, configuration.GetValue<int>("ResourceThresholds:MemoryWarningThresholdMB", 1024));
        }

        /// <summary>
        /// 兼容历史测试调用的构造函数（第一参数仅用于保持现有测试的签名兼容，不参与日志输出）。
        /// 生产代码请使用无 legacyLogger 参数的主构造函数。
        /// </summary>
        /// <param name="legacyLogger">历史签名兼容占位参数（不使用，仅保持现有测试可编译）。</param>
        /// <param name="observability">自动调优可观测输出器。</param>
        /// <param name="planRegressionProbe">执行计划回退探针。</param>
        /// <param name="scopeFactory">DI 作用域工厂。</param>
        /// <param name="dialect">数据库方言。</param>
        /// <param name="pipeline">慢查询分析管道。</param>
        /// <param name="configuration">应用配置。</param>
        public DatabaseAutoTuningHostedService(
            object legacyLogger,
            IAutoTuningObservability observability,
            IExecutionPlanRegressionProbe planRegressionProbe,
            IServiceScopeFactory scopeFactory,
            IDatabaseDialect dialect,
            SlowQueryAutoTuningPipeline pipeline,
            IConfiguration configuration)
            : this(observability, planRegressionProbe, scopeFactory, dialect, pipeline, configuration) {
            ArgumentNullException.ThrowIfNull(legacyLogger);
        }

        /// <summary>后台循环：按固定周期分析慢 SQL，并执行自治策略/验证/清理。</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            AuditBaseline();

            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_analyzeIntervalSeconds), stoppingToken);
                MoveToStage(AutoTuningClosedLoopStage.Monitor, "analysis-cycle-start");

                var result = _pipeline.Analyze(_dialect);
                if (result.Metrics.Count == 0) {
                    if (result.ShouldEmitDailyReport) {
                        EmitDailyReport(result);
                    }
                    if (result.ShouldEmitMonthlyReport) {
                        EmitMonthlyReport(result);
                    }
                    continue;
                }
                result = await ApplyIndexSuggestionGuardsAsync(result, stoppingToken);
                _analysisCycleCounter++;
                MoveToStage(AutoTuningClosedLoopStage.Diagnose, "metrics-aggregated");

                NLogLogger.Info(
                    "慢 SQL 分析窗口完成，Provider={Provider}, GeneratedTime={GeneratedTime}, Groups={Groups}, DroppedSamples={DroppedSamples}",
                    _dialect.ProviderName,
                    result.GeneratedTime,
                    result.Metrics.Count,
                    result.DroppedSamples);
                _observability.EmitMetric("autotuning.analysis.group_count", result.Metrics.Count);

                foreach (var metric in result.Metrics) {
                    NLogLogger.Info(
                        "慢 SQL 聚合：Provider={Provider}, Fingerprint={Fingerprint}, Calls={Calls}, Rows={Rows}, P95Ms={P95Ms}, P99Ms={P99Ms}, MaxMs={MaxMs}, ErrorRatePercent={ErrorRatePercent}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                        _dialect.ProviderName,
                        metric.SqlFingerprint,
                        metric.CallCount,
                        metric.TotalAffectedRows,
                        metric.P95Milliseconds,
                        metric.P99Milliseconds,
                        metric.MaxMilliseconds,
                        metric.ErrorRatePercent,
                        metric.TimeoutRatePercent,
                        metric.DeadlockCount);
                }

                foreach (var alert in result.Alerts) {
                    NLogLogger.Warn("慢 SQL 阈值告警：Provider={Provider}, Alert={Alert}", _dialect.ProviderName, alert);
                }
                foreach (var recovery in result.RecoveryNotifications) {
                    NLogLogger.Info("慢 SQL 告警恢复：Provider={Provider}, Recovery={Recovery}", _dialect.ProviderName, recovery);
                }

                var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
                UpdateAutonomousSignals(result, result.GeneratedTime);
                MoveToStage(AutoTuningClosedLoopStage.Execute, "execute-candidates");
                await ExecuteAutoTuningActionsAsync(result, metricsByFingerprint, stoppingToken);
                MoveToStage(AutoTuningClosedLoopStage.Verify, "validate-actions");
                await ValidateAutonomousActionsAsync(result, metricsByFingerprint, stoppingToken);
                PruneTrackingState();

                // 累计月报统计
                _monthlyAnalysisCycles++;
                _monthlyAlertCount += result.Alerts.Count;

                if (result.ShouldEmitDailyReport) {
                    EmitDailyReport(result);
                }

                if (result.ShouldEmitMonthlyReport) {
                    EmitMonthlyReport(result);
                }
            }
        }

        /// <summary>输出每日慢 SQL 汇总报告与只读建议。</summary>
        private void EmitDailyReport(SlowQueryAnalysisResult result) {
            NLogLogger.Info(
                "每日慢 SQL 报告：Provider={Provider}, GeneratedTime={GeneratedTime}, TopCount={TopCount}, DroppedSamples={DroppedSamples}",
                _dialect.ProviderName,
                result.GeneratedTime,
                result.Metrics.Count,
                result.DroppedSamples);

            if (result.Metrics.Count == 0) {
                NLogLogger.Info("每日慢 SQL 报告：Provider={Provider}, 当前报告周期无慢 SQL 样本。", _dialect.ProviderName);
            }

            foreach (var metric in result.Metrics) {
                NLogLogger.Info(
                    "每日慢 SQL Top（按 P99/P95 排序）：Fingerprint={Fingerprint}, Calls={Calls}, Rows={Rows}, P95Ms={P95Ms}, P99Ms={P99Ms}, ErrorRatePercent={ErrorRatePercent}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                    metric.SqlFingerprint,
                    metric.CallCount,
                    metric.TotalAffectedRows,
                    metric.P95Milliseconds,
                    metric.P99Milliseconds,
                    metric.ErrorRatePercent,
                    metric.TimeoutRatePercent,
                    metric.DeadlockCount);
            }

            foreach (var suggestion in result.SuggestionInsights) {
                NLogLogger.Info(
                    "每日慢 SQL 索引建议（只读，不自动执行，需人工确认）：Provider={Provider}, Fingerprint={Fingerprint}, Suggestion={Suggestion}, Reason={Reason}, RiskLevel={RiskLevel}, Confidence={Confidence:F2}",
                    _dialect.ProviderName,
                    suggestion.SqlFingerprint,
                    suggestion.SuggestionSql,
                    suggestion.Reason,
                    suggestion.RiskLevel,
                    suggestion.Confidence);
            }
        }

        /// <summary>
        /// 输出月度巡检报告，覆盖稳定性、治理动作成功率、告警总量与回滚次数。
        /// 输出完成后重置本月累计计数器。
        /// </summary>
        private void EmitMonthlyReport(SlowQueryAnalysisResult result) {
            // 无动作尝试时成功率默认为 100%（无失败即满分，符合无操作无风险语义）
            const double FullSuccessRatePercent = 100d;
            var analysisSuccessRate = _monthlyActionsAttempted > 0
                ? (double)_monthlyActionsSucceeded / _monthlyActionsAttempted * 100
                : FullSuccessRatePercent;
            NLogLogger.Info(
                "月度巡检报告：Provider={Provider}, GeneratedTime={GeneratedTime}, AnalysisCycles={AnalysisCycles}, ActionsAttempted={ActionsAttempted}, ActionsSucceeded={ActionsSucceeded}, ActionsFailed={ActionsFailed}, ActionSuccessRate={ActionSuccessRate:F1}%, RollbackCount={RollbackCount}, AlertCount={AlertCount}, ActiveHotTables={ActiveHotTables}",
                _dialect.ProviderName,
                result.GeneratedTime,
                _monthlyAnalysisCycles,
                _monthlyActionsAttempted,
                _monthlyActionsSucceeded,
                _monthlyActionsFailed,
                analysisSuccessRate,
                _monthlyRollbackCount,
                _monthlyAlertCount,
                _tableHeatByTable.Count(static pair => pair.Value > 0));

            if (result.Metrics.Count > 0) {
                NLogLogger.Info(
                    "月度巡检报告 - 慢 SQL Top（本次快照）：Provider={Provider}, TopCount={TopCount}",
                    _dialect.ProviderName,
                    result.Metrics.Count);
            }

            // 输出月报可观测性指标
            _observability.EmitMetric("autotuning.monthly.actions_attempted", _monthlyActionsAttempted);
            _observability.EmitMetric("autotuning.monthly.actions_succeeded", _monthlyActionsSucceeded);
            _observability.EmitMetric("autotuning.monthly.actions_failed", _monthlyActionsFailed);
            _observability.EmitMetric("autotuning.monthly.rollback_count", _monthlyRollbackCount);
            _observability.EmitMetric("autotuning.monthly.alert_count", _monthlyAlertCount);

            // 重置本月累计计数器
            _monthlyAnalysisCycles = 0;
            _monthlyActionsAttempted = 0;
            _monthlyActionsSucceeded = 0;
            _monthlyActionsFailed = 0;
            _monthlyRollbackCount = 0;
            _monthlyAlertCount = 0;
        }


        private async Task ExecuteAutoTuningActionsAsync(
            SlowQueryAnalysisResult result,
            IReadOnlyDictionary<string, SlowQueryMetric> metricsByFingerprint,
            CancellationToken cancellationToken) {
            // 步骤 1：先判断总开关与执行窗口。
            if (!_enableFullAutomation) {
                return;
            }
            if (_analysisCycleCounter < _pauseExecutionUntilCycle) {
                NLogLogger.Warn(
                    "闭环自治执行暂停中：Provider={Provider}, CurrentCycle={CurrentCycle}, ResumeCycle={ResumeCycle}",
                    _dialect.ProviderName,
                    _analysisCycleCounter,
                    _pauseExecutionUntilCycle);
                return;
            }

            var now = DateTime.Now;
            var inPeakWindow = IsInPeakWindow(now.TimeOfDay);
            if (_skipExecutionDuringPeak && inPeakWindow) {
                NLogLogger.Info("自动调优执行已跳过：当前处于高峰时段，仅采集不变更。Provider={Provider}", _dialect.ProviderName);
                return;
            }

            // 步骤 2：逐候选执行（含白名单、风险评分、动作上限）。
            var executedCount = 0;
            foreach (var candidate in result.TuningCandidates) {
                if (executedCount >= _maxExecuteActionsPerCycle) {
                    break;
                }

                var tableKey = BuildTableKey(candidate.SchemaName, candidate.TableName);
                if (!IsWhitelisted(candidate.SchemaName, candidate.TableName)) {
                    NLogLogger.Info(
                        "自动调优白名单拦截：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}",
                        _dialect.ProviderName,
                        candidate.SqlFingerprint,
                        tableKey);
                    continue;
                }

                metricsByFingerprint.TryGetValue(candidate.SqlFingerprint, out var currentMetric);
                var tableHeat = _tableHeatByTable.TryGetValue(tableKey, out var heat) ? heat : 0;
                foreach (var actionSql in candidate.SuggestedActions) {
                    if (executedCount >= _maxExecuteActionsPerCycle) {
                        break;
                    }

                    var policyDecision = EvaluateExecutionPolicy(actionSql, currentMetric, tableHeat, inPeakWindow);
                    if (!policyDecision.ShouldExecute) {
                        NLogLogger.Info(
                            "闭环自治策略引擎拦截自动动作：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, RiskScore={RiskScore:F2}, Reason={Reason}",
                            _dialect.ProviderName,
                            candidate.SqlFingerprint,
                            tableKey,
                            policyDecision.RiskScore,
                            policyDecision.Reason);
                        continue;
                    }

                    var rollbackSql = BuildRollbackSql(actionSql);
                    var actionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                    if (!await ExecuteThroughIsolatorAsync(
                            actionId,
                            candidate,
                            actionSql,
                            rollbackSql,
                            reason: $"execute-action, risk={policyDecision.RiskScore:F2}",
                            isRollback: false,
                            cancellationToken)) {
                        continue;
                    }
                    executedCount++;

                    // 步骤 3：登记可回滚动作，并执行自治维护 SQL。
                    if (!string.IsNullOrWhiteSpace(rollbackSql)) {
                        _pendingRollbackByFingerprint[candidate.SqlFingerprint] = new PendingRollbackAction(
                            ActionId: actionId,
                            Fingerprint: candidate.SqlFingerprint,
                            RollbackSql: rollbackSql,
                            TableKey: tableKey,
                            CreatedTime: now,
                            CreatedCycle: _analysisCycleCounter,
                            BaselineP95Milliseconds: currentMetric?.P95Milliseconds ?? 0d,
                            BaselineP99Milliseconds: currentMetric?.P99Milliseconds ?? 0d,
                            BaselineErrorRatePercent: currentMetric?.ErrorRatePercent ?? 0m,
                            BaselineTimeoutRatePercent: currentMetric?.TimeoutRatePercent ?? 0m,
                            BaselineDeadlockCount: currentMetric?.DeadlockCount ?? 0,
                            BaselineLockWaitCount: currentMetric?.LockWaitCount);
                    }

                    var maintenanceSql = _dialect.BuildAutonomousMaintenanceSql(
                        candidate.SchemaName,
                        candidate.TableName,
                        inPeakWindow,
                        policyDecision.RiskScore > _policyMaxRiskScore);
                    foreach (var maintenance in maintenanceSql) {
                        if (!await ExecuteThroughIsolatorAsync(
                                $"{actionId}-maintenance",
                                candidate,
                                maintenance,
                                rollbackSql: null,
                                reason: "autonomous-maintenance",
                                isRollback: false,
                                cancellationToken)) {
                            continue;
                        }
                    }
                }
            }
        }

        /// <summary>对已执行动作执行延迟验证，必要时触发自动回滚。</summary>
        private async Task ValidateAutonomousActionsAsync(
            SlowQueryAnalysisResult result,
            IReadOnlyDictionary<string, SlowQueryMetric> metricsByFingerprint,
            CancellationToken cancellationToken) {
            // 步骤 1：检查验证开关与待验证池。
            if (!_enableFullAutomation || !_enableAutoValidation || _pendingRollbackByFingerprint.Count == 0) {
                return;
            }

            // 步骤 2：计算回归指标并判定是否退化。
            foreach (var rollback in _pendingRollbackByFingerprint.Values.ToArray()) {
                if (_analysisCycleCounter - rollback.CreatedCycle < _validationDelayCycles) {
                    continue;
                }

                if (!metricsByFingerprint.TryGetValue(rollback.Fingerprint, out var currentMetric)) {
                    var unavailableReason = AutoTuningUnavailableReason.MetricWindowMiss;
                    var evidenceForUnavailable = BuildEvidenceContext(rollback.ActionId, rollback.Fingerprint);
                    _observability.EmitMetric(
                        "autotuning.validation.metric_unavailable",
                        1d,
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = unavailableReason.ToTagValue(),
                            ["evidence_id"] = evidenceForUnavailable.EvidenceId,
                            ["correlation_id"] = evidenceForUnavailable.CorrelationId
                        });
                    _observability.EmitEvent(
                        "autotuning.validation.metric_unavailable",
                        NLog.LogLevel.Warn,
                        $"validation metric unavailable: {rollback.Fingerprint}",
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = unavailableReason.ToTagValue(),
                            ["evidence_id"] = evidenceForUnavailable.EvidenceId,
                            ["correlation_id"] = evidenceForUnavailable.CorrelationId
                        });
                    NLogLogger.Warn(
                        "闭环自治自动验证跳过：Provider={Provider}, Stage={Stage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        AutoTuningClosedLoopStage.Verify,
                        rollback.ActionId,
                        rollback.Fingerprint,
                        unavailableReason.ToTagValue(),
                        evidenceForUnavailable.EvidenceId,
                        evidenceForUnavailable.CorrelationId);
                    continue;
                }

                var p95IncreasePercent = CalculateIncreasePercent(rollback.BaselineP95Milliseconds, currentMetric.P95Milliseconds);
                var p99IncreasePercent = CalculateIncreasePercent(rollback.BaselineP99Milliseconds, currentMetric.P99Milliseconds);
                var errorRateIncrease = currentMetric.ErrorRatePercent - rollback.BaselineErrorRatePercent;
                var timeoutRateIncrease = currentMetric.TimeoutRatePercent - rollback.BaselineTimeoutRatePercent;
                var deadlockIncrease = currentMetric.DeadlockCount - rollback.BaselineDeadlockCount;
                var lockWaitUnavailable = !rollback.BaselineLockWaitCount.HasValue || !currentMetric.LockWaitCount.HasValue;
                var lockWaitUnavailableReason = DetermineLockWaitUnavailableReason(rollback.BaselineLockWaitCount, currentMetric.LockWaitCount);
                var lockWaitStatus = lockWaitUnavailable ? "unavailable" : "available";
                var planRegression = EvaluatePlanRegression(rollback);
                var evidence = BuildEvidenceContext(rollback.ActionId, rollback.Fingerprint);

                var regressed = (_validationP95IncreasePercent > 0m && p95IncreasePercent >= _validationP95IncreasePercent)
                    || (_validationP99IncreasePercent > 0m && p99IncreasePercent >= _validationP99IncreasePercent)
                    || (_validationErrorRateIncreasePercent > 0m && errorRateIncrease >= _validationErrorRateIncreasePercent)
                    || (_validationTimeoutRateIncreasePercent > 0m && timeoutRateIncrease >= _validationTimeoutRateIncreasePercent)
                    || (_validationDeadlockIncreaseCount > 0 && deadlockIncrease >= _validationDeadlockIncreaseCount)
                    || (planRegression.IsAvailable && planRegression.IsRegressed);
                var regressionDecision = regressed
                    ? AutoRollbackDecisionEngine.Evaluate(
                        p99IncreasePercent,
                        timeoutRateIncrease,
                        lockWaitStatus,
                        _severeRollbackP99IncreasePercent,
                        _severeRollbackTimeoutIncreasePercent,
                        regressed,
                        "threshold-regression-detected")
                    : new RegressionEvaluationResult(
                        IsRegressed: false,
                        IsSevereRegression: false,
                        Reason: "validation-pass",
                        LockWaitStatus: lockWaitStatus);
                var verificationResult = AutoTuningVerificationResultBuilder.Build(
                    regressed: regressed,
                    severeRegressed: regressionDecision.IsSevereRegression,
                    reason: regressed ? regressionDecision.Reason : "validation-pass",
                    p95IncreasePercent: p95IncreasePercent,
                    p99IncreasePercent: p99IncreasePercent,
                    errorRateIncreasePercent: errorRateIncrease,
                    timeoutRateIncreasePercent: timeoutRateIncrease,
                    deadlockIncreaseCount: deadlockIncrease,
                    lockWaitBaseline: rollback.BaselineLockWaitCount,
                    lockWaitCurrent: currentMetric.LockWaitCount,
                    planRegression: planRegression,
                    lockWaitUnavailable: lockWaitUnavailable,
                    lockWaitUnavailableReason: lockWaitUnavailableReason);
                EmitValidationResult(rollback, verificationResult);

                if (!regressed) {
                    _pendingRollbackByFingerprint.Remove(rollback.Fingerprint);
                    NLogLogger.Info(
                        "闭环自治自动验证通过：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, P95Increase={P95Increase:F2}, P99Increase={P99Increase:F2}, ErrorRateIncrease={ErrorRateIncrease:F2}, TimeoutRateIncrease={TimeoutRateIncrease:F2}, DeadlockIncrease={DeadlockIncrease}, LockWaitStatus={LockWaitStatus}, PlanRegressionSummary={PlanRegressionSummary}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        rollback.TableKey,
                        p95IncreasePercent,
                        p99IncreasePercent,
                        errorRateIncrease,
                        timeoutRateIncrease,
                        deadlockIncrease,
                        lockWaitStatus,
                        planRegression.Summary,
                        evidence.EvidenceId,
                        evidence.CorrelationId);
                    continue;
                }

                var severeRegression = regressionDecision.IsSevereRegression;
                NLogLogger.Warn(
                    "闭环自治自动验证失败：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, P95Increase={P95Increase:F2}, P99Increase={P99Increase:F2}, ErrorRateIncrease={ErrorRateIncrease:F2}, TimeoutRateIncrease={TimeoutRateIncrease:F2}, DeadlockIncrease={DeadlockIncrease}, LockWaitStatus={LockWaitStatus}, PlanRegressionSummary={PlanRegressionSummary}, SevereRegression={SevereRegression}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                    _dialect.ProviderName,
                    rollback.Fingerprint,
                    rollback.TableKey,
                    p95IncreasePercent,
                    p99IncreasePercent,
                    errorRateIncrease,
                    timeoutRateIncrease,
                    deadlockIncrease,
                    regressionDecision.LockWaitStatus,
                    planRegression.Summary,
                    severeRegression,
                    evidence.EvidenceId,
                    evidence.CorrelationId);

                // 步骤 3：分级动作（暂停后续动作 / 立即回滚）。
                if (!severeRegression) {
                    _pauseExecutionUntilCycle = Math.Max(_pauseExecutionUntilCycle, _analysisCycleCounter + _pauseActionCyclesOnRegression);
                    NLogLogger.Warn(
                        "闭环自治触发保护暂停：Provider={Provider}, Fingerprint={Fingerprint}, PauseUntilCycle={PauseUntilCycle}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        _pauseExecutionUntilCycle);
                }

                // 步骤 4：在允许条件下执行回滚并移除追踪记录。
                if (_enableAutoRollback) {
                    var triggerReason = severeRegression ? "validation-severe-rollback-triggered" : "validation-regression-rollback-triggered";
                    _observability.EmitMetric(
                        "autotuning.validation.rollback_triggered",
                        1d,
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = triggerReason,
                            ["evidence_id"] = evidence.EvidenceId,
                            ["correlation_id"] = evidence.CorrelationId
                        });
                    _observability.EmitEvent(
                        "autotuning.validation.rollback_triggered",
                        NLog.LogLevel.Warn,
                        $"rollback triggered: {rollback.Fingerprint}",
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = triggerReason,
                            ["evidence_id"] = evidence.EvidenceId,
                            ["correlation_id"] = evidence.CorrelationId
                        });
                    NLogLogger.Warn(
                        "闭环自治自动验证触发回滚：Provider={Provider}, Stage={Stage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        AutoTuningClosedLoopStage.Verify,
                        rollback.ActionId,
                        rollback.Fingerprint,
                        triggerReason,
                        evidence.EvidenceId,
                        evidence.CorrelationId);
                }

                if (_enableAutoRollback && await ExecuteThroughIsolatorAsync(
                        rollback.ActionId,
                        new SlowQueryTuningCandidate(rollback.Fingerprint, null, rollback.TableKey, Array.Empty<string>(), Array.Empty<string>()),
                        rollback.RollbackSql,
                        rollbackSql: null,
                        reason: severeRegression ? "severe-regression-immediate-rollback" : "regression-rollback",
                        isRollback: true,
                        cancellationToken)) {
                    MoveToStage(AutoTuningClosedLoopStage.Rollback, "validation-rollback-executed", rollback.ActionId, rollback.Fingerprint);
                    NLogLogger.Warn(
                        "闭环自治自动验证回滚执行完成：Provider={Provider}, Fingerprint={Fingerprint}, ActionId={ActionId}, RollbackSql={RollbackSql}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        rollback.ActionId,
                        rollback.RollbackSql,
                        evidence.EvidenceId,
                        evidence.CorrelationId);
                }

                _pendingRollbackByFingerprint.Remove(rollback.Fingerprint);
            }
        }

        /// <summary>统一危险动作隔离入口（开关、dry-run、审计、执行）。</summary>
        private async Task<bool> ExecuteThroughIsolatorAsync(
            string actionId,
            SlowQueryTuningCandidate candidate,
            string actionSql,
            string? rollbackSql,
            string reason,
            bool isRollback,
            CancellationToken cancellationToken) {
            var dangerous = IsDangerousAction(actionSql);
            var isolationDecision = ActionIsolationPolicy.Evaluate(
                _enableDangerousActionIsolator,
                _allowDangerousActionExecution,
                _enableActionDryRun,
                dangerous,
                isRollback);
            if (isolationDecision == ActionIsolationDecision.BlockedByGuard) {
                EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: false, dryRun: false, reason: $"{reason}, blocked-by-guard");
                NLogLogger.Warn(
                    "危险动作隔离器拦截动作：Provider={Provider}, ActionId={ActionId}, Fingerprint={Fingerprint}, Sql={Sql}",
                    _dialect.ProviderName,
                    actionId,
                    candidate.SqlFingerprint,
                    actionSql);
                return false;
            }

            if (isolationDecision == ActionIsolationDecision.DryRunOnly) {
                EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: false, dryRun: true, reason: $"{reason}, dry-run");
                _observability.EmitEvent(
                    "autotuning.isolator.dry_run",
                    NLog.LogLevel.Info,
                    $"Dry-run blocked SQL execution: {candidate.SqlFingerprint}",
                    new Dictionary<string, string> {
                        ["provider"] = _dialect.ProviderName,
                        ["action_id"] = actionId
                    });
                return false;
            }

            var executed = await TryExecuteSqlAsync(actionSql, candidate.SqlFingerprint, isRollback, cancellationToken);
            EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: executed, dryRun: false, reason: reason);
            if (!isRollback) {
                _monthlyActionsAttempted++;
            }
            if (executed) {
                _observability.EmitMetric("autotuning.action.executed", 1d, new Dictionary<string, string> {
                    ["provider"] = _dialect.ProviderName,
                    ["is_rollback"] = isRollback ? "true" : "false"
                });
                if (isRollback) {
                    _monthlyRollbackCount++;
                }
                else {
                    _monthlyActionsSucceeded++;
                }
            }
            else if (!isRollback) {
                _monthlyActionsFailed++;
            }

            return executed;
        }

        /// <summary>在独立作用域中执行单条 SQL。</summary>
        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken) {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
            dbContext.Database.SetCommandTimeout(_actionExecutionTimeoutSeconds);
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        /// <summary>包装 SQL 执行并处理可忽略异常。</summary>
        private async Task<bool> TryExecuteSqlAsync(string sql, string fingerprint, bool isRollback, CancellationToken cancellationToken) {
            try {
                await ExecuteSqlAsync(sql, cancellationToken);
                return true;
            } catch (Exception ex) when (_dialect.ShouldIgnoreAutoTuningException(ex)) {
                NLogLogger.Warn(
                    ex,
                    "自动调优 SQL 已忽略异常：Provider={Provider}, Fingerprint={Fingerprint}, IsRollback={IsRollback}, Sql={Sql}",
                    _dialect.ProviderName,
                    fingerprint,
                    isRollback,
                    sql);
                return false;
            } catch (Exception ex) {
                NLogLogger.Error(
                    ex,
                    "自动调优 SQL 执行失败：Provider={Provider}, Fingerprint={Fingerprint}, IsRollback={IsRollback}, Sql={Sql}",
                    _dialect.ProviderName,
                    fingerprint,
                    isRollback,
                    sql);
                return false;
            }
        }

        /// <summary>计算百分比增幅（仅统计正向增长）。</summary>
        private static decimal CalculateIncreasePercent(double previousValue, double currentValue) {
            if (previousValue <= 0d) {
                return currentValue > 0d ? 100m : 0m;
            }

            var increase = currentValue - previousValue;
            if (increase <= 0d) {
                return 0m;
            }

            return (decimal)(increase / previousValue * 100d);
        }

        /// <summary>
        /// 执行逻辑：DetermineLockWaitUnavailableReason。
        /// </summary>
        private static AutoTuningUnavailableReason DetermineLockWaitUnavailableReason(int? baselineLockWaitCount, int? currentLockWaitCount) {
            if (!baselineLockWaitCount.HasValue && !currentLockWaitCount.HasValue) {
                return AutoTuningUnavailableReason.BaselineAndCurrentUnavailable;
            }

            if (!baselineLockWaitCount.HasValue) {
                return AutoTuningUnavailableReason.BaselineUnavailable;
            }

            if (!currentLockWaitCount.HasValue) {
                return AutoTuningUnavailableReason.CurrentUnavailable;
            }

            return AutoTuningUnavailableReason.Sampled;
        }

        /// <summary>输出自动调优动作审计日志。</summary>
        private void EmitAuditLog(
            string actionId,
            SlowQueryTuningCandidate candidate,
            string actionSql,
            string? rollbackSql,
            bool executed,
            bool dryRun,
            string reason) {
            NLogLogger.Info(
                "自动调优变更审计：Provider={Provider}, ActionId={ActionId}, Fingerprint={Fingerprint}, Table={Table}, Executed={Executed}, DryRun={DryRun}, Reason={Reason}, ActionSql={ActionSql}, RollbackSql={RollbackSql}",
                _dialect.ProviderName,
                actionId,
                candidate.SqlFingerprint,
                BuildTableKey(candidate.SchemaName, candidate.TableName),
                executed,
                dryRun,
                reason,
                actionSql,
                rollbackSql ?? string.Empty);
        }

        /// <summary>
        /// 执行逻辑：MoveToStage。
        /// </summary>
        private void MoveToStage(AutoTuningClosedLoopStage stage, string reason, string? actionId = null, string? fingerprint = null) {
            if (_currentStage == stage) {
                return;
            }

            var previousStage = _currentStage;
            _currentStage = stage;
            _closedLoopTracker.MoveTo(stage);
            var normalizedActionId = NormalizeTagValue(actionId);
            var normalizedFingerprint = NormalizeTagValue(fingerprint);
            var evidence = BuildEvidenceContext(normalizedActionId, normalizedFingerprint, normalized: true);
            NLogLogger.Info(
                "闭环自治阶段迁移：Provider={Provider}, PreviousStage={PreviousStage}, CurrentStage={CurrentStage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                _dialect.ProviderName,
                previousStage,
                _currentStage,
                normalizedActionId,
                normalizedFingerprint,
                reason,
                evidence.EvidenceId,
                evidence.CorrelationId);
            _observability.EmitMetric(
                "autotuning.closed_loop.stage_transition",
                1d,
                new Dictionary<string, string> {
                    ["provider"] = _dialect.ProviderName,
                    ["stage"] = _currentStage.ToString(),
                    ["action_id"] = normalizedActionId,
                    ["fingerprint"] = normalizedFingerprint,
                    ["reason"] = reason,
                    ["evidence_id"] = evidence.EvidenceId,
                    ["correlation_id"] = evidence.CorrelationId
                });
            _observability.EmitEvent(
                "autotuning.closed_loop.stage_transition",
                NLog.LogLevel.Info,
                $"stage moved: {previousStage} -> {_currentStage}",
                new Dictionary<string, string> {
                    ["provider"] = _dialect.ProviderName,
                    ["stage"] = _currentStage.ToString(),
                    ["action_id"] = normalizedActionId,
                    ["fingerprint"] = normalizedFingerprint,
                    ["reason"] = reason,
                    ["evidence_id"] = evidence.EvidenceId,
                    ["correlation_id"] = evidence.CorrelationId
                });
        }

        /// <summary>
        /// 执行逻辑：EmitValidationResult。
        /// </summary>
        private void EmitValidationResult(PendingRollbackAction rollback, AutoTuningVerificationResult result) {
            var level = string.Equals(result.Verdict, "pass", StringComparison.OrdinalIgnoreCase)
                ? NLog.LogLevel.Info
                : NLog.LogLevel.Warn;
            var tags = new Dictionary<string, string> {
                ["provider"] = _dialect.ProviderName,
                ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                ["action_id"] = rollback.ActionId,
                ["fingerprint"] = rollback.Fingerprint,
                ["reason"] = result.Reason,
                ["verdict"] = result.Verdict
            };
            var evidence = BuildEvidenceContext(rollback.ActionId, rollback.Fingerprint);
            tags["evidence_id"] = evidence.EvidenceId;
            tags["correlation_id"] = evidence.CorrelationId;
            _observability.EmitMetric("autotuning.validation.result", 1d, tags);
            _observability.EmitEvent(
                "autotuning.validation.result",
                level,
                $"validation verdict: {result.Verdict}",
                tags);
            NLogLogger.Info(
                "闭环自治自动验证结果：Provider={Provider}, Stage={Stage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Verdict={Verdict}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}, SnapshotDiff={SnapshotDiff}",
                _dialect.ProviderName,
                AutoTuningClosedLoopStage.Verify,
                rollback.ActionId,
                rollback.Fingerprint,
                result.Verdict,
                result.Reason,
                evidence.EvidenceId,
                evidence.CorrelationId,
                string.Join(" | ", result.SnapshotDiff.Select(static diff => $"{diff.Name}:{diff.Status}:{diff.Delta}:{diff.Reason}")));
        }

        /// <summary>
        /// 执行逻辑：NormalizeTagValue。
        /// </summary>
        private static string NormalizeTagValue(string? value) => string.IsNullOrWhiteSpace(value) ? NotAvailableTag : value.Trim();

        /// <summary>
        /// 执行逻辑：EvaluatePlanRegression。
        /// </summary>
        private PlanRegressionSnapshot EvaluatePlanRegression(PendingRollbackAction rollback) {
            var normalizedFingerprint = NormalizeTagValue(rollback.Fingerprint);
            if (!_enablePlanProbe) {
                return BuildUnavailablePlanRegressionSnapshot(normalizedFingerprint, AutoTuningUnavailableReason.PlanProbeDisabled);
            }

            if (!ShouldSamplePlanProbe(rollback)) {
                return BuildUnavailablePlanRegressionSnapshot(normalizedFingerprint, AutoTuningUnavailableReason.PlanProbeSamplingSkipped);
            }

            return _planRegressionProbe.Evaluate(_dialect.ProviderName, rollback.Fingerprint);
        }

        /// <summary>
        /// 执行逻辑：ShouldSamplePlanProbe。
        /// </summary>
        private bool ShouldSamplePlanProbe(PendingRollbackAction rollback) {
            if (_planProbeSampleRate <= 0m) {
                return false;
            }

            if (_planProbeSampleRate >= 1m) {
                return true;
            }

            var seed = $"{rollback.ActionId}:{rollback.Fingerprint}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var bucket = BinaryPrimitives.ReadUInt32LittleEndian(hashBytes) % 10000u;
            var threshold = (int)Math.Round((double)(_planProbeSampleRate * 10000m), MidpointRounding.AwayFromZero);
            return bucket < (uint)threshold;
        }

        /// <summary>
        /// 执行逻辑：BuildUnavailablePlanRegressionSnapshot。
        /// </summary>
        private PlanRegressionSnapshot BuildUnavailablePlanRegressionSnapshot(string fingerprint, AutoTuningUnavailableReason reason) {
            return new PlanRegressionSnapshot(
                IsAvailable: false,
                IsRegressed: false,
                Summary: $"fingerprint={fingerprint}, provider={_dialect.ProviderName}, plan regression unavailable({reason.ToTagValue()})",
                UnavailableReason: reason.ToTagValue());
        }

        /// <summary>
        /// 执行逻辑：BuildEvidenceContext。
        /// </summary>
        private static EvidenceContext BuildEvidenceContext(string? actionId, string? fingerprint, bool normalized = false) {
            var normalizedActionId = normalized ? actionId ?? NotAvailableTag : NormalizeTagValue(actionId);
            var normalizedFingerprint = normalized ? fingerprint ?? NotAvailableTag : NormalizeTagValue(fingerprint);
            var evidenceId = $"{normalizedActionId}:{normalizedFingerprint}";
            var correlationId = normalizedActionId != NotAvailableTag ? normalizedActionId : normalizedFingerprint;
            return new EvidenceContext(evidenceId, correlationId);
        }

        /// <summary>判断候选表是否命中执行白名单。</summary>
        private bool IsWhitelisted(string? schemaName, string tableName) {
            if (_whitelistedTables.Count == 0) {
                return false;
            }

            return _whitelistedTables.Contains(BuildTableKey(schemaName, tableName));
        }

        /// <summary>根据风险评分、热度与时段判定动作是否可执行。</summary>
        private PolicyDecision EvaluateExecutionPolicy(string actionSql, SlowQueryMetric? metric, int tableHeat, bool inPeakWindow) {
            var riskScore = CalculateRiskScore(actionSql, metric, inPeakWindow);
            if (tableHeat < _policyMinTableHeatCalls) {
                return PolicyDecision.Skip(riskScore, $"table heat too low: {tableHeat} < {_policyMinTableHeatCalls}");
            }

            if (inPeakWindow && tableHeat > _policyPeakMaxTableHeatCalls) {
                return PolicyDecision.Skip(riskScore, $"table heat too high in peak: {tableHeat} > {_policyPeakMaxTableHeatCalls}");
            }

            var threshold = inPeakWindow ? _policyPeakMaxRiskScore : _policyMaxRiskScore;
            if (riskScore > threshold) {
                return PolicyDecision.Skip(riskScore, $"risk score {riskScore:F2} > threshold {threshold:F2}");
            }

            return PolicyDecision.Execute(riskScore, "autonomous-policy-approved");
        }

        /// <summary>计算动作风险分（0~1）。</summary>
        private decimal CalculateRiskScore(string actionSql, SlowQueryMetric? metric, bool inPeakWindow) {
            decimal riskScore = 0m;
            if (IsDangerousAction(actionSql)) {
                riskScore += DangerousActionRiskWeight;
            }

            if (inPeakWindow) {
                riskScore += PeakWindowRiskWeight;
            }

            if (metric is not null) {
                if (metric.P99Milliseconds >= HighP99ThresholdMilliseconds) {
                    riskScore += HighP99RiskWeight;
                }

                if (metric.TimeoutRatePercent >= HighTimeoutThresholdPercent) {
                    riskScore += HighTimeoutRiskWeight;
                }

                if (metric.ErrorRatePercent >= HighErrorThresholdPercent) {
                    riskScore += HighErrorRiskWeight;
                }

                if (metric.DeadlockCount > 0) {
                    riskScore += DeadlockRiskWeight;
                }
            }

            return decimal.Clamp(riskScore, 0m, 1m);
        }

        /// <summary>按覆盖关系、重复语义和低价值规则过滤自动索引建议。</summary>
        private async Task<SlowQueryAnalysisResult> ApplyIndexSuggestionGuardsAsync(
            SlowQueryAnalysisResult result,
            CancellationToken cancellationToken) {
            await EnsureModelIndexColumnsLoadedAsync(cancellationToken);
            var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
            var blockedCreateIndexActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emittedSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filteredCandidates = new List<SlowQueryTuningCandidate>(result.TuningCandidates.Count);

            foreach (var candidate in result.TuningCandidates) {
                metricsByFingerprint.TryGetValue(candidate.SqlFingerprint, out var metric);
                var indexColumns = NormalizeCandidateColumns(candidate.WhereColumns, 3);
                var tableKey = BuildTableKey(candidate.SchemaName, candidate.TableName);
                var indexSignature = BuildIndexSignature(tableKey, indexColumns);
                var isCoveredByExisting = indexColumns.Length > 0 && IsCoveredByModelIndex(tableKey, indexColumns);
                var isSemanticDuplicate = !string.IsNullOrWhiteSpace(indexSignature) && !emittedSignatures.Add(indexSignature);
                var isLowValue = IsLowValueIndexCandidate(metric);
                var shouldFilterIndex = isCoveredByExisting || isSemanticDuplicate || isLowValue;
                if (shouldFilterIndex) {
                    var blockReason = isCoveredByExisting
                        ? "covered-by-existing-index"
                        : isSemanticDuplicate
                            ? "semantic-duplicate"
                            : "low-value-index";
                    NLogLogger.Info(
                        "自动索引建议已过滤：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, Reason={Reason}",
                        _dialect.ProviderName,
                        candidate.SqlFingerprint,
                        tableKey,
                        blockReason);
                }

                var filteredActions = new List<string>(candidate.SuggestedActions.Count);
                foreach (var action in candidate.SuggestedActions) {
                    if (IsCreateIndexAction(action) && shouldFilterIndex) {
                        blockedCreateIndexActions.Add(NormalizeSqlText(action));
                        continue;
                    }

                    filteredActions.Add(action);
                }

                if (filteredActions.Count == 0) {
                    continue;
                }

                filteredCandidates.Add(new SlowQueryTuningCandidate(
                    candidate.SqlFingerprint,
                    candidate.SchemaName,
                    candidate.TableName,
                    candidate.WhereColumns,
                    filteredActions));
            }

            var filteredInsights = result.SuggestionInsights
                .Where(insight => !ShouldBlockSuggestionInsight(insight, blockedCreateIndexActions))
                .ToList();
            return result with {
                TuningCandidates = filteredCandidates,
                SuggestionInsights = filteredInsights,
                ReadOnlySuggestions = filteredInsights.Select(static insight => insight.SuggestionSql).ToList()
            };
        }

        /// <summary>按需加载模型静态索引列信息。</summary>
        private async Task EnsureModelIndexColumnsLoadedAsync(CancellationToken cancellationToken) {
            if (_modelIndexColumnsByTable.Count > 0) {
                return;
            }

            try {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
                foreach (var entityType in dbContext.Model.GetEntityTypes()) {
                    var tableName = entityType.GetTableName();
                    if (string.IsNullOrWhiteSpace(tableName)) {
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var schemaName = entityType.GetSchema();
                    var tableKey = BuildTableKey(schemaName, tableName);
                    var tableStoreObject = StoreObjectIdentifier.Table(tableName, schemaName);
                    var indexColumns = entityType.GetIndexes()
                        .Select(index => index.Properties
                            .Select(property => property.GetColumnName(tableStoreObject))
                            .Where(static columnName => !string.IsNullOrWhiteSpace(columnName))
                            .Select(static columnName => columnName!.Trim().ToLowerInvariant())
                            .ToArray())
                        .Where(static columns => columns.Length > 0)
                        .ToArray();
                    if (indexColumns.Length == 0) {
                        continue;
                    }

                    _modelIndexColumnsByTable[tableKey] = indexColumns;
                }
            } catch (Exception ex) {
                NLogLogger.Error(ex, "加载模型索引覆盖信息失败：Provider={Provider}", _dialect.ProviderName);
            }
        }

        /// <summary>判断建议是否属于低价值索引候选。</summary>
        private bool IsLowValueIndexCandidate(SlowQueryMetric? metric) {
            if (metric is null) {
                return true;
            }

            return metric.CallCount < _configuredTriggerCount * 2
                && metric.P99Milliseconds < _configuredAlertP99Milliseconds
                && metric.TimeoutRatePercent <= 0m
                && metric.ErrorRatePercent <= 0m;
        }

        /// <summary>判断候选索引是否已被模型静态索引覆盖或语义重复。</summary>
        private bool IsCoveredByModelIndex(string tableKey, IReadOnlyList<string> candidateColumns) {
            if (candidateColumns.Count == 0 || !_modelIndexColumnsByTable.TryGetValue(tableKey, out var existingIndexes)) {
                return false;
            }

            foreach (var existing in existingIndexes) {
                if (IsPrefixMatch(existing, candidateColumns)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>判断列序列是否为前缀匹配。</summary>
        private static bool IsPrefixMatch(IReadOnlyList<string> source, IReadOnlyList<string> target) {
            if (source.Count == 0 || target.Count == 0 || source.Count < target.Count) {
                return false;
            }

            for (var i = 0; i < target.Count; i++) {
                if (!string.Equals(source[i], target[i], StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>判断建议洞察是否应按已拦截的索引动作过滤。</summary>
        private static bool ShouldBlockSuggestionInsight(SlowQuerySuggestionInsight insight, HashSet<string> blockedCreateIndexActions) {
            var suggestionBody = NormalizeSuggestionBody(insight.SuggestionSql);
            return blockedCreateIndexActions.Contains(suggestionBody);
        }

        /// <summary>移除只读建议标记后返回标准化 SQL。</summary>
        private static string NormalizeSuggestionBody(string suggestionSql) {
            var sql = suggestionSql.Trim();
            var markerIndex = sql.IndexOf("*/", StringComparison.Ordinal);
            if (markerIndex >= 0) {
                sql = sql[(markerIndex + 2)..];
            }

            return NormalizeSqlText(sql);
        }

        /// <summary>判断动作 SQL 是否为 CREATE INDEX。</summary>
        private static bool IsCreateIndexAction(string actionSql) {
            if (string.IsNullOrWhiteSpace(actionSql)) {
                return false;
            }

            var normalized = TrimLeadingComments(actionSql);
            return CreateIndexActionRegex.IsMatch(normalized);
        }

        /// <summary>构造统一索引签名（table + columns）。</summary>
        private static string BuildIndexSignature(string tableKey, IReadOnlyList<string> indexColumns) {
            if (string.IsNullOrWhiteSpace(tableKey) || indexColumns.Count == 0) {
                return string.Empty;
            }

            return $"{tableKey}|{string.Join(",", indexColumns)}";
        }

        /// <summary>归一化候选索引列，过滤空白并限制最大列数（maxColumnCount ≤ 0 时返回空数组； 当前调用侧固定传入 3）。</summary>
        private static string[] NormalizeCandidateColumns(IReadOnlyList<string> columns, int maxColumnCount) {
            if (columns.Count == 0 || maxColumnCount <= 0) {
                return [];
            }

            return columns
                .Where(static column => !string.IsNullOrWhiteSpace(column))
                .Select(static column => column.Trim().ToLowerInvariant())
                .Take(maxColumnCount)
                .ToArray();
        }

        /// <summary>标准化 SQL 文本以便做集合去重。</summary>
        private static string NormalizeSqlText(string sql) {
            var normalized = TrimLeadingComments(sql).Trim();
            return Regex.Replace(normalized, @"\s+", " ", RegexOptions.CultureInvariant).ToLowerInvariant();
        }

        /// <summary>更新表热度与容量观测快照。</summary>
        private void UpdateAutonomousSignals(
            SlowQueryAnalysisResult result,
            DateTime cycleTime) {
            // 容量预测与执行热度是相互独立的功能，分别检查各自的开关。
            if (!_enableFullAutomation && !_enableCapacityPrediction) {
                return;
            }

            // 步骤 1：对历史热度做衰减（仅全自动模式下维护热度，避免旧热点长期占用容量）。
            if (_enableFullAutomation) {
                foreach (var table in _tableHeatByTable.Keys.ToArray()) {
                    var decayed = (int)Math.Floor(_tableHeatByTable[table] * 0.9d);
                    if (decayed <= 0) {
                        _tableHeatByTable.Remove(table);
                        continue;
                    }

                    _tableHeatByTable[table] = decayed;
                }
            }

            // 步骤 2：按全量慢 SQL 指标聚合表级调用量与影响行数（避免仅依赖 tuning candidates 造成统计失真）。
            var tableSamples = new Dictionary<string, (long Rows, int Calls)>(StringComparer.OrdinalIgnoreCase);
            foreach (var metric in result.Metrics) {
                if (!TryExtractMetricTableKey(metric.SampleSql, out var tableKey)) {
                    continue;
                }

                if (!tableSamples.TryGetValue(tableKey, out var sample)) {
                    sample = (Rows: 0L, Calls: 0);
                }

                sample.Rows += metric.TotalAffectedRows;
                sample.Calls += metric.CallCount;
                tableSamples[tableKey] = sample;
            }

            // 步骤 3：刷新热度与容量样本，并触发趋势评估。
            foreach (var pair in tableSamples) {
                // 表热度仅在全自动模式下维护（用于执行策略评估）。
                if (_enableFullAutomation) {
                    if (_tableHeatByTable.TryGetValue(pair.Key, out var currentHeat)) {
                        _tableHeatByTable[pair.Key] = currentHeat + pair.Value.Calls;
                    }
                    else {
                        _tableHeatByTable[pair.Key] = pair.Value.Calls;
                    }
                }

                // 容量预测独立于全自动开关，单独由 _enableCapacityPrediction 控制。
                if (_enableCapacityPrediction) {
                    if (!_capacitySnapshotsByTable.TryGetValue(pair.Key, out var queue)) {
                        queue = new Queue<TableCapacitySnapshot>(MaxCapacitySnapshotsPerTable);
                        _capacitySnapshotsByTable[pair.Key] = queue;
                    }

                    if (queue.Count >= MaxCapacitySnapshotsPerTable) {
                        queue.Dequeue();
                    }

                    queue.Enqueue(new TableCapacitySnapshot(cycleTime, pair.Value.Rows, pair.Value.Calls));
                    EmitCapacityForecast(pair.Key, queue);
                }
            }

            EmitShardingObservabilityMetrics(result.Metrics, tableSamples);
        }

        /// <summary>输出分表命中率、跨分表查询占比与热点倾斜三类观测指标。</summary>
        private void EmitShardingObservabilityMetrics(
            IReadOnlyList<SlowQueryMetric> metrics,
            IReadOnlyDictionary<string, (long Rows, int Calls)> tableSamples) {
            if (metrics.Count == 0) {
                return;
            }

            var totalCalls = metrics.Sum(static metric => metric.CallCount);
            if (totalCalls <= 0) {
                return;
            }

            var shardingHitCalls = metrics.Sum(static metric => IsShardingHitQuery(metric.SampleSql) ? metric.CallCount : 0);
            var hitRate = Math.Clamp((double)shardingHitCalls / totalCalls, 0d, 1d);
            var crossTableCalls = metrics.Sum(static metric => IsCrossTableQuery(metric.SampleSql) ? metric.CallCount : 0);
            var crossTableRatio = Math.Clamp((double)crossTableCalls / totalCalls, 0d, 1d);

            var hotTableSkew = 0d;
            if (tableSamples.Count > 0) {
                var maxCalls = tableSamples.Values.Max(static sample => sample.Calls);
                var averageCalls = tableSamples.Values.Average(static sample => sample.Calls);
                hotTableSkew = averageCalls > 0d ? maxCalls / averageCalls : 0d;
            }

            var tags = new Dictionary<string, string> {
                ["provider"] = _dialect.ProviderName
            };
            _observability.EmitMetric("autotuning.sharding.hit_rate", hitRate, tags);
            _observability.EmitMetric("autotuning.sharding.cross_table_query_ratio", crossTableRatio, tags);
            _observability.EmitMetric("autotuning.sharding.hot_table_skew", hotTableSkew, tags);
        }

        /// <summary>根据表级样本序列估算查询体量增长趋势并告警。</summary>
        private void EmitCapacityForecast(string tableKey, Queue<TableCapacitySnapshot> snapshots) {
            // 步骤 1：确保观察窗口足够，避免短样本导致误报。
            if (snapshots.Count < 2) {
                return;
            }

            var ordered = snapshots.ToArray();
            var first = ordered[0];
            var last = ordered[^1];
            var observationWindowSeconds = (last.CapturedLocalTime - first.CapturedLocalTime).TotalSeconds;
            if (observationWindowSeconds <= 0d) {
                NLogLogger.Warn(
                    "闭环自治查询体量趋势预测跳过：Provider={Provider}, Table={Table}, ElapsedSeconds={ElapsedSeconds:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    observationWindowSeconds,
                    "non-positive elapsed time");
                return;
            }

            var minimumElapsedSeconds = _analyzeIntervalSeconds * 3d;
            if (observationWindowSeconds < minimumElapsedSeconds) {
                NLogLogger.Info(
                    "闭环自治查询体量趋势预测跳过：Provider={Provider}, Table={Table}, ElapsedSeconds={ElapsedSeconds:F0}, MinimumElapsedSeconds={MinimumElapsedSeconds:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    observationWindowSeconds,
                    minimumElapsedSeconds,
                    "insufficient observation window");
                return;
            }

            // 步骤 2：按窗口样本均值估算投影体量（AffectedRows 为每轮窗口值，不能直接做首尾差分增长）。
            var averageRowsPerCycle = ordered.Average(static snapshot => (double)snapshot.AffectedRows);
            var cyclesPerDay = TimeSpan.FromDays(1).TotalSeconds / _analyzeIntervalSeconds;
            var projectedWindowRows = averageRowsPerCycle * cyclesPerDay * _capacityProjectionDays;
            if (projectedWindowRows < _capacityGrowthAlertRows) {
                return;
            }

            // 步骤 3：达到投影阈值后输出容量告警并给出治理建议。
            NLogLogger.Warn(
                "闭环自治查询体量趋势告警：Provider={Provider}, Table={Table}, ProjectionDays={ProjectionDays}, ProjectedWindowQueryVolume={ProjectedWindowQueryVolume:F0}, AverageQueryVolumePerCycle={AverageRowsPerCycle:F0}, ActionHint={Hint}",
                _dialect.ProviderName,
                tableKey,
                _capacityProjectionDays,
                projectedWindowRows,
                averageRowsPerCycle,
                projectedWindowRows >= _capacityHotLayeringRows ? "建议冷热分层 + 历史归档 + 索引重评估" : "建议优先检查索引与统计信息维护策略");
        }

        /// <summary>生成统一的小写表标识（schema.table）。</summary>
        private static string BuildTableKey(string? schemaName, string tableName) {
            return string.IsNullOrWhiteSpace(schemaName)
                ? tableName.Trim().ToLowerInvariant()
                : $"{schemaName.Trim().ToLowerInvariant()}.{tableName.Trim().ToLowerInvariant()}";
        }

        /// <summary>识别 SQL 是否可视为分表命中（单主表且非跨表语义）。</summary>
        private static bool IsShardingHitQuery(string sql) {
            if (string.IsNullOrWhiteSpace(sql)) {
                return false;
            }

            var normalizedSql = TrimLeadingComments(sql);
            if (IsCrossTableQuery(normalizedSql)) {
                return false;
            }

            return SlowQueryAutoTuningPipeline.TryExtractPrimaryTable(normalizedSql, out _, out _);
        }

        /// <summary>识别 SQL 是否存在跨表查询语义（JOIN、集合运算、多 FROM、逗号连接）。</summary>
        private static bool IsCrossTableQuery(string sql) {
            if (string.IsNullOrWhiteSpace(sql)) {
                return false;
            }

            var normalizedSql = TrimLeadingComments(sql);
            return JoinKeywordRegex.IsMatch(normalizedSql)
                || SetOperatorRegex.IsMatch(normalizedSql)
                || MultiFromRegex.IsMatch(normalizedSql)
                || HasCommaJoinInFromClause(normalizedSql);
        }

        /// <summary>判断 FROM 子句是否存在顶层逗号连接（避免 WHERE/ORDER BY 中逗号误判）。</summary>
        private static bool HasCommaJoinInFromClause(string normalizedSql) {
            var fromMatch = FromClauseRegex.Match(normalizedSql);
            if (!fromMatch.Success) {
                return false;
            }

            var fromClause = fromMatch.Groups["from"].Value;
            var depth = 0;
            foreach (var ch in fromClause) {
                if (ch == '(') {
                    depth++;
                    continue;
                }

                if (ch == ')') {
                    if (depth > 0) {
                        depth--;
                    }
                    continue;
                }

                if (ch == ',' && depth == 0) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>尝试从慢 SQL 提取主表 key（schema.table）。</summary>
        private static bool TryExtractMetricTableKey(string sql, out string tableKey) {
            tableKey = string.Empty;
            if (string.IsNullOrWhiteSpace(sql)) {
                return false;
            }

            var normalizedSql = TrimLeadingComments(sql);
            if (!SlowQueryAutoTuningPipeline.TryExtractPrimaryTable(normalizedSql, out var schemaName, out var tableName)) {
                return false;
            }

            tableKey = BuildTableKey(schemaName, tableName);
            return true;
        }

        /// <summary>判断当前时刻是否位于高峰执行窗口。</summary>
        private bool IsInPeakWindow(TimeSpan nowTime) {
            if (_peakStartTime == _peakEndTime) {
                return false;
            }

            if (_peakStartTime < _peakEndTime) {
                return nowTime >= _peakStartTime && nowTime < _peakEndTime;
            }

            return nowTime >= _peakStartTime || nowTime < _peakEndTime;
        }

        /// <summary>识别是否为高风险 DDL 动作。</summary>
        private bool IsDangerousAction(string actionSql) {
            if (string.IsNullOrWhiteSpace(actionSql)) {
                return false;
            }

            var normalized = TrimLeadingComments(actionSql);
            return DangerousDdlRegex.IsMatch(normalized);
        }

        /// <summary>尝试为自动创建索引动作生成回滚 SQL。</summary>
        private string? BuildRollbackSql(string actionSql) {
            if (string.IsNullOrWhiteSpace(actionSql)) {
                return null;
            }

            var normalized = TrimLeadingComments(actionSql);
            var sqlServerMatch = SqlServerCreateIndexRegex.Match(normalized);
            if (sqlServerMatch.Success) {
                var indexName = sqlServerMatch.Groups["index"].Value;
                var tableName = sqlServerMatch.Groups["table"].Value;
                return $"DROP INDEX [{indexName}] ON {tableName}";
            }

            var mySqlMatch = MySqlCreateIndexRegex.Match(normalized);
            if (mySqlMatch.Success) {
                var indexName = mySqlMatch.Groups["index"].Value;
                var tableName = mySqlMatch.Groups["table"].Value;
                return $"DROP INDEX `{indexName}` ON {tableName}";
            }

            return null;
        }

        /// <summary>审计当前运行参数是否符合推荐基线。</summary>
        private void AuditBaseline() {
            AuditBaselineItem("CommandTimeoutSeconds", _configuredCommandTimeoutSeconds, _baselineCommandTimeoutSeconds);
            AuditBaselineItem("MaxRetryCount", _configuredMaxRetryCount, _baselineMaxRetryCount);
            AuditBaselineItem("MaxRetryDelaySeconds", _configuredMaxRetryDelaySeconds, _baselineMaxRetryDelaySeconds);
            AuditBaselineItem("SlowQueryThresholdMilliseconds", _configuredSlowQueryThresholdMilliseconds, 500);
            AuditBaselineItem("TriggerCount", _configuredTriggerCount, 3);
            AuditBaselineItem("MaxActionsPerCycle", _configuredMaxSuggestionsPerCycle, 3);
            AuditBaselineItem("AlertP99Milliseconds", _configuredAlertP99Milliseconds, 500);
            AuditBaselineItem("AlertTimeoutRatePercent", (double)_configuredAlertTimeoutRatePercent, 1d);
            AuditBaselineItem("AlertDeadlockCount", _configuredAlertDeadlockCount, 1);

            // 资源阈值边界审计：记录配置的连接池与内存告警阈值，超出推荐上限时告警
            NLogLogger.Info(
                "资源阈值配置审计：MaxConnectionPoolSize={MaxConnectionPoolSize}（推荐 ≤ 200），MemoryWarningThresholdMB={MemoryWarningThresholdMB}（推荐 ≥ 512）",
                _resourceMaxConnectionPoolSize,
                _resourceMemoryWarningThresholdMB);
            if (_resourceMaxConnectionPoolSize > 200) {
                NLogLogger.Warn(
                    "资源阈值告警：MaxConnectionPoolSize={MaxConnectionPoolSize} 超出推荐上限 200，可能导致数据库连接耗尽。",
                    _resourceMaxConnectionPoolSize);
                _observability.EmitEvent("resource.threshold.exceeded", NLog.LogLevel.Warn, $"MaxConnectionPoolSize={_resourceMaxConnectionPoolSize} exceeds recommended 200");
            }
            if (_resourceMemoryWarningThresholdMB > 0 && _resourceMemoryWarningThresholdMB < 512) {
                NLogLogger.Warn(
                    "资源阈值告警：MemoryWarningThresholdMB={MemoryWarningThresholdMB} 低于推荐下限 512 MB，可能导致频繁触发内存告警。",
                    _resourceMemoryWarningThresholdMB);
                _observability.EmitEvent("resource.threshold.exceeded", NLog.LogLevel.Warn, $"MemoryWarningThresholdMB={_resourceMemoryWarningThresholdMB} is below recommended 512");
            }
        }

        /// <summary>输出单个参数的基线审计结果。</summary>
        private void AuditBaselineItem(string key, int configured, int baseline) {
            AuditBaselineItem(key, (double)configured, baseline);
        }

        /// <summary>
        /// 执行逻辑：AuditBaselineItem。
        /// </summary>
        private void AuditBaselineItem(string key, double configured, double baseline) {
            if (Math.Abs(configured - baseline) < 0.0001d) {
                NLogLogger.Info("运行参数基线审计通过：Key={Key}, Current={Current}, Baseline={Baseline}", key, configured, baseline);
                return;
            }

            var ratio = baseline <= 0d ? 1d : Math.Abs(configured - baseline) / baseline;
            var level = ratio >= 0.5d ? "high" : ratio >= 0.2d ? "medium" : "low";
            NLogLogger.Warn(
                "运行参数基线审计告警：Key={Key}, Current={Current}, Baseline={Baseline}, DeviationLevel={DeviationLevel}, Recommended={Recommended}",
                key,
                configured,
                baseline,
                level,
                baseline);
            _observability.EmitEvent(
                "autotuning.baseline.deviation",
                NLog.LogLevel.Warn,
                $"baseline deviation: {key}",
                new Dictionary<string, string> {
                    ["key"] = key,
                    ["level"] = level
                });
        }

        /// <summary>加载执行白名单并标准化为小写。</summary>
        private static HashSet<string> LoadWhitelistedTables(IConfigurationSection section) {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in section.GetChildren()) {
                var value = child.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value)) {
                    continue;
                }

                tables.Add(value.ToLowerInvariant());
            }

            return tables;
        }

        /// <summary>移除 SQL 前导注释，便于后续规则匹配。</summary>
        private static string TrimLeadingComments(string sql) {
            var normalized = sql;
            while (true) {
                var match = LeadingCommentRegex.Match(normalized);
                if (!match.Success) {
                    break;
                }

                normalized = normalized[match.Length..];
            }

            return normalized.TrimStart();
        }

        /// <summary>裁剪追踪状态，限制内存占用并剔除过期记录。</summary>
        private void PruneTrackingState() {
            var rollbackOverflow = _pendingRollbackByFingerprint.Count - MaxTrackedFingerprintCount;
            if (rollbackOverflow > 0) {
                var removeRollbacks = _pendingRollbackByFingerprint
                    .OrderBy(static pair => pair.Value.CreatedTime)
                    .Take(rollbackOverflow)
                    .Select(static pair => pair.Key)
                    .ToArray();
                foreach (var key in removeRollbacks) {
                    _pendingRollbackByFingerprint.Remove(key);
                }
            }

            var now = DateTime.Now;
            var expiredFingerprints = _pendingRollbackByFingerprint
                .Where(pair => now - pair.Value.CreatedTime > PendingRollbackRetention)
                .Select(static pair => pair.Key)
                .ToArray();
            foreach (var fingerprint in expiredFingerprints) {
                _pendingRollbackByFingerprint.Remove(fingerprint);
            }

            var tableOverflow = _tableHeatByTable.Count - MaxTrackedTableCount;
            if (tableOverflow > 0) {
                var removeTables = _tableHeatByTable
                    .OrderBy(static pair => pair.Value)
                    .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair => pair.Key)
                    .Take(tableOverflow)
                    .ToArray();
                foreach (var table in removeTables) {
                    _tableHeatByTable.Remove(table);
                    _capacitySnapshotsByTable.Remove(table);
                }
            }
        }

    }
}
