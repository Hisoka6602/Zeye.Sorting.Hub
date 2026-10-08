using System.Text;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>轻量 SQL 词法脱敏，保留引号标识符和优化器提示，避免长结构及转义字面量盲区。</summary>
internal static class SlowQuerySqlText {
    /// <summary>标识符字符；数字不能从未加引号的名称中剥离。</summary>
    private static bool Identifier(char value) => char.IsLetterOrDigit(value) || value == '_';
    /// <summary>移除注释后只保留一个词法边界，不让追踪标签数量造成不同指纹。</summary>
    private static void Space(StringBuilder output, bool normalize) { if (!normalize || output.Length > 0 && output[^1] != ' ') output.Append(' '); }
    /// <summary>跨过完整引号片段，支持成对引号和 MySQL 反斜杠转义。</summary>
    private static int QuotedEnd(string text, int start, char end, bool backslash) {
        for (var index = start + 1; index < text.Length; index++) {
            if (backslash && text[index] == '\\') { index++; continue; }
            if (text[index] != end) continue;
            if (index + 1 < text.Length && text[index + 1] == end) { index++; continue; }
            return index + 1;
        }
        return text.Length;
    }
    /// <summary>按数据库引号语义脱敏；归一化时只折叠标识符之外的大小写和空白。</summary>
    internal static string Transform(string text, bool normalize, string provider = "") {
        var output = new StringBuilder(text.Length);
        var mysql = provider.Contains("MySql", StringComparison.OrdinalIgnoreCase);
        for (var index = 0; index < text.Length;) {
            var value = text[index];
            if (value == '-' && index + 1 < text.Length && text[index + 1] == '-' || mysql && value == '#') {
                while (index < text.Length && text[index] != '\n' && text[index] != '\r') index++;
                Space(output, normalize); continue;
            }
            if (value == '/' && index + 1 < text.Length && text[index + 1] == '*') {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) { Space(output, normalize); break; }
                if (index + 2 < text.Length && text[index + 2] == '+') {
                    // 提示会影响执行计划，不能和仅用于追踪的注释一起删除。
                    output.Append("/*+").Append(Transform(text[(index + 3)..end], normalize, provider)).Append("*/");
                }
                else Space(output, normalize);
                index = end + 2; continue;
            }
            if ((value == 'q' || value == 'Q') && index + 2 < text.Length && text[index + 1] == '\'' &&
                (index == 0 || !Identifier(text[index - 1]))) {
                var delimiter = text[index + 2];
                var closing = delimiter switch { '[' => ']', '{' => '}', '(' => ')', '<' => '>', _ => delimiter };
                var end = text.IndexOf(closing + "'", index + 3, StringComparison.Ordinal);
                output.Append('?'); index = end < 0 ? text.Length : end + 2; continue;
            }
            if (value == '\'' || mysql && value == '"') {
                index = QuotedEnd(text, index, value, mysql); output.Append('?'); continue;
            }
            if (value is '"' or '`' or '[') {
                var end = QuotedEnd(text, index, value == '[' ? ']' : value, false);
                output.Append(text, index, end - index); index = end; continue;
            }
            if (normalize && value == '@' && index + 1 < text.Length && text[index + 1] == '@') {
                output.Append("@@"); index += 2;
                while (index < text.Length && Identifier(text[index])) output.Append(char.ToLowerInvariant(text[index++]));
                continue;
            }
            if (normalize && (value is '@' or ':' or '$' or '?') && (index == 0 || !Identifier(text[index - 1])) &&
                (value == '?' || index + 1 < text.Length && (char.IsLetter(text[index + 1]) || text[index + 1] == '_' || value == ':' && char.IsDigit(text[index + 1])))) {
                index++; while (index < text.Length && Identifier(text[index])) index++;
                output.Append('?'); continue;
            }
            if ((char.IsDigit(value) || value == '.' && index + 1 < text.Length && char.IsDigit(text[index + 1])) &&
                (index == 0 || !Identifier(text[index - 1]))) {
                if (value == '0' && index + 1 < text.Length && text[index + 1] is 'x' or 'X') {
                    index += 2; while (index < text.Length && Uri.IsHexDigit(text[index])) index++;
                }
                else {
                    while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.')) index++;
                    if (index + 1 < text.Length && text[index] is 'e' or 'E' && (char.IsDigit(text[index + 1]) || text[index + 1] is '+' or '-')) {
                        index++; if (index < text.Length && text[index] is '+' or '-') index++;
                        while (index < text.Length && char.IsDigit(text[index])) index++;
                    }
                }
                output.Append('?'); continue;
            }
            if (normalize && char.IsWhiteSpace(value)) {
                if (output.Length > 0 && output[^1] != ' ') output.Append(' ');
            }
            else output.Append(normalize ? char.ToLowerInvariant(value) : value);
            index++;
        }
        return normalize ? output.ToString().Trim() : output.ToString();
    }
}
