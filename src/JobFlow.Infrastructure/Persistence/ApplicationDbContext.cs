using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Common;
using JobFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContext
    : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker
            .Entries<BaseEntity<Guid>>();

        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.SetCreatedAtUtc(utcNow);
                entry.Entity.SetUpdatedAtUtc(utcNow);
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.SetUpdatedAtUtc(utcNow);
            }
        }
    }
}
