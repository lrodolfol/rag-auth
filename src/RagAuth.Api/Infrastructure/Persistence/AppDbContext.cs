using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Business> Businesses => Set<Business>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyCamelCaseColumnNames(modelBuilder);
    }

    private static void ApplyCamelCaseColumnNames(ModelBuilder modelBuilder)
    {
        var properties = modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties());
        foreach (var property in properties)
        {
            property.SetColumnName(JsonNamingPolicy.CamelCase.ConvertName(property.Name));
        }
    }
}
