namespace JobFlow.UnitTests.Application.DTOs;

using FluentAssertions;
using JobFlow.Application.DTOs;
using System.Text.Json;

public class JobCreateRequestTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        var name = "Test Job";
        var payload = JsonSerializer.Deserialize<JsonElement>("{\"key\": \"value\"}");
        var metadata = new Dictionary<string, JsonElement> { { "meta", JsonSerializer.Deserialize<JsonElement>("\"data\"") } };

        var request = new JobCreateRequest(name, "High", payload, 5, metadata);

        request.Name.Should().Be(name);
        request.Priority.Should().Be("High");
        request.Payload.Should().Be(payload);
        request.MaxRetries.Should().Be(5);
        request.Metadata.Should().BeEquivalentTo(metadata);
    }
}
