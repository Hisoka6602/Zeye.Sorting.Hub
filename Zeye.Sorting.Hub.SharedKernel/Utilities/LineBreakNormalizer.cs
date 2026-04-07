namespace Zeye.Sorting.Hub.SharedKernel.Utilities;

/// <summary>
/// 换行标准化工具：将 CR/LF 归一化为空格，避免日志注入并保持单行输出。
/// </summary>
public static class LineBreakNormalizer {
    /// <summary>
    /// 将字符串中的 CR/LF 替换为空格，且仅在存在换行符时分配新字符串。
    /// </summary>
    /// <param name="value">输入字符串。</param>
    /// <returns>替换后的字符串。</returns>
    public static string ReplaceLineBreaksToSpace(string value) {
        // 步骤 1：先定位首个换行符；若不存在直接返回原字符串，避免额外分配。
        var firstBreakIndex = value.AsSpan().IndexOfAny('\r', '\n');
        if (firstBreakIndex < 0) {
            return value;
        }

        // 步骤 2：仅从首个换行位置开始单次遍历替换，减少无效扫描与分配。
        var buffer = value.ToCharArray();
        for (var index = firstBreakIndex; index < buffer.Length; index++) {
            if (buffer[index] is '\r' or '\n') {
                buffer[index] = ' ';
            }
        }

        // 步骤 3：返回归一化结果。
        return new string(buffer);
    }
}
