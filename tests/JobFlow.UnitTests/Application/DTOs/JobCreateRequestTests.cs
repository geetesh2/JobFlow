namespace JobFlow.UnitTests.Application.DTOs;

using JobFlow.Application.DTOs;
using System.Text.Json;

public class JobCreateRequestTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var name = "Test Job";
        var payload = JsonSerializer.Deserialize<JsonElement>("{\"key\": \"value\"}");
        var metadata = new Dictionary<string, JsonElement> { { "meta", JsonSerializer.Deserialize<JsonElement>("\"data\"") } };

        // Act
        var request = new JobCreateRequest(name, payload, metadata);

        // Assert
        Assert.Equal(name, request.Name);
        Assert.Equal(payload, request.Payload);
        Assert.Equal(metadata, request.Metadata);
    }
}
