using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Copilot 规则脚本回归测试。
/// </summary>
public sealed class CopilotRuleValidationScriptTests {
    /// <summary>
    /// README 历史记录门禁应覆盖常见标题变体，同时避免把正文文本误判为标题。
    /// </summary>
    [Fact]
    public void ValidateCopilotRulesScript_ShouldDetectReadmeHistoryHeadingVariantsWithoutMatchingBodyText() {
        var scriptContent = RepositoryFileReader.ReadAllText(".github", "scripts", "validate-copilot-rules.sh");
        var headingPattern = ExtractSingleQuotedVariable(scriptContent, "heading_pattern");

        Assert.Contains("新增了历史更新记录相关章节标题", scriptContent, StringComparison.Ordinal);
        Assert.True(IsGrepPatternMatch(headingPattern, "## 更新记录"));
        Assert.True(IsGrepPatternMatch(headingPattern, "### CHANGELOG"));
        Assert.True(IsGrepPatternMatch(headingPattern, "# 历史"));
        Assert.False(IsGrepPatternMatch(headingPattern, "README 中提到 更新记录.md 文件"));
        Assert.False(IsGrepPatternMatch(headingPattern, "当前历史原因如下"));
    }

    /// <summary>
    /// 性能反模式门禁应覆盖零值比较、Where 后再取首项与 string.Format 等常见场景，并保留正常写法。
    /// </summary>
    [Fact]
    public void ValidateCopilotRulesScript_ShouldDetectAdditionalPerformanceAntiPatterns() {
        var scriptContent = RepositoryFileReader.ReadAllText(".github", "scripts", "validate-copilot-rules.sh");
        var countZeroPattern = ExtractSingleQuotedVariable(scriptContent, "pattern_count_zero");
        var whereFirstPattern = ExtractSingleQuotedVariable(scriptContent, "pattern_where_first");
        var stringFormatPattern = ExtractSingleQuotedVariable(scriptContent, "pattern_string_format");

        Assert.True(IsGrepPatternMatch(countZeroPattern, "var hasAny = results.Count() > 0;"));
        Assert.False(IsGrepPatternMatch(countZeroPattern, "var hasAny = results.Any();"));
        Assert.True(IsGrepPatternMatch(whereFirstPattern, "var item = results.Where(static item => item.IsReady).FirstOrDefault();"));
        Assert.False(IsGrepPatternMatch(whereFirstPattern, "var item = results.FirstOrDefault(static item => item.IsReady);"));
        Assert.True(IsGrepPatternMatch(stringFormatPattern, "var text = string.Format(\"值：{0}\", count);"));
        Assert.False(IsGrepPatternMatch(stringFormatPattern, "var text = $\"值：{count}\";"));
    }

    /// <summary>
    /// 从脚本中提取单引号包裹的变量值。
    /// </summary>
    /// <param name="scriptContent">脚本文本。</param>
    /// <param name="variableName">变量名。</param>
    /// <returns>变量对应的正则表达式。</returns>
    private static string ExtractSingleQuotedVariable(string scriptContent, string variableName) {
        var match = Regex.Match(
            scriptContent,
            $@"local {Regex.Escape(variableName)}='(?<value>[^']+)'",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, $"未找到脚本变量：{variableName}");
        return match.Groups["value"].Value;
    }

    /// <summary>
    /// 使用 Bash `grep -E` 验证脚本正则是否匹配指定文本。
    /// </summary>
    /// <param name="pattern">脚本中的正则。</param>
    /// <param name="input">待验证文本。</param>
    /// <returns>匹配结果。</returns>
    private static bool IsGrepPatternMatch(string pattern, string input) {
        // 步骤 1：启动 Bash，并通过环境变量传入模式与待测文本，避免额外转义噪音。
        using var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "/bin/bash",
                Arguments = "-lc \"printf '%s\\n' \\\"$TEST_INPUT\\\" | grep -Eq \\\"$TEST_PATTERN\\\"\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.Environment["TEST_PATTERN"] = pattern;
        process.StartInfo.Environment["TEST_INPUT"] = input;

        // 步骤 2：等待 grep 完成，并把退出码映射为布尔结果。
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
