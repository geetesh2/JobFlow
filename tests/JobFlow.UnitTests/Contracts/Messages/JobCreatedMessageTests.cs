namespace JobFlow.UnitTests.Contracts.Messages;

using FluentAssertions;
using JobFlow.Contracts.Messages;

public class JobCreatedMessageTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        var jobId = Guid.NewGuid();
        var name = "Test Job";
        var createdAt = DateTime.UtcNow;
        var correlationId = "corr-123";

        var message = new JobCreatedMessage(jobId, name, "Normal", null, 3, createdAt, correlationId);

        message.JobId.Should().Be(jobId);
        message.Name.Should().Be(name);
        message.Priority.Should().Be("Normal");
        message.MaxRetries.Should().Be(3);
        message.CreatedAtUtc.Should().Be(createdAt);
        message.CorrelationId.Should().Be(correlationId);
    }
}
