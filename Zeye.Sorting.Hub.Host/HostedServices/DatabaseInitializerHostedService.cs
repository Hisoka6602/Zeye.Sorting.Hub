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

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
