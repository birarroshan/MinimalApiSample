using MinimalApiEntity.Models;

namespace MinimalApiEntity.Services;

public interface IPizzaReadService
{
    Task<List<PizzaResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<PizzaResponse?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task InvalidateAsync(string name);
}
