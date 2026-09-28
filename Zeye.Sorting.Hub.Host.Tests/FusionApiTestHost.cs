using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Application.Services.Idempotency;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;
using Zeye.Sorting.Hub.Infrastructure.Repositories;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>组合真实关系仓储和生产路由，供HTTP与本地浏览器验收复用。</summary>
public static class FusionApiTestHost {
    /// <summary>创建隔离测试API，不读取生产连接字符串或启动生产后台任务。</summary>
    public static async Task<WebApplication> CreateAsync(RelationalParcelTestDatabase database, bool useTestServer = true) {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        if (useTestServer) builder.WebHost.UseTestServer();
        else builder.WebHost.UseUrls("http://127.0.0.1:5098");
        builder.Services.AddSingleton<IParcelRepository>(database.Parcels);
        builder.Services.AddSingleton<IParcelProcessingRepository>(database.Processing);
        builder.Services.AddScoped<ParcelProcessingApplicationService>();
        builder.Services.AddScoped<GetParcelByIdQueryService>();
        builder.Services.AddScoped<GetParcelPagedQueryService>();
        builder.Services.AddScoped<GetParcelCursorPagedQueryService>();
        builder.Services.AddScoped<GetAdjacentParcelsQueryService>();
        builder.Services.AddSingleton<IDbContextFactory<SortingHubDbContext>>(database.Factory);
        builder.Services.AddSingleton<ParcelPartitionStore>(database.Partitions);
        builder.Services.AddOptions<ReadOnlyDatabaseOptions>();
        builder.Services.AddSingleton<ReportingQueryBudgetPlanner>();
        builder.Services.AddScoped<IParcelAnalyticsReadService, ParcelAnalyticsReadService>();
        builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        builder.Services.AddScoped<IdempotencyGuardService>();
        builder.Services.AddSingleton<IdempotencyKeyHasher>();
        builder.Services.AddScoped<CreateParcelCommandService>();
        builder.Services.AddScoped<UpdateParcelStatusCommandService>();
        builder.Services.AddScoped<DeleteParcelCommandService>();
        builder.Services.AddScoped<CleanupExpiredParcelsCommandService>();
        builder.Services.AddOptions<BufferedWriteOptions>().Configure(options => {
            options.IsEnabled = true; options.ChannelCapacity = 1000; options.BatchSize = 10;
            options.FlushIntervalMilliseconds = 20; options.BackpressureRejectThreshold = 900;
        });
        builder.Services.AddSingleton(new BoundedWriteChannel<BufferedParcelWriteItem>(1000));
        builder.Services.AddSingleton(new DeadLetterWriteStore(100));
        builder.Services.AddSingleton<ParcelBufferedWriteService>();
        builder.Services.AddSingleton<IBufferedWriteService>(services => services.GetRequiredService<ParcelBufferedWriteService>());
        builder.Services.AddSingleton<ParcelBatchWriteFlushService>();
        if (!useTestServer) builder.Services.AddHostedService<Zeye.Sorting.Hub.Host.HostedServices.ParcelBatchWriteFlushHostedService>();
        var app = builder.Build();
        app.MapParcelReadOnlyApis();
        app.MapParcelAnalyticsApis();
        app.MapParcelProcessingApis();
        app.MapParcelAdminApis();
        await app.StartAsync();
        return app;
    }
}
