// Infrastructure/Data/Outbox/OutboxMessage.cs
namespace YourNamespace.Infrastructure.Data.Outbox;

using System.Text.Json;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create<T>(T message, DateTimeOffset createdAt) where T : class
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = typeof(T).AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(message, JsonSerializerOptions),
            CreatedAt = createdAt
        };
    }

    public void MarkAsProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
        RetryCount++;
    }

    public object? Deserialize()
    {
        var type = System.Type.GetType(Type);
        return type is null ? null : JsonSerializer.Deserialize(Content, type, JsonSerializerOptions);
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

// Infrastructure/Data/Configurations/OutboxMessageConfiguration.cs
namespace YourNamespace.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YourNamespace.Infrastructure.Data.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        // Index for efficient polling of unprocessed messages
        builder.HasIndex(x => new { x.ProcessedAt, x.CreatedAt })
            .HasFilter("[ProcessedAt] IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Unprocessed");

        // Index for cleanup of old processed messages
        builder.HasIndex(x => x.ProcessedAt)
            .HasFilter("[ProcessedAt] IS NOT NULL")
            .HasDatabaseName("IX_OutboxMessages_Processed");
    }
}

// Infrastructure/Data/Interceptors/OutboxInterceptor.cs
namespace YourNamespace.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using YourNamespace.Domain.Common;
using YourNamespace.Infrastructure.Data.Outbox;

public sealed class OutboxInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void ConvertDomainEventsToOutboxMessages(DbContext context)
    {
        var now = timeProvider.GetUtcNow();

        // Get all entities that have domain events
        var entitiesWithEvents = context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        // Convert domain events to outbox messages
        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.PopDomainEvents())
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().AssemblyQualifiedName!,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    CreatedAt = now
                };

                context.Set<OutboxMessage>().Add(outboxMessage);
            }
        }
    }
}
