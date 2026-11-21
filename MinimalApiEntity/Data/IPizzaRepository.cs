using MinimalApiEntity.Models;

namespace MinimalApiEntity.Data;

public interface IPizzaRepository
{
    Task<IReadOnlyList<Pizza>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Pizza pizza, CancellationToken cancellationToken);

    Task<Pizza?> GetByNameAsync(string name, CancellationToken cancellationToken);
}
