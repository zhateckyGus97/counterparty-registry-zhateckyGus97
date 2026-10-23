using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Infrastructure.Persistence;
using CounterpartyRegistry.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CounterpartyRegistry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<RegistryDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IFounderRepository, FounderRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<RegistryDbContext>());
        return services;
    }
}
