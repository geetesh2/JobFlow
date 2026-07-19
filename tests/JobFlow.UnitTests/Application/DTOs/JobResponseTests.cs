namespace JobFlow.UnitTests.Application.DTOs;

using JobFlow.Application.DTOs;

public class JobResponseTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Job";
        var status = "Pending";
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow;

        // Act
        var response = new JobResponse(id, name, status, createdAt, updatedAt);

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal(name, response.Name);
        Assert.Equal(status, response.Status);
        Assert.Equal(createdAt, response.CreatedAtUtc);
        Assert.Equal(updatedAt, response.UpdatedAtUtc);
    }
}
