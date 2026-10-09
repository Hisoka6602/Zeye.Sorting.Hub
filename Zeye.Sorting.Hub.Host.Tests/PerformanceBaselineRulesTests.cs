namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 压测基线资产规则测试。
/// </summary>
public sealed class PerformanceBaselineRulesTests {
    /// <summary>
    /// 压测目录说明应覆盖 PR-S 所需场景与本地时间约束。
    /// </summary>
    [Fact]
    public void PerformanceReadmeShouldDescribeCoveredScenariosAndLocalTimeRules() {
        var performanceReadme = RepositoryFileReader.ReadAllText("performance", "README.md");

        Assert.Contains("Parcel 游标分页", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("Parcel 普通分页", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("Parcel 批量缓冲写入", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("审计日志查询", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("HealthCheck", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("慢查询画像 API", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("无 `Z`、无 offset 的本地时间字符串", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("DateTime.Ticks", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("performance-smoke-test.yml", performanceReadme, StringComparison.Ordinal);
    }

    /// <summary>
    /// k6 脚本应覆盖 PR-S 要求的核心接口，并统一通过本地时间格式化函数生成时间参数。
    /// </summary>
    [Fact]
    public void PerformanceScriptsShouldTargetRequiredApisAndUseLocalTimeFormatting() {
        var commonScript = RepositoryFileReader.ReadAllText("performance", "k6", "common.js");
        var parcelQueryScript = RepositoryFileReader.ReadAllText("performance", "k6", "parcel-cursor-query.js");
        var parcelBatchScript = RepositoryFileReader.ReadAllText("performance", "k6", "parcel-batch-buffer-write.js");
        var auditQueryScript = RepositoryFileReader.ReadAllText("performance", "k6", "audit-query.js");

        Assert.Contains("formatLocalDateTime", commonScript, StringComparison.Ordinal);
        Assert.Contains("createDotNetTicksLiteral", commonScript, StringComparison.Ordinal);
        Assert.Contains("/api/parcels/cursor", parcelQueryScript, StringComparison.Ordinal);
        Assert.Contains("/api/parcels?pageNumber=1", parcelQueryScript, StringComparison.Ordinal);
        Assert.Contains("createLocalWindow", parcelQueryScript, StringComparison.Ordinal);
        Assert.Contains("/api/admin/parcels/batch-buffer", parcelBatchScript, StringComparison.Ordinal);
        Assert.Contains("createParcelBatchPayload", parcelBatchScript, StringComparison.Ordinal);
        Assert.Contains("/api/audit/web-requests", auditQueryScript, StringComparison.Ordinal);
        Assert.Contains("/health/ready", auditQueryScript, StringComparison.Ordinal);
        Assert.Contains("/api/diagnostics/slow-queries", auditQueryScript, StringComparison.Ordinal);
    }

    /// <summary>
    /// 压测 smoke workflow 应只执行轻量规则测试，避免在 CI 中触发真实压测。
    /// </summary>
    [Fact]
    public void PerformanceSmokeWorkflowShouldRunFilteredRulesTests() {
        var workflowContent = RepositoryFileReader.ReadAllText(".github", "workflows", "performance-smoke-test.yml");

        Assert.Contains("performance-smoke-test", workflowContent, StringComparison.Ordinal);
        Assert.Contains("performance/**", workflowContent, StringComparison.Ordinal);
        Assert.Contains("性能基线报告.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("PerformanceBaselineRulesTests", workflowContent, StringComparison.Ordinal);
        Assert.DoesNotContain("k6 run", workflowContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// 真实回归工作流应执行读取门禁、显式保护写入场景并留存机器可读结果。
    /// </summary>
    [Fact]
    public void PerformanceRegressionWorkflowShouldRunThresholdGatesAndPublishResults() {
        var workflowContent = RepositoryFileReader.ReadAllText(".github", "workflows", "performance-regression-gate.yml");

        Assert.Contains("grafana/setup-k6-action@v1", workflowContent, StringComparison.Ordinal);
        Assert.Contains("parcel-cursor-query.js", workflowContent, StringComparison.Ordinal);
        Assert.Contains("audit-query.js", workflowContent, StringComparison.Ordinal);
        Assert.Contains("run_write_scenario", workflowContent, StringComparison.Ordinal);
        Assert.Contains("--summary-export", workflowContent, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", workflowContent, StringComparison.Ordinal);
        Assert.Contains("summarize-k6.ps1", workflowContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// 性能基线报告模板应包含 PR-S 强制指标与全部场景。
    /// </summary>
    [Fact]
    public void PerformanceBaselineReportTemplateShouldContainRequiredMetricsAndScenarios() {
        var reportTemplate = RepositoryFileReader.ReadAllText("性能基线报告.md");

        Assert.Contains("RPS", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("P50", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("P95", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("P99", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("错误率", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("超时率", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("数据库连接池占用", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("写入队列深度", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("CPU", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("内存", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("GC 次数", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("Parcel 游标分页", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("Parcel 普通分页", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("Parcel 批量缓冲写入", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("审计日志查询", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("HealthCheck", reportTemplate, StringComparison.Ordinal);
        Assert.Contains("慢查询画像 API", reportTemplate, StringComparison.Ordinal);
    }
}
