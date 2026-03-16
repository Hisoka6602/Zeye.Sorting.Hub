using Polly;
using System;
using System.Linq;
using System.Text;
using Polly.Retry;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库初始化后台服务：启动时迁移 + 可选方言初始化
    /// </summary>
    public sealed class DatabaseInitializerHostedService : IHostedService {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseInitializerHostedService> _logger;
        private readonly IDatabaseDialect _dialect;

        private readonly AsyncRetryPolicy _retryPolicy;

        public DatabaseInitializerHostedService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseInitializerHostedService> logger,
            IDatabaseDialect dialect) {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dialect = dialect;

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 6,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Min(30, 2 * attempt)),
                    onRetry: (ex, ts, attempt, _) => {
                        _logger.LogWarning(ex,
                            "数据库初始化重试中，Attempt={Attempt}, DelaySeconds={DelaySeconds}, Provider={Provider}",
                            attempt, ts.TotalSeconds, _dialect.ProviderName);
                    });
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            await _retryPolicy.ExecuteAsync(async () => {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();

                _logger.LogInformation("开始执行数据库迁移，Provider={Provider}", _dialect.ProviderName);
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("数据库迁移完成，Provider={Provider}", _dialect.ProviderName);

                await AssertMigrationConsistencyAsync(db, cancellationToken);

                foreach (var sql in _dialect.GetOptionalBootstrapSql()) {
                    try {
                        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                    }
                    catch (Exception ex) {
                        _logger.LogWarning(ex,
                            "可选初始化 SQL 执行失败，已降级忽略，Provider={Provider}, Sql={Sql}",
                            _dialect.ProviderName, sql);
                    }
                }
            });
        }

        /// <summary>
        /// 迁移一致性守卫：确保 CodeFirst 模型与数据库迁移历史保持同步。
        /// </summary>
        /// <remarks>
        /// <para>
        /// EF Core 的 <see cref="DatabaseFacade.MigrateAsync"/> 仅依赖 <c>__EFMigrationsHistory</c> 表来判断
        /// 哪些迁移已应用，因此存在以下风险场景：
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     有人手工执行了 <c>ALTER TABLE / DROP TABLE</c> 等 DDL，但未修改 <c>__EFMigrationsHistory</c>；
        ///     此时 EF Core 认为迁移已完成，但实际表结构已偏离代码模型。
        ///   </description></item>
        ///   <item><description>
        ///     数据库被回滚/还原到旧快照，但迁移历史表仍记录了较新的迁移版本。
        ///   </description></item>
        /// </list>
        /// <para>
        /// 本方法通过以下检查尽早发现不一致，维护 CodeFirst 原则：
        /// </para>
        /// <list type="number">
        ///   <item><description>
        ///     调用 <c>GetPendingMigrationsAsync()</c>：若 <c>MigrateAsync()</c> 后仍有未应用迁移，
        ///     说明存在异常，立即抛出异常阻止启动。
        ///   </description></item>
        ///   <item><description>
        ///     对比代码程序集中的迁移总数与 <c>__EFMigrationsHistory</c> 中的记录数：
        ///     若记录数多于代码（迁移历史被外部污染）或少于代码（迁移历史记录丢失），记录 Critical 日志。
        ///   </description></item>
        /// </list>
        /// <para>
        /// ⚠️ 本方法无法自动修复手工 DDL 导致的表结构偏差。若检测到偏差，应通过 <c>dotnet ef migrations add</c>
        /// 生成新迁移来对齐数据库与代码模型，而非直接修改数据库。
        /// </para>
        /// </remarks>
        private async Task AssertMigrationConsistencyAsync(
            SortingHubDbContext db, CancellationToken cancellationToken) {

            // 检查 1：MigrateAsync 之后不应再有待应用迁移
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0) {
                throw new InvalidOperationException(
                    $"[CodeFirst 守卫] MigrateAsync 完成后仍存在 {pending.Count} 个未应用迁移，" +
                    $"可能是并发写入或迁移文件缺失导致的不一致状态。" +
                    $"未应用迁移：{string.Join(", ", pending)}");
            }

            // 检查 2：__EFMigrationsHistory 记录数应与代码中定义的迁移总数一致
            var allMigrationsInCode = db.Database.GetMigrations().ToList();
            var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            if (appliedMigrations.Count != allMigrationsInCode.Count) {
                // 记录数多于代码：可能有外部写入或历史表被污染
                // 记录数少于代码：迁移历史丢失（例如数据库被还原后 MigrateAsync 意外未执行）
                _logger.LogCritical(
                    "[CodeFirst 守卫] 迁移一致性异常：代码中定义了 {CodeCount} 个迁移，" +
                    "但 __EFMigrationsHistory 中记录了 {AppliedCount} 个。" +
                    "若数据库曾被手工 DDL 修改或还原，请通过 'dotnet ef migrations add' 生成新迁移以对齐模型，" +
                    "切勿直接修改数据库结构，以维护 CodeFirst 原则。" +
                    "Provider={Provider}",
                    allMigrationsInCode.Count,
                    appliedMigrations.Count,
                    _dialect.ProviderName);
            }
            else {
                _logger.LogInformation(
                    "[CodeFirst 守卫] 迁移一致性验证通过：共 {Count} 个迁移均已应用，Provider={Provider}",
                    appliedMigrations.Count,
                    _dialect.ProviderName);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
