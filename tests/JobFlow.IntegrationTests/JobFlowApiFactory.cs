using JobFlow.Infrastructure.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using JobFlow.Api.Authentication;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Polly;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace JobFlow.IntegrationTests;

public sealed class JobFlowApiFactory : WebApplicationFactory<Program>
{
    private static TestcontainersContainer? _mongoContainer;
    private static TestcontainersContainer? _esContainer;
    private static readonly object _containerLock = new();
    private static bool _skipExternalInitializers = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Docker not available in this environment; skip external initializers.
        _skipExternalInitializers = true;

        builder.ConfigureAppConfiguration((context, config) =>
        {
                string mongoConn;
                string esUrl;

                if (_skipExternalInitializers)
                {
                    mongoConn = "mongodb://localhost:27017";
                    esUrl = "http://localhost:9200";
                }
                else
                {
                    try
                    {
                        mongoConn = _mongoContainer is not null ? $"mongodb://localhost:{_mongoContainer.GetMappedPublicPort(27017)}" : "mongodb://localhost:27017";
                    }
                    catch
                    {
                        mongoConn = "mongodb://localhost:27017";
                    }

                    try
                    {
                        esUrl = _esContainer is not null ? $"http://localhost:{_esContainer.GetMappedPublicPort(9200)}" : "http://localhost:9200";
                    }
                    catch
                    {
                        esUrl = "http://localhost:9200";
                    }
                }

                var dict = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:JobFlowDb"] = "Host=localhost;Port=5432;Database=JobFlowDb;Username=postgres",
                    ["Redis:Configuration"] = "localhost:6379",
                    ["Redis:InstanceName"] = "JobFlow:",
                    ["MongoDb:ConnectionString"] = mongoConn,
                    ["MongoDb:Database"] = "jobflow",
                    ["MongoDb:JobsCollection"] = "jobs",
                    ["Elasticsearch:Url"] = esUrl,
                    ["Elasticsearch:DefaultIndex"] = "jobflow-jobs",
                    ["RabbitMq:Host"] = "localhost",
                    ["RabbitMq:Username"] = "guest",
                    ["RabbitMq:Password"] = "guest",
                    ["Otlp:Endpoint"] = "http://localhost:4317",
                    ["Test:SkipExternalInitializers"] = _skipExternalInitializers.ToString()
                };

                config.AddInMemoryCollection(dict!);
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.TestScheme, options => { });

            // Replace RabbitMQ with test stubs to avoid external dependency during tests
            services.RemoveAll(typeof(RabbitMQ.Client.IConnection));
            services.RemoveAll(typeof(JobFlow.Application.Interfaces.IJobPublisher));
            services.AddScoped<JobFlow.Application.Interfaces.IJobPublisher, TestJobPublisher>();
            // Replace IJobService with a lightweight in-memory test implementation to avoid DB/Mongo/Elastic calls
            services.RemoveAll(typeof(JobFlow.Application.Interfaces.IJobService));
            services.AddSingleton<JobFlow.Application.Interfaces.IJobService, TestJobService>();
            services.AddSingleton<ITestJobStore, TestJobService>();
            // Replace search service with test implementation to return in-memory jobs
            services.RemoveAll(typeof(JobFlow.Application.Interfaces.IJobSearchService));
            services.AddScoped<JobFlow.Application.Interfaces.IJobSearchService, TestJobSearchService>();

            // Replace CQRS handler dependencies with in-memory test doubles
            services.RemoveAll(typeof(IJobRepository));
            services.AddSingleton<IJobRepository, TestJobRepository>();
            services.RemoveAll(typeof(IUnitOfWork));
            services.AddScoped<IUnitOfWork>(sp =>
                new TestUnitOfWork(sp.GetRequiredService<IJobRepository>()));
            services.RemoveAll(typeof(IJobSynchronizer));
            services.AddScoped<IJobSynchronizer, TestJobSynchronizer>();
            services.RemoveAll(typeof(IJobIndexer));
            services.AddScoped<IJobIndexer, TestJobIndexer>();
            services.RemoveAll(typeof(ResiliencePipeline));
            services.AddSingleton(new ResiliencePipelineBuilder().Build());

            // Prevent background service failures (like OutboxProcessor failing to connect to PostgreSQL) from crashing the test host
            services.Configure<HostOptions>(opts =>
                opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

            services.AddDistributedMemoryCache();
            services.RemoveAll(typeof(JobFlow.Application.Abstractions.Services.IIdempotencyService));
            services.AddSingleton<JobFlow.Application.Abstractions.Services.IIdempotencyService, JobFlow.Infrastructure.Services.RedisIdempotencyService>();
        });

    }

    private static void EnsureTestcontainersStarted()
    {
        lock (_containerLock)
        {
            if (_mongoContainer is null)
            {
                var tempMongo = new TestcontainersBuilder<TestcontainersContainer>()
                    .WithImage("mongo:6.0")
                    .WithName("jobflow_test_mongo")
                    .WithPortBinding(27017, assignRandomHostPort: true)
                    .Build();

                tempMongo.StartAsync().GetAwaiter().GetResult();
                _mongoContainer = tempMongo;
            }

            if (_esContainer is null)
            {
                var tempEs = new TestcontainersBuilder<TestcontainersContainer>()
                    .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.9.0")
                    .WithName("jobflow_test_elasticsearch")
                    .WithEnvironment("discovery.type", "single-node")
                    .WithEnvironment("xpack.security.enabled", "false")
                    .WithEnvironment("bootstrap.memory_lock", "false")
                    .WithPortBinding(9200, assignRandomHostPort: true)
                    .Build();

                tempEs.StartAsync().GetAwaiter().GetResult();
                _esContainer = tempEs;
            }
        }
    }
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string TestScheme = "TestScheme";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, JobFlowRoles.User)
        };

        var identity = new ClaimsIdentity(claims, TestScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}