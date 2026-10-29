using Microsoft.EntityFrameworkCore;

namespace OnlineStore.Infrastructure.Data;

public class StoreTransaction(OnlineStoreDbContext db)
{
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // ponytail: one database-wide writer lock suits a university store; use ordered
        // product row locks if write throughput becomes a measured bottleneck.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'OnlineStore.Writes', @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 50011, 'Store is busy. Retry the request.', 1;
            """, ct);
        var result = await action();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
