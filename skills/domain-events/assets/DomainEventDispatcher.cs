// Application/Common/Interfaces/IDomainEventDispatcher.cs
namespace YourApp.Application.Common.Interfaces;

using YourApp.Domain.Common;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
}

// Infrastructure/Events/DomainEventDispatcher.cs
namespace YourApp.Infrastructure.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YourApp.Application.Common.Interfaces;
using YourApp.Domain.Common;

/// <summary>
/// Dispatches domain events to registered handlers.
/// </summary>
public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchEventAsync(domainEvent, ct);
        }
    }

    private async Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        logger.LogDebug(
            "Dispatching domain event {EventType} ({EventId})",
            eventType.Name,
            domainEvent.EventId);

        var handlers = serviceProvider.GetServices(handlerType).ToList();

        if (handlers.Count == 0)
        {
            logger.LogWarning("No handlers registered for {EventType}", eventType.Name);
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                var method = handlerType.GetMethod("HandleAsync")!;
                var task = (Task)method.Invoke(handler, [domainEvent, ct])!;
                await task;

                logger.LogDebug(
                    "Handler {HandlerType} processed {EventType}",
                    handler.GetType().Name,
                    eventType.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Handler {HandlerType} failed processing {EventType} ({EventId})",
                    handler.GetType().Name,
                    eventType.Name,
                    domainEvent.EventId);

                // Re-throw or handle based on your requirements
                throw;
            }
        }
    }
}

// Infrastructure/Data/Interceptors/DomainEventInterceptor.cs
namespace YourApp.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YourApp.Application.Common.Interfaces;
using YourApp.Domain.Common;

/// <summary>
/// Dispatches domain events after successful SaveChanges.
/// </summary>
public sealed class DomainEventInterceptor(
    IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        await DispatchDomainEventsAsync(eventData.Context, cancellationToken);

        return result;
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        if (eventData.Context is not null)
        {
            DispatchDomainEventsAsync(eventData.Context, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        return result;
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken ct)
    {
        var domainEvents = context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        if (domainEvents.Count > 0)
        {
            await dispatcher.DispatchAsync(domainEvents, ct);
        }
    }
}

// Registration in DependencyInjection.cs:
//
// services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
// services.AddScoped<DomainEventInterceptor>();
//
// // Register all handlers
// services.Scan(scan => scan
//     .FromAssemblyOf<OrderCreatedEventHandler>()
//     .AddClasses(c => c.AssignableTo(typeof(IDomainEventHandler<>)))
//     .AsImplementedInterfaces()
//     .WithScopedLifetime());
//
// services.AddDbContext<AppDbContext>((sp, options) =>
// {
//     options.UseSqlServer(connectionString)
//            .AddInterceptors(sp.GetRequiredService<DomainEventInterceptor>());
// });
