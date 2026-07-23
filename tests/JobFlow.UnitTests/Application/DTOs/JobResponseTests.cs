namespace JobFlow.UnitTests.Application.DTOs;

using FluentAssertions;
using JobFlow.Application.DTOs;

public class JobResponseTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var name = "Test Job";
        var status = "Pending";
        var priority = "Normal";
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow;

        var response = new JobResponse(id, name, status, priority, 0, 3, 0, null, null, createdAt, updatedAt, null);

        response.Id.Should().Be(id);
        response.Name.Should().Be(name);
        response.Status.Should().Be(status);
        response.Priority.Should().Be(priority);
        response.MaxRetries.Should().Be(3);
        response.CreatedAtUtc.Should().Be(createdAt);
        response.UpdatedAtUtc.Should().Be(updatedAt);
    }
}
