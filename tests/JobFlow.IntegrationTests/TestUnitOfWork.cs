using JobFlow.Application.Abstractions.Persistence;

namespace JobFlow.IntegrationTests;

public sealed class TestUnitOfWork : IUnitOfWork
{
    public IJobRepository Jobs { get; }

    public TestUnitOfWork(IJobRepository jobRepository)
    {
        Jobs = jobRepository;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(1);
    }

    public void Dispose()
    {
    }
}
