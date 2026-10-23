using CounterpartyRegistry.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CounterpartyRegistry.Infrastructure.Persistence.Configurations;

public sealed class ClientFounderConfiguration : IEntityTypeConfiguration<ClientFounder>
{
    public void Configure(EntityTypeBuilder<ClientFounder> builder)
    {
        builder.ToTable("ClientFounders");
        builder.HasKey(link => new { link.ClientId, link.FounderId });
        builder.HasOne(link => link.Founder).WithMany()
            .HasForeignKey(link => link.FounderId).OnDelete(DeleteBehavior.Restrict);
    }
}
