using System.Text.RegularExpressions;

namespace Zeye.Sorting.Hub.Host.Hubs;

/// <summary>实时只读请求的封闭白名单；不接受写操作、任意代理、账号或文件接口。</summary>
internal static partial class RealtimeReadPolicy {
    /// <summary>不包含路径参数的可订阅读取接口。</summary>
    private static readonly HashSet<string> Paths = new(StringComparer.Ordinal) {
        "/api/parcels", "/api/parcels/cursor", "/api/parcels/analytics", "/api/parcels/adjacent", "/api/parcels/processing-records/unbound",
        "/api/data-governance/archive-tasks", "/api/diagnostics/slow-queries", "/api/audit/web-requests",
        "/api/operations/partitions", "/api/operations/configuration", "/api/operations/configuration/policy",
        "/api/operations/rules/parcel", "/api/operations/rules/exception", "/api/operations/backup", "/api/operations/backup/artifacts",
        "/health/live", "/health/ready", "/health/deep"
    };

    /// <summary>检查本地路径边界；查询参数仍接受正常的 URL 编码。</summary>
    internal static bool IsAllowed(string? path) {
        if (string.IsNullOrEmpty(path) || path.Length > 4096 || path[0] != '/' || path.Contains("//", StringComparison.Ordinal)
            || path.Contains('\\') || path.Contains('#') || path.Any(char.IsControl)) return false;
        var route = path.Split('?', 2)[0];
        if (route.Contains('%') || route.Contains("..", StringComparison.Ordinal)) return false;
        return Paths.Contains(route) || ParcelPath().IsMatch(route) || AuditPath().IsMatch(route) || SlowQueryPath().IsMatch(route);
    }

    /// <summary>包裹详情和图片目录只接受正整数主键。</summary>
    [GeneratedRegex("^/api/parcels/[1-9][0-9]{0,18}(/images)?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ParcelPath();
    /// <summary>审计详情只接受正整数主键。</summary>
    [GeneratedRegex("^/api/audit/web-requests/[1-9][0-9]{0,18}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex AuditPath();
    /// <summary>慢查询详情只接受安全指纹字符。</summary>
    [GeneratedRegex("^/api/diagnostics/slow-queries/[a-zA-Z0-9_-]{1,128}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SlowQueryPath();
}
