// Domain/Common/ISoftDeletable.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Contract for entities that support soft deletion.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedOn { get; }
    string? DeletedBy { get; }
}

// Domain/Common/SoftDeletableEntity.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Base class for entities that support soft deletion with audit tracking.
/// Soft delete is handled automatically by SoftDeleteInterceptor when
/// using context.Remove() or setting EntityState.Deleted.
/// </summary>
public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public string? DeletedBy { get; private set; }

    /// <summary>
    /// Marks the entity as deleted. Override to cascade to related entities.
    /// </summary>
    public virtual void Delete(DateTimeOffset timestamp, string? userId)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedOn = timestamp;
        DeletedBy = userId;
    }

    /// <summary>
    /// Restores a soft-deleted entity. Override to restore related entities.
    /// </summary>
    public virtual void Restore()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        DeletedOn = null;
        DeletedBy = null;
    }
}

// Infrastructure/Data/Extensions/SoftDeleteExtensions.cs
namespace YourNamespace.Infrastructure.Data.Extensions;

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using YourNamespace.Domain.Common;

public static class SoftDeleteExtensions
{
    /// <summary>
    /// Applies global query filters for all ISoftDeletable entities.
    /// Call in OnModelCreating.
    /// </summary>
    public static void ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(
                Expression.Equal(property, Expression.Constant(false)),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}

// Infrastructure/Data/Configurations/SoftDeletableEntityConfiguration.cs
namespace YourNamespace.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YourNamespace.Domain.Common;

/// <summary>
/// Base configuration for soft-deletable entities.
/// </summary>
public abstract class SoftDeletableEntityConfiguration<TEntity>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : SoftDeletableEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(256);

        // Query filter (alternative to global approach)
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Filtered index for finding deleted entities efficiently
        builder.HasIndex(e => e.IsDeleted)
            .HasFilter("[IsDeleted] = 1")
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_IsDeleted_Filtered");

        // Audit fields from base
        builder.Property(e => e.CreatedOn).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
    }
}

// Example usage in DbContext:
//
// protected override void OnModelCreating(ModelBuilder modelBuilder)
// {
//     base.OnModelCreating(modelBuilder);
//     modelBuilder.ApplySoftDeleteFilters();
//     modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
// }
