using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using MinimalApiEntity.Data;
using MinimalApiEntity.Models;

namespace MinimalApiEntity.Services;

public sealed class CachedPizzaReadService(
    IPizzaRepository repository,
    IDistributedCache cache,
    ILogger<CachedPizzaReadService> logger) : IPizzaReadService
{
    private const string ListKey = "pizzas:all";
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
    };

    public async Task<List<PizzaResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var cached = await ReadAsync<List<PizzaResponse>>(ListKey, cancellationToken);
        if (cached is not null)
            return cached;

        var pizzas = await repository.GetAllAsync(cancellationToken);
        var response = pizzas.Select(pizza =>
            new PizzaResponse(pizza.PizzaId, pizza.Name, pizza.Description)).ToList();
        await WriteAsync(ListKey, response, cancellationToken);
        return response;
    }

    public async Task<PizzaResponse?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var key = NameKey(name);
        var cached = await ReadAsync<PizzaResponse>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var pizza = await repository.GetByNameAsync(name, cancellationToken);
        if (pizza is null)
            return null;

        var response = new PizzaResponse(pizza.PizzaId, pizza.Name, pizza.Description);
        await WriteAsync(key, response, cancellationToken);
        return response;
    }

    public Task InvalidateAsync(string name) =>
        Task.WhenAll(RemoveAsync(ListKey), RemoveAsync(NameKey(name)));

    private async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var json = await cache.GetStringAsync(key, cancellationToken);
            if (json is null)
                return null;

            return JsonSerializer.Deserialize<T>(json)
                ?? throw new JsonException("The cached response was null.");
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Redis read failed for {CacheKey}; querying PostgreSQL.", key);
            return null;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid cached response for {CacheKey}; querying PostgreSQL.", key);
            return null;
        }
    }

    private async Task WriteAsync<T>(string key, T response, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(response), CacheOptions, cancellationToken);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Redis fill failed for {CacheKey}; returning PostgreSQL data.", key);
        }
    }

    private async Task RemoveAsync(string key)
    {
        try
        {
            // The database has committed; invalidate even if the POST client disconnects.
            await cache.RemoveAsync(key, CancellationToken.None);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception,
                "Redis invalidation failed for {CacheKey} after commit; cached data can remain stale until its 60-second expiry.", key);
        }
    }

    private static string NameKey(string name) =>
        "pizzas:name:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
}
