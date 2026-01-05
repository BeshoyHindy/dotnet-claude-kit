// Copy to: src/Domain/Common/*.cs (IDomainEvent.cs, DomainEvent.cs, Entity.cs)
// Requires: None (pure C#)
// Domain/Common/IDomainEvent.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

// Domain/Common/DomainEvent.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Base record for domain events. Use records for immutability.
/// OccurredOn is set via constructor to support TimeProvider injection.
/// </summary>
public abstract record DomainEvent(DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

// Alternative: If you need a parameterless base, use a factory
public static class DomainEventFactory
{
    /// <summary>
    /// Creates domain events with TimeProvider for testability.
    /// </summary>
    public static TEvent Create<TEvent>(TimeProvider timeProvider, Func<DateTimeOffset, TEvent> factory)
        where TEvent : IDomainEvent
    {
        return factory(timeProvider.GetUtcNow());
    }
}

// Domain/Common/Entity.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Base entity class with domain event support.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Domain events raised by this entity.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raises a domain event.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears and returns all domain events.
    /// Called by infrastructure after dispatching.
    /// </summary>
    public IReadOnlyList<IDomainEvent> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    /// <summary>
    /// Clears domain events without returning them.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// Application/Common/Interfaces/IDomainEventHandler.cs
namespace YourNamespace.Application.Common.Interfaces;

using YourNamespace.Domain.Common;

/// <summary>
/// Handler for a specific domain event type.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

// Example events - pass OccurredOn from TimeProvider
namespace YourNamespace.Domain.Orders.Events;

using YourNamespace.Domain.Common;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

public sealed record OrderSubmittedEvent(
    Guid OrderId,
    decimal TotalAmount,
    int ItemCount,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

public sealed record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    Guid? CancelledBy,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

public sealed record OrderShippedEvent(
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

public sealed record OrderDeliveredEvent(
    Guid OrderId,
    DateTimeOffset DeliveredAt,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
