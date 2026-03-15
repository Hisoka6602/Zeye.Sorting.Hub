using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库自动调谐后台服务：慢查询分析 + 只读建议输出
    /// </summary>
    public sealed class DatabaseAutoTuningHostedService : BackgroundService {
        private readonly ILogger<DatabaseAutoTuningHostedService> _logger;
        private readonly IDatabaseDialect _dialect;
        private readonly SlowQueryAutoTuningPipeline _pipeline;
        private readonly int _analyzeIntervalSeconds;
        private readonly int _baselineCommandTimeoutSeconds;
        private readonly int _baselineMaxRetryCount;
        private readonly int _baselineMaxRetryDelaySeconds;
        private readonly int _configuredCommandTimeoutSeconds;
        private readonly int _configuredMaxRetryCount;
        private readonly int _configuredMaxRetryDelaySeconds;

        public DatabaseAutoTuningHostedService(
            ILogger<DatabaseAutoTuningHostedService> logger,
            IDatabaseDialect dialect,
            SlowQueryAutoTuningPipeline pipeline,
            IConfiguration configuration) {
            _logger = logger;
            _dialect = dialect;
            _pipeline = pipeline;
            _analyzeIntervalSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AnalyzeIntervalSeconds", 30);
            _baselineCommandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineCommandTimeoutSeconds", 30);
            _baselineMaxRetryCount = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineMaxRetryCount", 5);
            _baselineMaxRetryDelaySeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineMaxRetryDelaySeconds", 10);
            _configuredCommandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            _configuredMaxRetryCount = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MaxRetryCount", 5);
            _configuredMaxRetryDelaySeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            AuditBaseline();

            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_analyzeIntervalSeconds), stoppingToken);

                var result = _pipeline.Analyze(_dialect);
                if (result.Metrics.Count == 0) {
                    continue;
                }

                _logger.LogInformation(
                    "慢 SQL 分析窗口完成，Provider={Provider}, GeneratedTime={GeneratedTime}, Groups={Groups}, DroppedSamples={DroppedSamples}",
                    _dialect.ProviderName,
                    result.GeneratedTime,
                    result.Metrics.Count,
                    result.DroppedSamples);

                foreach (var metric in result.Metrics) {
                    _logger.LogInformation(
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
                    _logger.LogWarning("慢 SQL 阈值告警：Provider={Provider}, Alert={Alert}", _dialect.ProviderName, alert);
                }

                if (result.ShouldEmitDailyReport) {
                    EmitDailyReport(result);
                }
            }
        }

        private void EmitDailyReport(SlowQueryAnalysisResult result) {
            _logger.LogInformation(
                "每日慢 SQL 报告：Provider={Provider}, GeneratedTime={GeneratedTime}, TopCount={TopCount}, DroppedSamples={DroppedSamples}",
                _dialect.ProviderName,
                result.GeneratedTime,
                result.Metrics.Count,
                result.DroppedSamples);

            foreach (var metric in result.Metrics) {
                _logger.LogInformation(
                    "每日慢 SQL Top：Fingerprint={Fingerprint}, Calls={Calls}, P95Ms={P95Ms}, P99Ms={P99Ms}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                    metric.SqlFingerprint,
                    metric.CallCount,
                    metric.P95Milliseconds,
                    metric.P99Milliseconds,
                    metric.TimeoutRatePercent,
                    metric.DeadlockCount);
            }

            foreach (var suggestion in result.ReadOnlySuggestions) {
                _logger.LogInformation(
                    "每日慢 SQL 索引建议（只读，不自动执行，需人工确认）：Provider={Provider}, Suggestion={Suggestion}",
                    _dialect.ProviderName,
                    suggestion);
            }
        }

        private void AuditBaseline() {
            AuditBaselineItem("CommandTimeoutSeconds", _configuredCommandTimeoutSeconds, _baselineCommandTimeoutSeconds);
            AuditBaselineItem("MaxRetryCount", _configuredMaxRetryCount, _baselineMaxRetryCount);
            AuditBaselineItem("MaxRetryDelaySeconds", _configuredMaxRetryDelaySeconds, _baselineMaxRetryDelaySeconds);
        }

        private void AuditBaselineItem(string key, int configured, int baseline) {
            if (configured == baseline) {
                _logger.LogInformation("运行参数基线审计通过：Key={Key}, Current={Current}, Baseline={Baseline}", key, configured, baseline);
                return;
            }

            _logger.LogWarning("运行参数基线审计告警：Key={Key}, Current={Current}, Baseline={Baseline}", key, configured, baseline);
        }

        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }
    }
}
