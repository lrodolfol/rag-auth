using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Persistence.Repositories;

namespace RagAuth.Api.Infrastructure.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TimestampInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(BuildConnectionString(serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value))
            .AddInterceptors(serviceProvider.GetRequiredService<TimestampInterceptor>()));
        services.AddScoped<IUnitOfWork, EntityFrameworkUnitOfWork>();
        services.AddScoped<IClientRepository, EntityFrameworkClientRepository>();
        services.AddScoped<IBusinessRepository, EntityFrameworkBusinessRepository>();
        return services;
    }

    private static string BuildConnectionString(DatabaseOptions databaseOptions) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = databaseOptions.Host,
            Port = databaseOptions.Port,
            Database = databaseOptions.Name,
            Username = databaseOptions.Username,
            Password = databaseOptions.Password,
        }.ConnectionString;
}
