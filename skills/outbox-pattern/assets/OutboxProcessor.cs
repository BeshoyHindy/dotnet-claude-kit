// Infrastructure/BackgroundJobs/OutboxProcessor.cs
namespace YourApp.Infrastructure.BackgroundJobs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YourApp.Application.Common.Interfaces;
using YourApp.Infrastructure.Data.Outbox;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessOutboxMessagesAsync(stoppingToken);

                if (processedCount > 0)
                {
                    logger.LogDebug("Processed {Count} outbox messages", processedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        logger.LogInformation("Outbox processor stopped");
    }

    private async Task<int> ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return 0;

        var processedCount = 0;

        foreach (var message in messages)
        {
            try
            {
                var @event = message.Deserialize();
                if (@event is null)
                {
                    logger.LogWarning(
                        "Could not deserialize outbox message {Id} of type {Type}",
                        message.Id,
                        message.Type);
                    message.MarkAsFailed("Deserialization failed");
                    continue;
                }

                await publisher.PublishAsync(@event, ct);
                message.MarkAsProcessed(timeProvider.GetUtcNow());
                processedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to process outbox message {Id}, attempt {Attempt}",
                    message.Id,
                    message.RetryCount + 1);

                message.MarkAsFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);

        return processedCount;
    }
}

// Infrastructure/BackgroundJobs/OutboxCleanupJob.cs
namespace YourApp.Infrastructure.BackgroundJobs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YourApp.Application.Common.Interfaces;

public sealed class OutboxCleanupJob(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxCleanupJob> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit before first cleanup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupProcessedMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning up outbox messages");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    private async Task CleanupProcessedMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContext>();

        var cutoff = DateTimeOffset.UtcNow - _retentionPeriod;

        // Delete old processed messages
        var deletedProcessed = await db.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        // Delete failed messages that have exceeded max retries and are old
        var deletedFailed = await db.OutboxMessages
            .Where(m => m.RetryCount >= 3 && m.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedProcessed > 0 || deletedFailed > 0)
        {
            logger.LogInformation(
                "Outbox cleanup: deleted {Processed} processed and {Failed} failed messages",
                deletedProcessed,
                deletedFailed);
        }
    }
}

// Application/Common/Interfaces/IEventPublisher.cs
namespace YourApp.Application.Common.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(object @event, CancellationToken ct = default);
}

// Registration in Program.cs:
//
// builder.Services.AddSingleton(TimeProvider.System);
// builder.Services.AddScoped<OutboxInterceptor>();
// builder.Services.AddHostedService<OutboxProcessor>();
// builder.Services.AddHostedService<OutboxCleanupJob>();
// builder.Services.AddScoped<IEventPublisher, YourEventPublisher>();
//
// builder.Services.AddDbContext<AppDbContext>((sp, options) =>
// {
//     options.UseSqlServer(connectionString)
//            .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>());
// });
