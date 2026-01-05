// Infrastructure/Data/Interceptors/AuditableEntityInterceptor.cs
namespace YourApp.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YourApp.Application.Common.Interfaces;
using YourApp.Domain.Common;

/// <summary>
/// Automatically populates audit fields on entities implementing IAuditableEntity.
/// </summary>
public sealed class AuditableEntityInterceptor(
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(DbContext? context)
    {
        if (context is null) return;

        var now = timeProvider.GetUtcNow();
        var userId = currentUserService.UserId;

        var entries = context.ChangeTracker
            .Entries<IAuditableEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                SetCreationAudit(entry, now, userId);
            }
            else if (entry.State == EntityState.Modified)
            {
                SetModificationAudit(entry, now, userId);
                PreventCreationAuditOverwrite(entry);
            }
        }
    }

    private static void SetCreationAudit(
        EntityEntry<IAuditableEntity> entry,
        DateTimeOffset timestamp,
        string? userId)
    {
        entry.Property(e => e.CreatedOn).CurrentValue = timestamp;
        entry.Property(e => e.CreatedBy).CurrentValue = userId;
    }

    private static void SetModificationAudit(
        EntityEntry<IAuditableEntity> entry,
        DateTimeOffset timestamp,
        string? userId)
    {
        entry.Property(e => e.UpdatedOn).CurrentValue = timestamp;
        entry.Property(e => e.UpdatedBy).CurrentValue = userId;
    }

    private static void PreventCreationAuditOverwrite(EntityEntry<IAuditableEntity> entry)
    {
        entry.Property(e => e.CreatedOn).IsModified = false;
        entry.Property(e => e.CreatedBy).IsModified = false;
    }
}

// Registration in DependencyInjection.cs:
//
// services.AddHttpContextAccessor();
// services.AddScoped<ICurrentUserService, CurrentUserService>();
// services.AddSingleton(TimeProvider.System);
// services.AddScoped<AuditableEntityInterceptor>();
//
// services.AddDbContext<AppDbContext>((sp, options) =>
// {
//     var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
//     options.UseSqlServer(configuration.GetConnectionString("Default"))
//            .AddInterceptors(interceptor);
// });
