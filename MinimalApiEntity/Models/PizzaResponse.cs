namespace MinimalApiEntity.Models;

/// <summary>Represents a pizza returned by the API.</summary>
public sealed record PizzaResponse(int Id, string Name, string? Description);
