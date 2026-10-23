using CounterpartyRegistry.Domain.Founders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CounterpartyRegistry.Infrastructure.Persistence.Configurations;

public sealed class FounderConfiguration : IEntityTypeConfiguration<Founder>
{
    public void Configure(EntityTypeBuilder<Founder> builder)
    {
        builder.ToTable("Founders", table => table.HasCheckConstraint("CK_Founders_Inn",
            "LEN([Inn]) = 12 AND [Inn] NOT LIKE '%[^0-9]%'"));
        builder.HasKey(founder => founder.Id);
        builder.Property(founder => founder.Id).ValueGeneratedNever();
        builder.Property(founder => founder.Inn).HasMaxLength(12).IsUnicode(false).IsRequired();
        builder.Property(founder => founder.FullName).HasMaxLength(Founder.MaxFullNameLength).IsRequired();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasIndex(founder => founder.Inn).IsUnique();
    }
}
