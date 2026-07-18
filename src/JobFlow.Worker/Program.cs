using JobFlow.Worker;
using JobFlow.Infrastructure.DependencyInjection;
using JobFlow.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
using var scope = host.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;

var mongoInitializer = serviceProvider.GetRequiredService<MongoDbIndexInitializer>();
var elasticInitializer = serviceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
await mongoInitializer.InitializeAsync();
await elasticInitializer.InitializeAsync();

await host.RunAsync();
