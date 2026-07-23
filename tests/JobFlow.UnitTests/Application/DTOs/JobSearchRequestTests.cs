namespace JobFlow.UnitTests.Application.DTOs;

using FluentAssertions;
using JobFlow.Application.DTOs;

public class JobSearchRequestTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValuesCorrectly()
    {
        // Act
        var request = new JobSearchRequest(null, null, null, null);

        // Assert
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(20);
        request.SortBy.Should().Be("CreatedAtUtc");
        request.SortOrder.Should().Be("desc");
    }

    [Fact]
    public void Constructor_ShouldSetValuesCorrectly()
    {
        // Arrange
        var query = "test";
        var status = "Pending";
        var after = DateTime.UtcNow.AddDays(-1);
        var before = DateTime.UtcNow.AddDays(1);

        // Act
        var request = new JobSearchRequest(query, status, after, before, 2, 50, "Name", "asc");

        // Assert
        request.Query.Should().Be(query);
        request.Status.Should().Be(status);
        request.CreatedAfterUtc.Should().Be(after);
        request.CreatedBeforeUtc.Should().Be(before);
        request.Page.Should().Be(2);
        request.PageSize.Should().Be(50);
        request.SortBy.Should().Be("Name");
        request.SortOrder.Should().Be("asc");
    }
}
