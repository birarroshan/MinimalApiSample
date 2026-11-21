using Microsoft.EntityFrameworkCore;
using Npgsql;
using MinimalApiEntity.Models;

namespace MinimalApiEntity.Data;

public sealed class PizzaRepository(PizzaDbContext db, ILogger<PizzaRepository> logger) : IPizzaRepository
{
    public async Task<IReadOnlyList<Pizza>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await db.Pizzas
            .AsNoTracking()
            .OrderBy(pizza => pizza.PizzaId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Pizza pizza, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
            // Every API instance uses this transaction-scoped PostgreSQL advisory lock.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(71422001)", cancellationToken);

            db.Pizzas.Add(pizza);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            logger.LogWarning(exception, "Timed out waiting for the shared pizza write lock.");
            throw new PizzaWriteBusyException(exception);
        }
    }

    public async Task<Pizza?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await db.Pizzas
            .AsNoTracking()
            .OrderBy(pizza => pizza.PizzaId)
            .FirstOrDefaultAsync(pizza => pizza.Name == name, cancellationToken);
    }
}
