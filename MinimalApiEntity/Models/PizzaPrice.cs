namespace MinimalApiEntity.Models;

public class PizzaPrice
{
    public int PizzaId { get; set; }
    public decimal Price { get; set; }
    public Pizza Pizza { get; set; } = null!;
}
