using JobFlow.Application.Interfaces;
using JobFlow.Contracts.Messages;

namespace JobFlow.IntegrationTests;

public sealed class TestJobPublisher : IJobPublisher
{
    public Task PublishJobCreatedAsync(JobCreatedMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
