using JobFlow.Domain.Entities;

namespace JobFlow.Worker.Handlers;

public interface IJobHandler
{
    string JobType { get; }
    Task HandleAsync(Job job, string? payload, CancellationToken cancellationToken);
}
