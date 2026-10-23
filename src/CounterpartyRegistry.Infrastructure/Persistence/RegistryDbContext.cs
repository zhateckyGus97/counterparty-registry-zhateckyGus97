using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Founders;
using Microsoft.Data.SqlClient;
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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
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

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("concurrency_conflict",
                "Данные уже изменены другим запросом. Загрузите их повторно.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException)
        {
            var sqlException = (SqlException)exception.InnerException;
            if (sqlException.Number is 2601 or 2627)
            {
                if (sqlException.Message.Contains("IX_Clients_Inn", StringComparison.Ordinal))
                    throw new ConflictException("client.inn_conflict", "Клиент с таким ИНН уже существует.", exception);
                if (sqlException.Message.Contains("IX_Founders_Inn", StringComparison.Ordinal))
                    throw new ConflictException("founder.inn_conflict", "Учредитель с таким ИНН уже существует.", exception);
                if (sqlException.Message.Contains("PK_ClientFounders", StringComparison.Ordinal))
                    throw new ConflictException("client.founder_already_linked", "Учредитель уже связан с клиентом.", exception);
            }

            if (sqlException.Number == 547 &&
                (sqlException.Message.Contains("FK_ClientFounders_Founders_FounderId", StringComparison.Ordinal) ||
                 sqlException.Message.Contains("FK_ClientFounders_Clients_ClientId", StringComparison.Ordinal)))
            {
                if (exception.Entries.Any(entry => entry.Entity is Founder && entry.State == EntityState.Deleted))
                    throw new ConflictException("founder.has_clients", "Учредитель связан с клиентами и не может быть удалён.", exception);

                throw new ConflictException("concurrency_conflict", "Клиент или учредитель уже удалён другим запросом.", exception);
            }

            throw;
        }
    }
}
