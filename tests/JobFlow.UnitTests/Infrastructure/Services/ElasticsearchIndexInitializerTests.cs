using JobFlow.Infrastructure.Services;
using Nest;
using Moq;

namespace JobFlow.UnitTests.Infrastructure.Services;

public class ElasticsearchIndexInitializerTests
{
    private readonly Mock<IElasticClient> _elasticClientMock;
    private readonly ElasticsearchIndexInitializer _initializer;

    public ElasticsearchIndexInitializerTests()
    {
        _elasticClientMock = new Mock<IElasticClient>();
        var settings = new ConnectionSettings(new Uri("http://localhost:9200")).DefaultIndex("test-index");
        _elasticClientMock.Setup(c => c.ConnectionSettings).Returns(settings);

        _initializer = new ElasticsearchIndexInitializer(_elasticClientMock.Object);
    }

    [Fact]
    public void Placeholder_Test()
    {
        Assert.True(true);
    }
}
