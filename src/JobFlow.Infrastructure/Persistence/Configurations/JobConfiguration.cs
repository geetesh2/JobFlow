using JobFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobFlow.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(Domain.Enums.JobPriority.Normal);

        builder.Property(x => x.Payload)
            .HasColumnType("text");

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.Property(x => x.MaxRetries)
            .HasDefaultValue(3);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.ProgressPercentage)
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.OwnsOne(x => x.Metadata, meta =>
        {
            meta.Property(m => m.Source).HasMaxLength(200).HasColumnName("MetadataSource");
            meta.Property(m => m.ScheduledAtUtc).HasColumnName("MetadataScheduledAtUtc");
            meta.Property(m => m.Tags)
                .HasColumnName("MetadataTags")
                .HasColumnType("text")
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        });

        builder.Ignore(x => x.DomainEvents);
    }
}
