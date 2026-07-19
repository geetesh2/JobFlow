namespace JobFlow.UnitTests.Contracts.Messages;

using JobFlow.Contracts.Messages;

public class JobCreatedMessageTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var name = "Test Job";
        var createdAt = DateTime.UtcNow;
        var correlationId = "corr-123";

        // Act
        var message = new JobCreatedMessage(jobId, name, createdAt, correlationId);

        // Assert
        Assert.Equal(jobId, message.JobId);
        Assert.Equal(name, message.Name);
        Assert.Equal(createdAt, message.CreatedAtUtc);
        Assert.Equal(correlationId, message.CorrelationId);
    }
}
