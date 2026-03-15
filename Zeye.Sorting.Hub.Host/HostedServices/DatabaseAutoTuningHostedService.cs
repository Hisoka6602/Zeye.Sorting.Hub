using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库自动调谐后台服务：慢查询分析 + 自动动作执行
    /// </summary>
    public sealed class DatabaseAutoTuningHostedService : BackgroundService {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseAutoTuningHostedService> _logger;
        private readonly IDatabaseDialect _dialect;
        private readonly SlowQueryAutoTuningPipeline _pipeline;
        private readonly int _analyzeIntervalSeconds;

        public DatabaseAutoTuningHostedService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseAutoTuningHostedService> logger,
            IDatabaseDialect dialect,
            SlowQueryAutoTuningPipeline pipeline,
            IConfiguration configuration) {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dialect = dialect;
            _pipeline = pipeline;
            _analyzeIntervalSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AnalyzeIntervalSeconds", 30);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_analyzeIntervalSeconds), stoppingToken);

                var actions = _pipeline.BuildActions(_dialect, _logger);
                if (actions.Count == 0) {
                    continue;
                }

                await using var scope = _serviceProvider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();

                foreach (var action in actions) {
                    try {
                        await db.Database.ExecuteSqlRawAsync(action, stoppingToken);
                        _logger.LogInformation("自动调谐动作执行成功，Provider={Provider}, Sql={Sql}", _dialect.ProviderName, action);
                    }
                    catch (Exception ex) {
                        if (_dialect.ShouldIgnoreAutoTuningException(ex)) {
                            _logger.LogInformation(ex,
                                "自动调谐动作命中可忽略异常，已跳过，Provider={Provider}, Sql={Sql}",
                                _dialect.ProviderName,
                                action);
                            continue;
                        }

                        _logger.LogWarning(ex,
                            "自动调谐动作执行失败，已降级忽略，Provider={Provider}, Sql={Sql}",
                            _dialect.ProviderName,
                            action);
                    }
                }
            }
        }

        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }
    }
}
