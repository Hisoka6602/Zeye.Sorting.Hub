using System.IO;
using System.Linq;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 压测基线资产规则测试。
/// </summary>
public sealed class PerformanceBaselineRulesTests {
    /// <summary>
    /// 压测目录说明应覆盖 PR-S 所需场景与本地时间约束。
    /// </summary>
    [Fact]
    public void PerformanceReadme_ShouldDescribeCoveredScenariosAndLocalTimeRules() {
        var performanceReadme = ReadRepositoryFile("performance", "README.md");

        Assert.Contains("Parcel 游标分页", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("Parcel 普通分页", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("Parcel 批量缓冲写入", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("审计日志查询", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("HealthCheck", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("慢查询画像 API", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("无 `Z`、无 offset 的本地时间字符串", performanceReadme, StringComparison.Ordinal);
        Assert.Contains("performance-smoke-test.yml", performanceReadme, StringComparison.Ordinal);
    }

    /// <summary>
    /// k6 脚本应覆盖 PR-S 要求的核心接口，并统一通过本地时间格式化函数生成时间参数。
    /// </summary>
    [Fact]
    public void PerformanceScripts_ShouldTargetRequiredApisAndUseLocalTimeFormatting() {
        var commonScript = ReadRepositoryFile("performance", "k6", "common.js");
        var parcelQueryScript = ReadRepositoryFile("performance", "k6", "parcel-cursor-query.js");
        var parcelBatchScript = ReadRepositoryFile("performance", "k6", "parcel-batch-buffer-write.js");
        var auditQueryScript = ReadRepositoryFile("performance", "k6", "audit-query.js");

        Assert.Contains("formatLocalDateTime", commonScript, StringComparison.Ordinal);
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
    public void PerformanceSmokeWorkflow_ShouldRunFilteredRulesTests() {
        var workflowContent = ReadRepositoryFile(".github", "workflows", "performance-smoke-test.yml");

        Assert.Contains("performance-smoke-test", workflowContent, StringComparison.Ordinal);
        Assert.Contains("performance/**", workflowContent, StringComparison.Ordinal);
        Assert.Contains("性能基线报告.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("PerformanceBaselineRulesTests", workflowContent, StringComparison.Ordinal);
        Assert.DoesNotContain("k6 run", workflowContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// 性能基线报告模板应包含 PR-S 强制指标与全部场景。
    /// </summary>
    [Fact]
    public void PerformanceBaselineReportTemplate_ShouldContainRequiredMetricsAndScenarios() {
        var reportTemplate = ReadRepositoryFile("性能基线报告.md");

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

    /// <summary>
    /// 读取仓库文件内容。
    /// </summary>
    /// <param name="pathSegments">相对路径片段。</param>
    /// <returns>文件内容。</returns>
    private static string ReadRepositoryFile(params string[] pathSegments) {
        var repositoryRoot = LocateRepositoryRoot();
        var filePath = Path.Combine(new[] { repositoryRoot }.Concat(pathSegments).ToArray());
        return File.ReadAllText(filePath);
    }

    /// <summary>
    /// 定位仓库根目录。
    /// </summary>
    /// <returns>仓库根目录绝对路径。</returns>
    private static string LocateRepositoryRoot() {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) {
            var readmePath = Path.Combine(current.FullName, "README.md");
            var solutionPath = Path.Combine(current.FullName, "Zeye.Sorting.Hub.sln");
            if (File.Exists(readmePath) && File.Exists(solutionPath)) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("未找到仓库根目录。");
    }
}
