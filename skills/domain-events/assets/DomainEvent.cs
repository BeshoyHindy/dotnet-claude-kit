// Domain/Common/IDomainEvent.cs
namespace YourApp.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

// Domain/Common/DomainEvent.cs
namespace YourApp.Domain.Common;

/// <summary>
/// Base record for domain events. Use records for immutability.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

// Domain/Common/Entity.cs
namespace YourApp.Domain.Common;

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
namespace YourApp.Application.Common.Interfaces;

using YourApp.Domain.Common;

/// <summary>
/// Handler for a specific domain event type.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

// Example events
namespace YourApp.Domain.Orders.Events;

using YourApp.Domain.Common;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber) : DomainEvent;

public sealed record OrderSubmittedEvent(
    Guid OrderId,
    decimal TotalAmount,
    int ItemCount) : DomainEvent;

public sealed record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    Guid? CancelledBy) : DomainEvent;

public sealed record OrderShippedEvent(
    Guid OrderId,
    string TrackingNumber,
    string Carrier) : DomainEvent;

public sealed record OrderDeliveredEvent(
    Guid OrderId,
    DateTimeOffset DeliveredAt) : DomainEvent;
