namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Copilot 规则脚本回归测试。
/// </summary>
public sealed class CopilotRuleValidationScriptTests {
    /// <summary>
    /// README 历史记录门禁应覆盖常见标题变体，避免仅靠单一关键字漏检。
    /// </summary>
    [Fact]
    public void ValidateCopilotRulesScript_ShouldCoverReadmeHistoryHeadingVariants() {
        var scriptContent = RepositoryFileReader.ReadAllText(".github", "scripts", "validate-copilot-rules.sh");

        Assert.Contains("更新记录|历史更新记录|更新历史", scriptContent, StringComparison.Ordinal);
        Assert.Contains("CHANGELOG|Changelog|History|历史", scriptContent, StringComparison.Ordinal);
        Assert.Contains("新增了历史更新记录相关章节标题", scriptContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// 性能反模式门禁应覆盖零值比较、Where 后再取首项与 string.Format 等常见场景。
    /// </summary>
    [Fact]
    public void ValidateCopilotRulesScript_ShouldCoverAdditionalPerformanceAntiPatterns() {
        var scriptContent = RepositoryFileReader.ReadAllText(".github", "scripts", "validate-copilot-rules.sh");

        Assert.Contains("Count\\(\\)[[:space:]]*(==|!=|>|<|>=|<=)[[:space:]]*0", scriptContent, StringComparison.Ordinal);
        Assert.Contains("\\.Where\\([^\\r\\n]*\\)\\.(FirstOrDefault|First|SingleOrDefault|Single)\\(", scriptContent, StringComparison.Ordinal);
        Assert.Contains("string\\.Format\\(", scriptContent, StringComparison.Ordinal);
    }
}
