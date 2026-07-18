using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MindUnlocking.Workers;

public sealed class OutboxDispatchWorker(ILogger<OutboxDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Outbox dispatch tick");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
