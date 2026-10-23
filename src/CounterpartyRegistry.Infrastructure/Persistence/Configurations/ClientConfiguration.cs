using CounterpartyRegistry.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CounterpartyRegistry.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients", table => table.HasCheckConstraint("CK_Clients_TypeAndInn",
            "([Type] = 1 AND LEN([Inn]) = 10 OR [Type] = 2 AND LEN([Inn]) = 12) AND [Inn] NOT LIKE '%[^0-9]%'"));
        builder.HasKey(client => client.Id);
        builder.Property(client => client.Id).ValueGeneratedNever();
        builder.Property(client => client.Inn).HasMaxLength(12).IsUnicode(false).IsRequired();
        builder.Property(client => client.Name).HasMaxLength(Client.MaxNameLength).IsRequired();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasIndex(client => client.Inn).IsUnique();

        builder.HasMany(client => client.FounderLinks).WithOne()
            .HasForeignKey(link => link.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(client => client.FounderLinks)
            .HasField("_founderLinks").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
