using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 查询治理报告后台服务。
/// </summary>
public sealed class QueryGovernanceReportHostedService : BackgroundService {
    /// <summary>
    /// 查询治理报告间隔配置键。
    /// </summary>
    private const string ReportIntervalHoursConfigKey = "QueryGovernance:ReportIntervalHours";

    /// <summary>
    /// 默认巡检间隔。
    /// </summary>
    private static readonly TimeSpan DefaultReportInterval = TimeSpan.FromHours(4);

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 查询治理索引建议服务。
    /// </summary>
    private readonly QueryIndexRecommendationService _queryIndexRecommendationService;

    /// <summary>
    /// 查询治理报告巡检间隔。
    /// </summary>
    private readonly TimeSpan _reportInterval;

    /// <summary>
    /// 初始化查询治理报告后台服务。
    /// </summary>
    /// <param name="queryIndexRecommendationService">查询治理索引建议服务。</param>
    /// <param name="configuration">配置根。</param>
    public QueryGovernanceReportHostedService(
        QueryIndexRecommendationService queryIndexRecommendationService,
        IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        _queryIndexRecommendationService = queryIndexRecommendationService ?? throw new ArgumentNullException(nameof(queryIndexRecommendationService));
        _reportInterval = ResolveReportInterval(configuration);
    }

    /// <summary>
    /// 执行查询治理巡检循环。
    /// </summary>
    /// <param name="stoppingToken">取消令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // 步骤 1：启动后立即输出一次基线报告，保证当前窗口无慢查询时也能看到模板登记完整性。
        EmitGovernanceReport();

        // 步骤 2：后续按固定周期重复巡检，持续输出模板覆盖与索引建议闭环结果。
        using var timer = new PeriodicTimer(_reportInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            EmitGovernanceReport();
        }
    }

    /// <summary>
    /// 输出查询治理报告。
    /// </summary>
    private void EmitGovernanceReport() {
        try {
            var report = _queryIndexRecommendationService.BuildReport();
            NLogLogger.Info(
                "查询治理报告：GeneratedAtLocal={GeneratedAtLocal}, RegisteredTemplateCount={RegisteredTemplateCount}, MatchedTemplateCount={MatchedTemplateCount}, ObservedSlowQueryFingerprintCount={ObservedSlowQueryFingerprintCount}, RecommendationCount={RecommendationCount}, UnmatchedSlowQueryCount={UnmatchedSlowQueryCount}",
                report.GeneratedAtLocal,
                report.RegisteredTemplateCount,
                report.MatchedTemplateCount,
                report.ObservedSlowQueryFingerprintCount,
                report.Recommendations.Count,
                report.UnmatchedSlowQueryFingerprints.Count);

            foreach (var template in report.RegisteredTemplates) {
                NLogLogger.Info(
                    "查询模板登记：TemplateName={TemplateName}, Purpose={Purpose}, ServiceName={ServiceName}, Tables={Tables}, Filters={Filters}, Sorts={Sorts}, RecommendedIndexes={RecommendedIndexes}, MaxTimeRangeHours={MaxTimeRangeHours}, IsCountAllowed={IsCountAllowed}, IsDeepPagingAllowed={IsDeepPagingAllowed}",
                    template.TemplateName,
                    template.Purpose,
                    template.ServiceName,
                    string.Join(", ", template.TableNames),
                    string.Join(", ", template.FilterColumns),
                    string.Join(", ", template.SortColumns),
                    string.Join(" | ", template.RecommendedIndexes),
                    template.MaxTimeRangeHours,
                    template.IsCountAllowed,
                    template.IsDeepPagingAllowed);
            }

            if (report.TemplatesWithoutObservedProfiles.Count > 0) {
                NLogLogger.Info(
                    "查询治理报告：以下模板当前窗口未命中慢查询画像，Templates={Templates}",
                    string.Join(" | ", report.TemplatesWithoutObservedProfiles));
            }

            if (report.UnmatchedSlowQueryFingerprints.Count > 0) {
                NLogLogger.Warn(
                    "查询治理报告：发现未登记模板覆盖的慢查询指纹，Fingerprints={Fingerprints}",
                    string.Join(" | ", report.UnmatchedSlowQueryFingerprints));
            }

            foreach (var recommendation in report.Recommendations) {
                NLogLogger.Info(
                    "查询治理索引建议（只读，不自动执行）：TemplateName={TemplateName}, Fingerprint={Fingerprint}, TableName={TableName}, RecommendedIndex={RecommendedIndex}, Reason={Reason}, RiskLevel={RiskLevel}, Confidence={Confidence}, ObservedP99Milliseconds={ObservedP99Milliseconds}, ObservedCallCount={ObservedCallCount}",
                    recommendation.TemplateName,
                    recommendation.Fingerprint,
                    recommendation.TableName,
                    recommendation.RecommendedIndex,
                    recommendation.Reason,
                    recommendation.RiskLevel,
                    recommendation.Confidence,
                    recommendation.ObservedP99Milliseconds,
                    recommendation.ObservedCallCount);
            }
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "输出查询治理报告失败。");
        }
    }

    /// <summary>
    /// 解析查询治理报告巡检间隔。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    /// <returns>巡检间隔。</returns>
    private static TimeSpan ResolveReportInterval(IConfiguration configuration) {
        var reportIntervalHours = Math.Clamp(
            AutoTuningConfigurationReader.GetPositiveIntOrDefault(
                configuration,
                AutoTuningConfigurationReader.BuildAutoTuningKey(ReportIntervalHoursConfigKey),
                (int)DefaultReportInterval.TotalHours),
            1,
            24);
        return TimeSpan.FromHours(reportIntervalHours);
    }
}
