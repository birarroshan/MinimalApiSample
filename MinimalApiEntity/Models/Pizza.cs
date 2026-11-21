using System.ComponentModel.DataAnnotations;

namespace MinimalApiEntity.Models;

public class Pizza
{
    [Key]
    public int PizzaId { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public required string Name { get; set; }

    public string? Description { get; set; }
    public PizzaPrice? Price { get; set; }
}
