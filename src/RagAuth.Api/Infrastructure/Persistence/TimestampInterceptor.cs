using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence;

public sealed class TimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTimestamps(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var currentTime = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<IHasTimestamps>())
        {
            ApplyTimestamps(entry.State, entry.Entity, currentTime);
        }
    }

    private static void ApplyTimestamps(EntityState state, IHasTimestamps entity, DateTimeOffset currentTime)
    {
        if (state == EntityState.Added)
        {
            entity.CreatedAt = currentTime;
            entity.UpdatedAt = currentTime;
        }
        else if (state == EntityState.Modified)
        {
            entity.UpdatedAt = currentTime;
        }
    }
}
