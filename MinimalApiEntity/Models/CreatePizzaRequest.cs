namespace MinimalApiEntity.Models;

/// <summary>Details for creating a pizza.</summary>
public sealed record CreatePizzaRequest(string? Name, string? Description);
