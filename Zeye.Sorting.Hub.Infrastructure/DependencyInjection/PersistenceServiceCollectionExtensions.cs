using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.DependencyInjection {

    /// <summary>
    /// 持久化模块注册扩展（仅负责能力注册，不负责进程启动编排）
    /// </summary>
    public static class PersistenceServiceCollectionExtensions {

        public static IServiceCollection AddSortingHubPersistence(this IServiceCollection services, IConfiguration configuration) {
            var provider = configuration["Persistence:Provider"];
            var commandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            var minCommandElapsedMilliseconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MinCommandElapsedMilliseconds", 50);
            var parcelShardingStartTime = GetShardingStartTime(configuration);

            if (string.IsNullOrWhiteSpace(provider)) {
                throw new InvalidOperationException("缺少配置：Persistence:Provider，可选值：MySql / SqlServer");
            }

            if (string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase)) {
                var connectionString = configuration.GetConnectionString("MySql");
                if (string.IsNullOrWhiteSpace(connectionString)) {
                    throw new InvalidOperationException("缺少连接字符串：ConnectionStrings:MySql");
                }

                // DbContextPool：更低分配、更稳吞吐
                services.AddDbContextPool<SortingHubDbContext>(static (sp, options) => {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var cs = cfg.GetConnectionString("MySql")!;
                    var commandTimeoutSeconds = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);

                    // 建议：生产环境可改为固定版本，避免探测失败导致启动失败
                    var serverVersion = ServerVersion.AutoDetect(cs);

                    options.UseMySql(cs, serverVersion, mySqlOptions => {
                        // 迁移程序集通常指向 Host 或 Infrastructure，按你迁移放置位置调整
                        // mySqlOptions.MigrationsAssembly("Zeye.Sorting.Hub.Host");
                        mySqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                        mySqlOptions.CommandTimeout(commandTimeoutSeconds);
                    });

                    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                    // 开发环境建议开启，生产建议关闭
                    // options.EnableSensitiveDataLogging();
                    // options.EnableDetailedErrors();
                });

                services.AddEFCoreSharding(shardingBuilder => {
                    shardingBuilder
                        .SetEntityAssemblies(typeof(SortingHubDbContext).Assembly)
                        .SetCommandTimeout(commandTimeoutSeconds)
                        .SetMinCommandElapsedMilliseconds(minCommandElapsedMilliseconds)
                        .CreateShardingTableOnStarting(false)
                        .UseDatabase(connectionString, DatabaseType.MySql, typeof(Parcel).Namespace!, static _ => { })
                        .SetDateSharding<Parcel>(
                            shardingField: nameof(Parcel.ScannedTime),
                            expandByDateMode: ExpandByDateMode.PerMonth,
                            startTime: parcelShardingStartTime,
                            sourceName: ShardingConstant.DefaultSource);
                });

                services.AddSingleton<IDatabaseDialect, MySqlDialect>();
            }
            else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase)) {
                var connectionString = configuration.GetConnectionString("SqlServer");
                if (string.IsNullOrWhiteSpace(connectionString)) {
                    throw new InvalidOperationException("缺少连接字符串：ConnectionStrings:SqlServer");
                }

                services.AddDbContextPool<SortingHubDbContext>(static (sp, options) => {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var cs = cfg.GetConnectionString("SqlServer")!;
                    var commandTimeoutSeconds = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);

                    options.UseSqlServer(cs, sqlServerOptions => {
                        // 迁移程序集通常指向 Host 或 Infrastructure，按你迁移放置位置调整
                        // sqlServerOptions.MigrationsAssembly("Zeye.Sorting.Hub.Host");
                        sqlServerOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                        sqlServerOptions.CommandTimeout(commandTimeoutSeconds);
                    });

                    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                });

                services.AddEFCoreSharding(shardingBuilder => {
                    shardingBuilder
                        .SetEntityAssemblies(typeof(SortingHubDbContext).Assembly)
                        .SetCommandTimeout(commandTimeoutSeconds)
                        .SetMinCommandElapsedMilliseconds(minCommandElapsedMilliseconds)
                        .CreateShardingTableOnStarting(false)
                        .UseDatabase(connectionString, DatabaseType.SqlServer, typeof(Parcel).Namespace!, static _ => { })
                        .SetDateSharding<Parcel>(
                            shardingField: nameof(Parcel.ScannedTime),
                            expandByDateMode: ExpandByDateMode.PerMonth,
                            startTime: parcelShardingStartTime,
                            sourceName: ShardingConstant.DefaultSource);
                });

                services.AddSingleton<IDatabaseDialect, SqlServerDialect>();
            }
            else {
                throw new InvalidOperationException($"不支持的数据库类型：{provider}，可选值：MySql / SqlServer");
            }

            // 仓储注册（示例）：按你现有 RepositoryBase/MemoryCacheRepositoryBase 的实际泛型/接口进行补齐
            // services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));

            return services;
        }

        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        private static DateTime GetShardingStartTime(IConfiguration configuration) {
            var configured = configuration["Persistence:Sharding:ParcelStartTime"];
            if (DateTime.TryParse(configured, out var parsed)) {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
