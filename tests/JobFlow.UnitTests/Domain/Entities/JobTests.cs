namespace JobFlow.UnitTests.Domain.Entities;

using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;

public class JobTests
{
    [Fact]
    public void Constructor_ShouldSetInitialProperties()
    {
        // Arrange
        var name = "Test Job";

        // Act
        var job = new Job(name);

        // Assert
        Assert.Equal(name, job.Name);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.NotEqual(Guid.Empty, job.Id);
    }

    [Fact]
    public void MarkAsProcessing_ShouldUpdateStatusToProcessing()
    {
        // Arrange
        var job = new Job("Test Job");

        // Act
        job.MarkAsProcessing();

        // Assert
        Assert.Equal(JobStatus.Processing, job.Status);
    }

    [Fact]
    public void MarkAsCompleted_ShouldUpdateStatusToCompleted()
    {
        // Arrange
        var job = new Job("Test Job");

        // Act
        job.MarkAsCompleted();

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    [Fact]
    public void MarkAsFailed_ShouldUpdateStatusToFailed()
    {
        // Arrange
        var job = new Job("Test Job");

        // Act
        job.MarkAsFailed();

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
    }
}
