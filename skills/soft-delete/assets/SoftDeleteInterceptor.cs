// Infrastructure/Data/Interceptors/SoftDeleteInterceptor.cs
namespace YourApp.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YourApp.Application.Common.Interfaces;
using YourApp.Domain.Common;

/// <summary>
/// Converts physical delete operations to soft deletes for ISoftDeletable entities.
/// </summary>
public sealed class SoftDeleteInterceptor(
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ConvertToSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ConvertToSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ConvertToSoftDelete(DbContext? context)
    {
        if (context is null) return;

        var now = timeProvider.GetUtcNow();
        var userId = currentUserService.UserId;

        var deletedEntries = context.ChangeTracker
            .Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted);

        foreach (var entry in deletedEntries)
        {
            // Change state from Deleted to Modified
            entry.State = EntityState.Modified;

            // Set soft delete properties
            entry.Property(e => e.IsDeleted).CurrentValue = true;
            entry.Property(e => e.DeletedOn).CurrentValue = now;
            entry.Property(e => e.DeletedBy).CurrentValue = userId;
        }
    }
}

// Registration in DependencyInjection.cs:
//
// services.AddScoped<SoftDeleteInterceptor>();
//
// services.AddDbContext<AppDbContext>((sp, options) =>
// {
//     var auditInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
//     var softDeleteInterceptor = sp.GetRequiredService<SoftDeleteInterceptor>();
//
//     options.UseSqlServer(configuration.GetConnectionString("Default"))
//            .AddInterceptors(auditInterceptor, softDeleteInterceptor);
// });
