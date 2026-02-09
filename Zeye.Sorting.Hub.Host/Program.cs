using Zeye.Sorting.Hub.Host;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

/*
builder.Services.AddSortingHubPersistence(builder.Configuration);

// Host ∏∫‘∆Ù∂Ø±‡≈≈
builder.Services.AddHostedService<DatabaseInitializerHostedService>();*/

host.Run();
