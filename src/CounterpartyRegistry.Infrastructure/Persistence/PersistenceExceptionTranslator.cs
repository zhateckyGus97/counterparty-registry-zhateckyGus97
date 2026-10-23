using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Domain.Founders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CounterpartyRegistry.Infrastructure.Persistence;

internal static class PersistenceExceptionTranslator
{
    public static ConflictException? Translate(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return new ConflictException("concurrency_conflict",
                "Данные уже изменены другим запросом. Загрузите их повторно.", exception);
        }

        if (exception.InnerException is not SqlException sqlException)
            return null;

        // SQL Server reports constraint names only in the error message.
        // Keep these names in sync with the EF configurations and migrations.
        if (sqlException.Number is 2601 or 2627)
        {
            if (sqlException.Message.Contains("IX_Clients_Inn", StringComparison.Ordinal))
                return new ConflictException("client.inn_conflict", "Клиент с таким ИНН уже существует.", exception);
            if (sqlException.Message.Contains("IX_Founders_Inn", StringComparison.Ordinal))
                return new ConflictException("founder.inn_conflict", "Учредитель с таким ИНН уже существует.", exception);
            if (sqlException.Message.Contains("PK_ClientFounders", StringComparison.Ordinal))
                return new ConflictException("client.founder_already_linked", "Учредитель уже связан с клиентом.", exception);
        }

        if (sqlException.Number == 547 &&
            (sqlException.Message.Contains("FK_ClientFounders_Founders_FounderId", StringComparison.Ordinal) ||
             sqlException.Message.Contains("FK_ClientFounders_Clients_ClientId", StringComparison.Ordinal)))
        {
            if (exception.Entries.Any(entry => entry.Entity is Founder && entry.State == EntityState.Deleted))
                return new ConflictException("founder.has_clients", "Учредитель связан с клиентами и не может быть удалён.", exception);

            return new ConflictException("concurrency_conflict", "Клиент или учредитель уже удалён другим запросом.", exception);
        }

        return null;
    }
}
