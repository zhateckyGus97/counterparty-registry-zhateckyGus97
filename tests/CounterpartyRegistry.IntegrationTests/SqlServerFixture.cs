using CounterpartyRegistry.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CounterpartyRegistry.IntegrationTests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public string ConnectionString { get; }

    public SqlServerFixture()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Run . ./scripts/Use-LocalDatabase.ps1 before integration tests.");
        var builder = new SqlConnectionStringBuilder(configuredConnection)
        {
            InitialCatalog = "CounterpartyRegistryTests_" + Guid.NewGuid().ToString("N")
        };
        ConnectionString = builder.ConnectionString;
    }

    public RegistryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RegistryDbContext>().UseSqlServer(ConnectionString).Options;
        return new RegistryDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (!database.StartsWith("CounterpartyRegistryTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete a non-test database.");

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
