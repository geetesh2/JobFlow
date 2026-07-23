using JobFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFlow.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.HasIndex(x => x.ProcessedAtUtc)
            .HasFilter("\"ProcessedAtUtc\" IS NULL");
    }
}
