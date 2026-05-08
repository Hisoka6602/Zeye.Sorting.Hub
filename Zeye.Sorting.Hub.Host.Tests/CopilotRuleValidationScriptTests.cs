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
    public void ValidateScript_ShouldDetectReadmeHistoryHeadingVariants() {
        if (!CanUseBash()) {
            return;
        }

        var scriptContent = RepositoryFileReader.ReadAllText(".github", "scripts", "validate-copilot-rules.sh");
        var historyKeywords = ExtractVariableValue(scriptContent, "history_heading_keywords");
        var headingPattern = ExtractVariableValue(scriptContent, "heading_pattern")
            .Replace("${history_heading_keywords}", historyKeywords, StringComparison.Ordinal);

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
    public void ValidateScript_ShouldDetectAdditionalPerformanceAntiPatterns() {
        if (!CanUseBash()) {
            return;
        }

        var scriptContent = RepositoryFileReader.ReadAllText(".github", "scripts", "validate-copilot-rules.sh");
        var countZeroPattern = ExtractVariableValue(scriptContent, "pattern_count_zero");
        var whereFirstPattern = ExtractVariableValue(scriptContent, "pattern_where_first");
        var stringFormatPattern = ExtractVariableValue(scriptContent, "pattern_string_format");

        Assert.True(IsGrepPatternMatch(countZeroPattern, "var hasAny = results.Count() > 0;"));
        Assert.False(IsGrepPatternMatch(countZeroPattern, "var hasAny = results.Any();"));
        Assert.True(IsGrepPatternMatch(whereFirstPattern, "var item = results.Where(static item => item.IsReady).FirstOrDefault();"));
        Assert.False(IsGrepPatternMatch(whereFirstPattern, "var item = results.FirstOrDefault(static item => item.IsReady);"));
        Assert.True(IsGrepPatternMatch(stringFormatPattern, "var text = string.Format(\"值：{0}\", count);"));
        Assert.False(IsGrepPatternMatch(stringFormatPattern, "var text = $\"值：{count}\";"));
    }

    /// <summary>
    /// 从脚本中提取变量值。
    /// </summary>
    /// <param name="scriptContent">脚本文本。</param>
    /// <param name="variableName">变量名。</param>
    /// <returns>变量对应的正则表达式。</returns>
    private static string ExtractVariableValue(string scriptContent, string variableName) {
        var match = Regex.Match(
            scriptContent,
            $@"local {Regex.Escape(variableName)}=(?:""(?<value>[^""]+)""|'(?<value>[^']+)')",
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
        // 步骤 1：启动 Bash，并通过环境变量传入模式与待测文本，避免额外转义复杂度。
        using var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "/usr/bin/env",
                Arguments = "bash -lc \"printf '%s\\n' \\\"$TEST_INPUT\\\" | grep -Eq \\\"$TEST_PATTERN\\\"\"",
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

    /// <summary>
    /// 判断当前环境是否支持 Bash 校验。
    /// </summary>
    /// <returns>若当前环境可运行 Bash 则返回 <see langword="true"/>。</returns>
    private static bool CanUseBash() {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    }
}
