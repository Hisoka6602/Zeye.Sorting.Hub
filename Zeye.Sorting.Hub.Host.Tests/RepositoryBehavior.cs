namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 测试仓储行为模式。
/// </summary>
public enum RepositoryBehavior {
    /// <summary>
    /// 成功写入。
    /// </summary>
    Success = 0,

    /// <summary>
    /// 返回失败结果。
    /// </summary>
    ReturnFailure = 1,

    /// <summary>
    /// 抛出异常。
    /// </summary>
    ThrowException = 2
}
