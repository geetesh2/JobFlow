using JobFlow.Worker;
using JobFlow.Infrastructure.DependencyInjection;
using JobFlow.Infrastructure.Services;
using JobFlow.Worker.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGrpc();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();
app.MapGrpcService<JobControlService>();
// ... rest of the code
using var scope = app.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;

var mongoInitializer = serviceProvider.GetRequiredService<MongoDbIndexInitializer>();
var elasticInitializer = serviceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
await mongoInitializer.InitializeAsync();
await elasticInitializer.InitializeAsync();

await app.RunAsync();
