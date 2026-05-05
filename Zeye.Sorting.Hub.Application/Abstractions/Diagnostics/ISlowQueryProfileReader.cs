namespace Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;

/// <summary>
/// 慢查询画像只读接口。
/// </summary>
public interface ISlowQueryProfileReader {
    /// <summary>
    /// 获取当前窗口内的慢查询画像列表。
    /// </summary>
    /// <returns>画像列表与总量。</returns>
    (IReadOnlyList<SlowQueryProfileReadModel> Items, int TotalFingerprintCount) GetTopProfiles();

    /// <summary>
    /// 按指纹读取慢查询画像。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <param name="profile">画像读模型。</param>
    /// <returns>是否命中。</returns>
    bool TryGetProfile(string fingerprint, out SlowQueryProfileReadModel? profile);
}
