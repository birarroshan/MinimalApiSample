using Microsoft.EntityFrameworkCore;
using MinimalApiEntity.Models;

namespace MinimalApiEntity.Data;

public class PizzaDbContext(DbContextOptions<PizzaDbContext> options) : DbContext(options)
{
    public DbSet<Pizza> Pizzas => Set<Pizza>();
    public DbSet<PizzaPrice> Prices => Set<PizzaPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Pizza>().HasKey(pizza => pizza.PizzaId);
        modelBuilder.Entity<PizzaPrice>().HasKey(price => price.PizzaId);
        modelBuilder.Entity<PizzaPrice>()
            .Property(price => price.PizzaId)
            .ValueGeneratedNever();

        modelBuilder.Entity<PizzaPrice>()
            .HasOne(price => price.Pizza)
            .WithOne(pizza => pizza.Price)
            .HasForeignKey<PizzaPrice>(price => price.PizzaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
