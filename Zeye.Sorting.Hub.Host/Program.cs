using Zeye.Sorting.Hub.Host;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSortingHubPersistence(builder.Configuration);

// Host 启动时执行持久化初始化
builder.Services.AddHostedService<DatabaseInitializerHostedService>();
builder.Services.AddHostedService<DatabaseAutoTuningHostedService>();

var host = builder.Build();

host.Run();
