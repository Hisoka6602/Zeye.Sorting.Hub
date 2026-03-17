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
        private const string FailStartupOnMigrationErrorConfigKey = "Persistence:Migration:FailStartupOnError";
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseInitializerHostedService> _logger;
        private readonly IDatabaseDialect _dialect;
        private readonly bool _failStartupOnMigrationError;

        private readonly AsyncRetryPolicy _retryPolicy;

        public DatabaseInitializerHostedService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseInitializerHostedService> logger,
            IDatabaseDialect dialect,
            IConfiguration configuration) {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dialect = dialect;
            _failStartupOnMigrationError = ResolveFailStartupOnMigrationError(configuration);

            _retryPolicy = Policy
                .Handle<Exception>(ex => ex is not OperationCanceledException)
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
            try {
                await _retryPolicy.ExecuteAsync(async (ct) => {
                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();

                    _logger.LogInformation("开始执行数据库迁移，Provider={Provider}", _dialect.ProviderName);
                    await db.Database.MigrateAsync(ct);
                    _logger.LogInformation("数据库迁移完成，Provider={Provider}", _dialect.ProviderName);

                    await AssertMigrationConsistencyAsync(db, ct);

                    foreach (var sql in _dialect.GetOptionalBootstrapSql()) {
                        try {
                            await db.Database.ExecuteSqlRawAsync(sql, ct);
                        }
                        catch (Exception ex) {
                            _logger.LogWarning(ex,
                                "可选初始化 SQL 执行失败，已降级忽略，Provider={Provider}, Sql={Sql}",
                                _dialect.ProviderName, sql);
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException) {
                // 宿主正常停止时触发，不计为错误
                _logger.LogInformation("数据库初始化已因取消令牌中止，Provider={Provider}", _dialect.ProviderName);
            }
            catch (Exception ex) {
                // 重试耗尽或不可恢复异常：按配置决定是否阻断启动。
                if (_failStartupOnMigrationError) {
                    _logger.LogCritical(ex,
                        "[数据库初始化] 所有重试均失败，数据库连接不可用，且已启用 FailStartupOnError，应用将终止启动。" +
                        "请检查连接字符串与数据库服务状态，Provider={Provider}, ConfigKey={ConfigKey}",
                        _dialect.ProviderName,
                        FailStartupOnMigrationErrorConfigKey);
                    throw;
                }

                // 降级模式行为：
                //   - 应用继续运行，IHostedService 生命周期正常完成
                //   - 后续业务请求若访问数据库，将在 Repository/DbContext 层收到
                //     连接异常（如 MySqlException / SqlException），需在业务层自行处理
                //   - 应尽快修复数据库连接问题并重启应用以恢复正常
                //   - 请监控 logs/database-*.log 文件中的 Critical/Error 日志
                _logger.LogCritical(ex,
                    "[数据库初始化] 所有重试均失败，数据库连接不可用，服务将以降级模式运行。" +
                    "请检查连接字符串与数据库服务状态，Provider={Provider}",
                    _dialect.ProviderName);
            }
        }

        /// <summary>
        /// 解析“迁移失败是否阻断启动”配置。
        /// </summary>
        /// <remarks>
        /// 约定：
        /// <list type="bullet">
        ///   <item><description>未配置时默认 <c>false</c>，保持历史“降级启动”行为。</description></item>
        ///   <item><description>仅当配置值可解析且为 <c>true</c> 时，迁移失败才会阻断启动。</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// 返回 <c>true</c> 表示迁移失败时阻断启动；返回 <c>false</c> 表示迁移失败后降级运行。
        /// </returns>
        internal static bool ResolveFailStartupOnMigrationError(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);
            var raw = configuration[FailStartupOnMigrationErrorConfigKey];
            return bool.TryParse(raw, out var value) && value;
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
        ///     说明存在异常，记录 Critical 日志。
        ///   </description></item>
        ///   <item><description>
        ///     对比代码程序集中的迁移总数与 <c>__EFMigrationsHistory</c> 中的记录数：
        ///     若记录数多于代码（迁移历史被外部污染）或少于代码（迁移历史记录丢失），记录 Critical 日志。
        ///   </description></item>
        ///   <item><description>
        ///     调用 EF Core 9+ 提供的 <c>HasPendingModelChanges()</c>：检测当前代码模型是否存在
        ///     尚未通过 <c>dotnet ef migrations add</c> 生成迁移的变更（即模型快照与实体模型不一致），
        ///     若存在则记录 Critical 日志。这是 EF Core 8 所不具备的模型级一致性检测能力。
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
                // 迁移后仍有未应用项：记录 Critical 日志，继续执行后续可选 SQL。
                // 实际迁移 (MigrateAsync) 已运行完毕，该情况通常发生在：
                //   - 迁移文件在部署时被意外删除（History 表有记录但文件不存在）
                //   - 并发部署竞争导致 History 记录异常
                // 不抛出异常，以免阻止应用启动；但需运维人员关注 Critical 日志并排查原因。
                _logger.LogCritical(
                    "[CodeFirst 守卫] MigrateAsync 完成后仍存在 {PendingCount} 个未应用迁移，" +
                    "可能是并发写入或迁移文件缺失导致的不一致状态。" +
                    "未应用迁移：{PendingMigrations}，Provider={Provider}",
                    pending.Count,
                    string.Join(", ", pending),
                    _dialect.ProviderName);
                return;
            }

            // 检查 2：__EFMigrationsHistory 记录数应与代码中定义的迁移总数一致
            var allMigrationsInCode = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

            // 在代码中定义但尚未在 __EFMigrationsHistory 中记录（通常不应在 MigrateAsync 后出现）
            var inCodeNotApplied = allMigrationsInCode.Except(appliedMigrations).OrderBy(static m => m).ToList();
            // 在 __EFMigrationsHistory 中记录但在代码中已不存在（可能是迁移文件被删除或历史表被外部污染）
            var appliedNotInCode = appliedMigrations.Except(allMigrationsInCode).OrderBy(static m => m).ToList();

            if (inCodeNotApplied.Count > 0 || appliedNotInCode.Count > 0) {
                var sb = new StringBuilder();
                sb.Append($"[CodeFirst 守卫] 迁移一致性异常：代码中定义了 {allMigrationsInCode.Count} 个迁移，");
                sb.Append($"__EFMigrationsHistory 中记录了 {appliedMigrations.Count} 个。");

                if (inCodeNotApplied.Count > 0) {
                    sb.AppendLine();
                    sb.Append($"  ▸ 代码中存在但未应用（{inCodeNotApplied.Count} 个）：");
                    sb.Append(string.Join(", ", inCodeNotApplied));
                }

                if (appliedNotInCode.Count > 0) {
                    sb.AppendLine();
                    sb.Append($"  ▸ 已应用但代码中不存在（{appliedNotInCode.Count} 个，可能为手工写入或迁移文件已删除）：");
                    sb.Append(string.Join(", ", appliedNotInCode));
                }

                sb.AppendLine();
                sb.Append("请通过 'dotnet ef migrations add' 生成新迁移以对齐模型，切勿直接修改数据库结构，以维护 CodeFirst 原则。");

                _logger.LogCritical(
                    "{Message} Provider={Provider}",
                    sb.ToString(),
                    _dialect.ProviderName);
            }
            else {
                _logger.LogInformation(
                    "[CodeFirst 守卫] 迁移一致性验证通过：共 {Count} 个迁移均已应用，Provider={Provider}",
                    appliedMigrations.Count,
                    _dialect.ProviderName);
            }

            // 检查 3（EF Core 9+）：检测代码模型是否存在尚未生成迁移的变更
            // HasPendingModelChanges() 对比当前 DbContext 的实体模型与最新迁移快照（ModelSnapshot），
            // 可发现手工修改实体类/配置后忘记执行 dotnet ef migrations add 的情况，
            // 这是 EF Core 8 所不具备的能力——EF Core 9 首次提供此 API。
            if (db.Database.HasPendingModelChanges()) {
                _logger.LogCritical(
                    "[CodeFirst 守卫] 检测到代码模型存在尚未生成迁移的变更（HasPendingModelChanges=true）。" +
                    "当前实体模型与最新迁移快照不一致，请执行 'dotnet ef migrations add <名称>' 生成新迁移，" +
                    "以维护 CodeFirst 原则。Provider={Provider}",
                    _dialect.ProviderName);
            }
            else {
                _logger.LogInformation(
                    "[CodeFirst 守卫] 模型变更检测通过（HasPendingModelChanges=false）：代码模型与迁移快照完全一致，Provider={Provider}",
                    _dialect.ProviderName);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
