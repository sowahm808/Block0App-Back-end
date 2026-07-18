using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MindUnlocking.Infrastructure.Persistence;
using MindUnlocking.Workers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMindUnlockingInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
builder.Services.AddSerilog();
await builder.Build().RunAsync();
