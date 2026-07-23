namespace JobFlow.Application.Abstractions.Persistence;

public interface IUnitOfWork : IDisposable
{
    IJobRepository Jobs { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
