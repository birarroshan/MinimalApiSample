using Microsoft.AspNetCore.Http.HttpResults;
using MinimalApiEntity.Data;
using MinimalApiEntity.Models;
using MinimalApiEntity.Services;

namespace MinimalApiEntity.Endpoints;

public static class PizzaEndpoints
{
    public static void MapPizza(this WebApplication app)
    {
        app.MapGet("/pizza", async (IPizzaReadService reads, CancellationToken cancellationToken) =>
        {
            var response = await reads.GetAllAsync(cancellationToken);

            return TypedResults.Ok(response);
        })
        .WithName("GetPizzas")
        .WithSummary("Get all pizzas")
        .WithDescription("Returns all pizzas ordered by ID, or an empty array. Responses are cached for 60 seconds.")
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .RequireRateLimiting("pizza-read");

        app.MapPost("/pizza",
        async Task<Results<Created<PizzaResponse>, ValidationProblem, ProblemHttpResult>> (
            CreatePizzaRequest request, IPizzaRepository repository, IPizzaReadService reads, HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["A pizza name is required."]
                });
            }

            if (request.Name.Length > 100)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["Name cannot exceed 100 characters."]
                });
            }

            var pizza = new Pizza
            {
                Name = request.Name,
                Description = request.Description
            };
            try
            {
                await repository.AddAsync(pizza, cancellationToken);
            }
            catch (PizzaWriteBusyException)
            {
                context.Response.Headers.RetryAfter = "1";
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Pizza writes are busy",
                    detail: "Another API instance is holding the write lock. Retry later.");
            }

            await reads.InvalidateAsync(pizza.Name);

            return TypedResults.Created($"/pizza/{pizza.PizzaId}",
                new PizzaResponse(pizza.PizzaId, pizza.Name, pizza.Description));
        })
        .WithName("CreatePizza")
        .WithSummary("Create a pizza")
        .WithDescription("Creates a pizza. One active POST and 100 queued requests are allowed per process. "
            + "Writes share a PostgreSQL lock across instances; queue overflow or a 5-second lock wait returns 429.")
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .RequireRateLimiting("pizza-post");

        app.MapGet("/pizza/{name}", async Task<Results<Ok<PizzaResponse>, NotFound<string>>> (
            string name, IPizzaReadService reads, CancellationToken cancellationToken) =>
        {
            var pizza = await reads.GetByNameAsync(name, cancellationToken);
            if (pizza is not null)
            {
                return TypedResults.Ok(pizza);
            }

            return TypedResults.NotFound($"Pizza with name {name} not found");
        })
        .WithName("GetPizzaByName")
        .WithSummary("Get a pizza by name")
        .WithDescription("Returns the lowest-ID pizza matching the name, or 404. Successful responses are cached for 60 seconds.")
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .RequireRateLimiting("pizza-read");
    }

}
