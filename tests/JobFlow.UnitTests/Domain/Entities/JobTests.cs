namespace JobFlow.UnitTests.Domain.Entities;

using FluentAssertions;
using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;

public class JobTests
{
    [Fact]
    public void Constructor_ShouldSetInitialProperties()
    {
        var job = new Job("Test Job");

        job.Name.Should().Be("Test Job");
        job.Status.Should().Be(JobStatus.Pending);
        job.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void MarkAsProcessing_ShouldUpdateStatusToProcessing()
    {
        var job = new Job("Test Job");

        job.MarkAsProcessing();

        job.Status.Should().Be(JobStatus.Processing);
    }

    [Fact]
    public void MarkAsCompleted_ShouldUpdateStatusToCompleted()
    {
        var job = new Job("Test Job");
        job.MarkAsProcessing();

        job.MarkAsCompleted();

        job.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public void MarkAsFailed_ShouldUpdateStatusToFailed()
    {
        var job = new Job("Test Job");

        job.MarkAsFailed();

        job.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public void MarkAsCompleted_ShouldThrow_WhenStatusIsPending()
    {
        var job = new Job("Test Job");

        var act = () => job.MarkAsCompleted();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsProcessing_ShouldThrow_WhenStatusIsCompleted()
    {
        var job = new Job("Test Job");
        job.MarkAsProcessing();
        job.MarkAsCompleted();

        var act = () => job.MarkAsProcessing();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsFailed_ShouldThrow_WhenStatusIsCompleted()
    {
        var job = new Job("Test Job");
        job.MarkAsProcessing();
        job.MarkAsCompleted();

        var act = () => job.MarkAsFailed();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsProcessing_ShouldSucceed_WhenStatusIsFailed()
    {
        var job = new Job("Test Job");
        job.MarkAsFailed();

        job.MarkAsProcessing();

        job.Status.Should().Be(JobStatus.Processing);
    }

    [Fact]
    public void MarkAsProcessing_ShouldSucceed_WhenAlreadyProcessing()
    {
        var job = new Job("Test Job");
        job.MarkAsProcessing();

        job.MarkAsProcessing();

        job.Status.Should().Be(JobStatus.Processing);
    }

    [Fact]
    public void MarkAsCompleted_ShouldThrow_WhenStatusIsFailed()
    {
        var job = new Job("Test Job");
        job.MarkAsFailed();

        var act = () => job.MarkAsCompleted();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsFailed_ShouldSucceed_WhenStatusIsProcessing()
    {
        var job = new Job("Test Job");
        job.MarkAsProcessing();

        job.MarkAsFailed("Something went wrong");

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("Something went wrong");
    }
}
