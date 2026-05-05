using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;
using Zeye.Sorting.Hub.Contracts.Models.Diagnostics;

namespace Zeye.Sorting.Hub.Application.Services.Diagnostics;

/// <summary>
/// 慢查询画像查询应用服务。
/// </summary>
public sealed class GetSlowQueryProfileQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 慢查询画像存储。
    /// </summary>
    private readonly ISlowQueryProfileReader _profileReader;

    /// <summary>
    /// 初始化慢查询画像查询应用服务。
    /// </summary>
    /// <param name="profileReader">画像读取器。</param>
    public GetSlowQueryProfileQueryService(ISlowQueryProfileReader profileReader) {
        _profileReader = profileReader ?? throw new ArgumentNullException(nameof(profileReader));
    }

    /// <summary>
    /// 获取慢查询画像列表。
    /// </summary>
    /// <returns>画像列表响应。</returns>
    public SlowQueryProfileListResponse Execute() {
        try {
            var (snapshots, totalFingerprintCount) = _profileReader.GetTopProfiles();
            return new SlowQueryProfileListResponse {
                GeneratedAtLocal = DateTime.Now,
                TotalFingerprintCount = totalFingerprintCount,
                Items = snapshots.Select(MapToResponse).ToArray()
            };
        }
        catch (Exception exception) {
            Logger.Error(exception, "获取慢查询画像列表失败。");
            throw;
        }
    }

    /// <summary>
    /// 获取指定指纹的慢查询画像。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <returns>命中的画像快照；未命中返回 null。</returns>
    public SlowQueryProfileResponse? Execute(string fingerprint) {
        if (string.IsNullOrWhiteSpace(fingerprint)) {
            throw new ArgumentException("fingerprint 不能为空。", nameof(fingerprint));
        }

        var normalizedFingerprint = fingerprint.Trim().ToLowerInvariant();
        if (normalizedFingerprint.Length != 16 || !normalizedFingerprint.All(static character => Uri.IsHexDigit(character))) {
            throw new ArgumentException("fingerprint 必须为 16 位十六进制字符串。", nameof(fingerprint));
        }

        try {
            return _profileReader.TryGetProfile(normalizedFingerprint, out var snapshot)
                ? MapToResponse(snapshot!)
                : null;
        }
        catch (Exception exception) {
            Logger.Error(exception, "获取慢查询画像详情失败。Fingerprint={Fingerprint}", normalizedFingerprint);
            throw;
        }
    }

    /// <summary>
    /// 将内部快照映射为外部响应。
    /// </summary>
    /// <param name="snapshot">内部快照。</param>
    /// <returns>响应合同。</returns>
    private static SlowQueryProfileResponse MapToResponse(SlowQueryProfileReadModel snapshot) {
        return new SlowQueryProfileResponse {
            Fingerprint = snapshot.Fingerprint,
            NormalizedSql = snapshot.NormalizedSql,
            SampleSql = snapshot.SampleSql,
            CallCount = snapshot.CallCount,
            AverageElapsedMilliseconds = snapshot.AverageElapsedMilliseconds,
            P95Milliseconds = snapshot.P95Milliseconds,
            P99Milliseconds = snapshot.P99Milliseconds,
            MaxMilliseconds = snapshot.MaxMilliseconds,
            TimeoutCount = snapshot.TimeoutCount,
            ErrorCount = snapshot.ErrorCount,
            DeadlockCount = snapshot.DeadlockCount,
            TotalAffectedRows = snapshot.TotalAffectedRows,
            WindowStartedAtLocal = snapshot.WindowStartedAtLocal,
            WindowEndedAtLocal = snapshot.WindowEndedAtLocal,
            LastOccurredAtLocal = snapshot.LastOccurredAtLocal
        };
    }
}
