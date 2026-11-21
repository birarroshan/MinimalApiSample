using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MinimalApiEntity.Data;
using MinimalApiEntity.Models;
using MinimalApiEntity.Services;
using StackExchange.Redis;

namespace MinimalApiEntity.Tests;

public class CachedPizzaReadServiceTests
{
    private readonly FakePizzaRepository _repository = new();
    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private CachedPizzaReadService CreateService(IDistributedCache? cache = null) =>
        new(_repository, cache ?? _cache, NullLogger<CachedPizzaReadService>.Instance);

    [Fact]
    public async Task GetAllAsync_SecondCall_IsServedFromCache()
    {
        _repository.Pizzas.Add(new Pizza { PizzaId = 1, Name = "Margherita" });
        var service = CreateService();

        await service.GetAllAsync(CancellationToken.None);
        var second = await service.GetAllAsync(CancellationToken.None);

        Assert.Equal(1, _repository.GetAllCalls);
        Assert.Equal([new PizzaResponse(1, "Margherita", null)], second);
    }

    [Fact]
    public async Task InvalidateAsync_ForcesNextReadToHitRepository()
    {
        _repository.Pizzas.Add(new Pizza { PizzaId = 1, Name = "Margherita" });
        var service = CreateService();
        await service.GetAllAsync(CancellationToken.None);
        await service.GetByNameAsync("Margherita", CancellationToken.None);

        _repository.Pizzas.Add(new Pizza { PizzaId = 2, Name = "Pepperoni" });
        await service.InvalidateAsync("Margherita");
        var all = await service.GetAllAsync(CancellationToken.None);
        await service.GetByNameAsync("Margherita", CancellationToken.None);

        Assert.Equal(2, all.Count);
        Assert.Equal(2, _repository.GetAllCalls);
        Assert.Equal(2, _repository.GetByNameCalls);
    }

    [Fact]
    public async Task GetByNameAsync_MissingPizza_IsNotCached()
    {
        var service = CreateService();

        Assert.Null(await service.GetByNameAsync("Unknown", CancellationToken.None));
        _repository.Pizzas.Add(new Pizza { PizzaId = 1, Name = "Unknown" });
        var found = await service.GetByNameAsync("Unknown", CancellationToken.None);

        Assert.Equal(new PizzaResponse(1, "Unknown", null), found);
    }

    [Fact]
    public async Task RedisFailure_FallsBackToRepository()
    {
        _repository.Pizzas.Add(new Pizza { PizzaId = 1, Name = "Margherita" });
        var service = CreateService(new FailingCache());

        var all = await service.GetAllAsync(CancellationToken.None);
        await service.InvalidateAsync("Margherita");

        Assert.Single(all);
        Assert.Equal(1, _repository.GetAllCalls);
    }

    private sealed class FakePizzaRepository : IPizzaRepository
    {
        public List<Pizza> Pizzas { get; } = [];
        public int GetAllCalls { get; private set; }
        public int GetByNameCalls { get; private set; }

        public Task<IReadOnlyList<Pizza>> GetAllAsync(CancellationToken cancellationToken)
        {
            GetAllCalls++;
            return Task.FromResult<IReadOnlyList<Pizza>>(Pizzas.ToList());
        }

        public Task<Pizza?> GetByNameAsync(string name, CancellationToken cancellationToken)
        {
            GetByNameCalls++;
            return Task.FromResult(Pizzas.FirstOrDefault(pizza => pizza.Name == name));
        }

        public Task AddAsync(Pizza pizza, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FailingCache : IDistributedCache
    {
        private static RedisException Failure() => new("Redis is unavailable.");

        public byte[]? Get(string key) => throw Failure();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Failure();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Failure();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => throw Failure();
        public void Refresh(string key) => throw Failure();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Failure();
        public void Remove(string key) => throw Failure();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Failure();
    }
}
