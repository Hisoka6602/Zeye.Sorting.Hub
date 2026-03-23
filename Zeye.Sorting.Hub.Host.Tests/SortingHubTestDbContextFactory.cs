using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Host.Tests 通用 InMemory DbContextFactory。
/// </summary>
internal sealed class SortingHubTestDbContextFactory : IDbContextFactory<SortingHubDbContext> {
    /// <summary>
    /// 保存 InMemory 测试数据库配置选项，供多次创建上下文时复用并保持同库数据可见。
    /// </summary>
    private readonly DbContextOptions<SortingHubDbContext> _options;

    /// <summary>
    /// 创建测试 DbContextFactory。
    /// </summary>
    /// <param name="options">数据库选项。</param>
    public SortingHubTestDbContextFactory(DbContextOptions<SortingHubDbContext> options) {
        _options = options;
    }

    /// <summary>
    /// 创建 DbContext。
    /// </summary>
    /// <returns>数据库上下文。</returns>
    public SortingHubDbContext CreateDbContext() {
        return new SortingHubDbContext(_options);
    }

    /// <summary>
    /// 异步创建 DbContext。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>数据库上下文。</returns>
    public Task<SortingHubDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
        return Task.FromResult(new SortingHubDbContext(_options));
    }
}
