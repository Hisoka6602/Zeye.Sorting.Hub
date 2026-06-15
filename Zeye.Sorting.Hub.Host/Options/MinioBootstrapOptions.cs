namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// MinIO 启动期自检配置。
/// </summary>
public sealed class MinioBootstrapOptions {
    /// <summary>
    /// 是否检查并补齐 Bucket。
    /// 可填写范围：true / false。
    /// </summary>
    public bool EnsureBucketsExist { get; set; }

    /// <summary>
    /// 是否仅执行 dry-run。
    /// 可填写范围：true / false。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// 是否启用危险动作守卫。
    /// 可填写范围：true / false。
    /// </summary>
    public bool EnableGuard { get; set; } = true;

    /// <summary>
    /// 是否允许真实执行危险动作。
    /// 可填写范围：当前仅允许 false。
    /// </summary>
    public bool AllowDangerousActionExecution { get; set; }

    /// <summary>
    /// 校验当前启动期自检配置是否安全。
    /// </summary>
    /// <param name="options">待校验配置。</param>
    /// <returns>安全时返回 <see langword="true"/>。</returns>
    public static bool IsSafeConfiguration(MinioBootstrapOptions? options) {
        if (options is null) {
            return false;
        }

        if (options.AllowDangerousActionExecution) {
            return false;
        }

        return !options.EnsureBucketsExist || (options.DryRun && options.EnableGuard);
    }
}
