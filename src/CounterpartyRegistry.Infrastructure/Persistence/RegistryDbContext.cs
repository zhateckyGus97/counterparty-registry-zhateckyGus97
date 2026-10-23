using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Founders;
using Microsoft.EntityFrameworkCore;

namespace CounterpartyRegistry.Infrastructure.Persistence;

public sealed class RegistryDbContext : DbContext, IUnitOfWork
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Founder> Founders => Set<Founder>();
    public DbSet<ClientFounder> ClientFounders => Set<ClientFounder>();

    public RegistryDbContext(DbContextOptions<RegistryDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RegistryDbContext).Assembly);
    }

    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareClientVersionUpdates();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception)
        {
            var conflict = PersistenceExceptionTranslator.Translate(exception);
            if (conflict is null)
                throw;

            throw conflict;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareClientVersionUpdates();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var conflict = PersistenceExceptionTranslator.Translate(exception);
            if (conflict is null)
                throw;

            throw conflict;
        }
    }

    private void PrepareClientVersionUpdates()
    {
        ChangeTracker.DetectChanges();

        // Even if UpdatedAt has the same value, changing links must update the client's
        // rowversion. Otherwise a concurrent change of client type could go unnoticed.
        var changedClientIds = ChangeTracker.Entries<ClientFounder>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Deleted)
            .Select(entry => entry.Entity.ClientId).Distinct().ToArray();

        foreach (var clientId in changedClientIds)
        {
            var clientEntry = ChangeTracker.Entries<Client>()
                .SingleOrDefault(entry => entry.Entity.Id == clientId)
                ?? throw new InvalidOperationException("Изменять связи нужно через загруженного клиента.");

            if (clientEntry.State is EntityState.Unchanged or EntityState.Modified)
            {
                clientEntry.Property(client => client.UpdatedAt).IsModified = true;
            }
        }
    }
}
