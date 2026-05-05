namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

/// <summary>
/// 基线数据种子入口。
/// </summary>
public sealed class BaselineDataSeeder {
    /// <summary>
    /// 执行可选幂等种子入口。
    /// </summary>
    /// <param name="validationResult">校验结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新后的结果。</returns>
    public Task<BaselineDataValidationResult> SeedAsync(
        BaselineDataValidationResult validationResult,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(validationResult);
        cancellationToken.ThrowIfCancellationRequested();

        var seedMessages = validationResult.SeedMessages
            .Concat(["当前版本未定义需写入数据库的持久化默认数据，已跳过自动写入。"])
            .ToArray();
        return Task.FromResult(validationResult with {
            WasSeedAttempted = true,
            SeededRecordCount = 0,
            SeedMessages = seedMessages,
            Summary = "基线数据校验通过，已执行幂等种子入口（当前无需持久化写入）。"
        });
    }
}
