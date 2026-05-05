using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 查询治理测试。
/// </summary>
public sealed class QueryGovernanceTests {
    /// <summary>
    /// 必须登记的查询模板应全部存在。
    /// </summary>
    [Fact]
    public void QueryTemplateRegistry_ShouldContainMandatoryTemplates() {
        var registry = new QueryTemplateRegistry();
        var templates = registry.GetAll();

        Assert.Equal(6, templates.Count);
        Assert.Contains(templates, static item => item.TemplateName == "ParcelRecentCursorQuery");
        Assert.Contains(templates, static item => item.TemplateName == "ParcelByBarcodeQuery");
        Assert.Contains(templates, static item => item.TemplateName == "ParcelByChuteQuery");
        Assert.Contains(templates, static item => item.TemplateName == "ParcelByWorkstationQuery");
        Assert.Contains(templates, static item => item.TemplateName == "WebRequestAuditLogCursorQuery");
        Assert.Contains(templates, static item => item.TemplateName == "ArchiveTaskListQuery");
    }

    /// <summary>
    /// 已登记模板命中慢查询画像时应输出索引建议。
    /// </summary>
    [Fact]
    public void QueryIndexRecommendationService_WhenObservedTemplateIsSlow_ShouldReturnRecommendation() {
        var configuration = BuildConfiguration(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:QueryGovernance:RecommendationP99Milliseconds"] = "800",
            ["Persistence:AutoTuning:QueryGovernance:MinimumCallCount"] = "1"
        });
        var store = new SlowQueryProfileStore(configuration);
        store.Record("SELECT Id FROM Parcels WHERE WorkstationName = 'WS-01' AND ScannedTime >= '2026-01-01 00:00:00' ORDER BY ScannedTime DESC, Id DESC", TimeSpan.FromMilliseconds(1500));
        var service = new QueryIndexRecommendationService(store, new QueryTemplateRegistry(), configuration);

        var report = service.BuildReport();

        var recommendation = Assert.Single(report.Recommendations);
        Assert.Equal("ParcelByWorkstationQuery", recommendation.TemplateName);
        Assert.Contains("WorkstationName", recommendation.RecommendedIndex, StringComparison.Ordinal);
        Assert.Equal(1, report.MatchedTemplateCount);
        Assert.Empty(report.UnmatchedSlowQueryFingerprints);
    }

    /// <summary>
    /// 未登记模板覆盖的慢查询应进入待补登记列表。
    /// </summary>
    [Fact]
    public void QueryIndexRecommendationService_WhenObservedSqlIsUnmatched_ShouldExposeFingerprintGap() {
        var configuration = BuildConfiguration(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:QueryGovernance:RecommendationP99Milliseconds"] = "800",
            ["Persistence:AutoTuning:QueryGovernance:MinimumCallCount"] = "1"
        });
        var store = new SlowQueryProfileStore(configuration);
        store.Record("SELECT * FROM QueryDiagnostics WHERE Fingerprint = 'slow-query-gap'", TimeSpan.FromMilliseconds(1200));
        var service = new QueryIndexRecommendationService(store, new QueryTemplateRegistry(), configuration);

        var report = service.BuildReport();

        Assert.Empty(report.Recommendations);
        Assert.Equal(0, report.MatchedTemplateCount);
        Assert.Single(report.UnmatchedSlowQueryFingerprints);
        Assert.Equal(6, report.TemplatesWithoutObservedProfiles.Count);
    }

    /// <summary>
    /// 构建测试配置。
    /// </summary>
    /// <param name="overrides">覆盖项。</param>
    /// <returns>配置实例。</returns>
    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?>? overrides = null) {
        var values = new Dictionary<string, string?> {
            ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = "500",
            ["Persistence:AutoTuning:SlowQueryProfile:IsEnabled"] = "true",
            ["Persistence:AutoTuning:SlowQueryProfile:WindowMinutes"] = "30",
            ["Persistence:AutoTuning:SlowQueryProfile:TopN"] = "50",
            ["Persistence:AutoTuning:SlowQueryProfile:MaxFingerprintCount"] = "1000",
            ["Persistence:AutoTuning:SlowQueryProfile:MaxSampleCountPerFingerprint"] = "256"
        };
        if (overrides is not null) {
            foreach (var pair in overrides) {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
