using JobFlow.Contracts.Messages;

namespace JobFlow.Application.Interfaces;

public interface IJobPublisher
{
    Task PublishJobCreatedAsync(JobCreatedMessage message, CancellationToken cancellationToken = default);
}
