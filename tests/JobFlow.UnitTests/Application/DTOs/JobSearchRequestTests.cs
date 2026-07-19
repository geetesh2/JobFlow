namespace JobFlow.UnitTests.Application.DTOs;

using JobFlow.Application.DTOs;

public class JobSearchRequestTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValuesCorrectly()
    {
        // Act
        var request = new JobSearchRequest(null, null, null, null);

        // Assert
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal("CreatedAtUtc", request.SortBy);
        Assert.Equal("desc", request.SortOrder);
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
        Assert.Equal(query, request.Query);
        Assert.Equal(status, request.Status);
        Assert.Equal(after, request.CreatedAfterUtc);
        Assert.Equal(before, request.CreatedBeforeUtc);
        Assert.Equal(2, request.Page);
        Assert.Equal(50, request.PageSize);
        Assert.Equal("Name", request.SortBy);
        Assert.Equal("asc", request.SortOrder);
    }
}
