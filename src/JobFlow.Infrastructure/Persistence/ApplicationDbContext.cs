using JobFlow.Application.Abstractions.Persistence;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
