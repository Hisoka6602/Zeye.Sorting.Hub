using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.DependencyInjection {

    /// <summary>
    /// 持久化模块注册扩展（仅负责能力注册，不负责进程启动编排）
    /// </summary>
    public static class PersistenceServiceCollectionExtensions {

        /// <summary>
        /// 针对“无天然时间字段/时间字段可为空”的属性表，采用固定哈希分表。
        /// </summary>
        /// <remarks>
        /// 说明：
        /// 1) 这些表若直接按可空时间分表，插入时可能出现路由不稳定或无法命中分表；
        /// 2) 采用哈希分表可以确保“具备分表能力”，并在数据持续增长时横向分散压力；
        /// 3) 取模值保持温和，兼顾分散效果与运维复杂度。
        /// </remarks>
        private const int ParcelRelatedHashShardingMod = 16;

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
                        .UseDatabase(connectionString, DatabaseType.MySql, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime);
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
                        .UseDatabase(connectionString, DatabaseType.SqlServer, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime);
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

        /// <summary>
        /// 统一注册 Parcel 主表与属性表的分表规则。
        /// </summary>
        /// <param name="shardingBuilder">分表构建器。</param>
        /// <param name="parcelShardingStartTime">Parcel 月分表起始时间。</param>
        /// <remarks>
        /// 设计目标：
        /// - Parcel 主表已经采用按月分表；
        /// - 与 Parcel 同步增长的属性表也必须具备分表能力，避免单表无限膨胀；
        /// - 对“有稳定必填时间字段”的表优先采用按月分表；
        /// - 对“无时间字段/时间可空”的表采用哈希分表，保证可路由且可扩展。
        /// </remarks>
        private static void ConfigureParcelAggregateSharding(IShardingBuilder shardingBuilder, DateTime parcelShardingStartTime) {
            // ------------------------------
            // 1) 主表：Parcel（按月分表）
            // ------------------------------
            shardingBuilder.SetDateSharding<Parcel>(
                shardingField: nameof(Parcel.CreatedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 2) 独立属性实体：BagInfo
            // ------------------------------
            // BagInfo 的 BaggingTime 可为空，采用 BagCode 哈希分表以保证路由稳定。
            shardingBuilder.SetHashModSharding<BagInfo>(
                shardingField: nameof(BagInfo.BagCode),
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 3) 一对一属性表（OwnsOne）
            // ------------------------------
            // 具备必填时间字段 -> 按月分表
            shardingBuilder.SetDateSharding<VolumeInfo>(
                shardingField: nameof(VolumeInfo.MeasurementTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<ChuteInfo>(
                shardingField: nameof(ChuteInfo.LandedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<SorterCarrierInfo>(
                shardingField: nameof(SorterCarrierInfo.LoadedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<GrayDetectorInfo>(
                shardingField: nameof(GrayDetectorInfo.ResultTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // 无稳定时间字段或时间可空 -> 按 ParcelId 哈希分表
            shardingBuilder.SetHashModSharding<ParcelDeviceInfo>(
                shardingField: "ParcelId",
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<ParcelPositionInfo>(
                shardingField: "ParcelId",
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<StickingParcelInfo>(
                shardingField: "ParcelId",
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 4) 一对多属性表（OwnsMany）
            // ------------------------------
            // 具备必填时间字段 -> 按月分表
            shardingBuilder.SetDateSharding<ApiRequestInfo>(
                shardingField: nameof(ApiRequestInfo.RequestTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<CommandInfo>(
                shardingField: nameof(CommandInfo.GeneratedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<WeightInfo>(
                shardingField: nameof(WeightInfo.WeighingTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: parcelShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // 可空时间或无时间字段 -> 按 ParcelId 哈希分表
            shardingBuilder.SetHashModSharding<BarCodeInfo>(
                shardingField: "ParcelId",
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<ImageInfo>(
                shardingField: "ParcelId",
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<VideoInfo>(
                shardingField: "ParcelId",
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
        }

        private static DateTime GetShardingStartTime(IConfiguration configuration) {
            var configured = configuration["Persistence:Sharding:ParcelStartTime"];
            if (DateTime.TryParse(
                configured,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed)) {
                return parsed.Kind switch {
                    DateTimeKind.Unspecified => DateTime.SpecifyKind(parsed, DateTimeKind.Local),
                    DateTimeKind.Local => parsed,
                    _ => parsed.ToLocalTime()
                };
            }

            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);
        }
    }
}
