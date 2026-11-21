using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MinimalApiEntity.Data;
using MinimalApiEntity.Endpoints;
using MinimalApiEntity.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("Pizzas")
    ?? throw new InvalidOperationException(
        "Configure ConnectionStrings:Pizzas for PostgreSQL. For local development, run scripts\\Start-Postgres.ps1.");
builder.Services.AddDbContext<PizzaDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IPizzaRepository, PizzaRepository>();
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Redis for the shared pizza cache.");
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = ConfigurationOptions.Parse(redisConnection);
    options.ConfigurationOptions.AbortOnConnectFail = false;
    options.ConfigurationOptions.ConnectRetry = 0;
    options.ConfigurationOptions.ConnectTimeout = 500;
    options.ConfigurationOptions.AsyncTimeout = 500;
    options.ConfigurationOptions.BacklogPolicy = BacklogPolicy.FailFast;
    options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "MinimalApiEntity:v1:";
});
builder.Services.AddScoped<IPizzaReadService, CachedPizzaReadService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddConcurrencyLimiter("pizza-read", limiter =>
    {
        limiter.PermitLimit = 32;
        limiter.QueueLimit = 100;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.AddConcurrencyLimiter("pizza-post", limiter =>
    {
        limiter.PermitLimit = 1;
        limiter.QueueLimit = 100;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "1";
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("PizzaRequestQueue")
            .LogWarning("Rejected {Method} {Path} because its per-process queue is full.",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        await TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Pizza request queue is full",
            detail: "Retry later. Each process permits 1 active POST or 32 active pizza GETs, with 100 queued requests per policy.")
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PizzaDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapPizza();

app.Run();
