# JobFlow

Comprehensive overview and current status for the JobFlow project.

## Project Overview

JobFlow is a .NET 9 (net9.0) microservice-style application composed of an API, Application layer, Infrastructure layer, Worker, and tests. Its purpose is to create and manage jobs, persist job state in a relational database, store rich documents in MongoDB, and provide search capabilities via Elasticsearch. Messages for job creation are published to RabbitMQ and processed by a Worker.

Key modules:

- `src/JobFlow.Api` - Minimal API (endpoints for jobs, auth, wiring)
- `src/JobFlow.Application` - DTOs, use-cases, interfaces
- `src/JobFlow.Infrastructure` - DB clients (Postgres/EF Core), MongoDB, Elasticsearch, RabbitMQ wiring, index initializers
- `src/JobFlow.Worker` - background worker processing messages
- `tests/JobFlow.IntegrationTests` - integration tests using WebApplicationFactory
- `tests/JobFlow.UnitTests` - unit tests

## Architecture & Tech Stack

- .NET 9 (net9.0) minimal APIs
- PostgreSQL (EF Core) — primary relational data
- MongoDB — document storage (Job documents, indexes)
- Elasticsearch (NEST) — search index, analyzers, mappings
- RabbitMQ — messaging for job-created events
- Docker + docker-compose for local environment orchestration (compose files in repo)
- xUnit + Microsoft.AspNetCore.Mvc.Testing for integration tests
- DotNet.Testcontainers (attempted in tests) as optional approach for containerized test dependencies

## Important Files

- `Program.cs` - application startup and DI wiring
- `src/JobFlow.Infrastructure/Services/MongoDbIndexInitializer.cs` - ensures MongoDB indexes
- `src/JobFlow.Infrastructure/Services/ElasticsearchIndexInitializer.cs` - ensures ES index and mappings
- `src/JobFlow.Infrastructure/Services/ElasticJobIndexer.cs` - ES indexing helper
- `src/JobFlow.Api/Endpoints/JobEndpoints.cs` - job endpoints and search signature
- `tests/JobFlow.IntegrationTests/JobFlowApiFactory.cs` - test host customization (Testcontainers fallback and DI overrides)

## Current Status (as of 2026-07-19)

- Solution builds successfully.
- Phase 1-5 verification completed, including:
    - Infrastructure stack (Postgres, MongoDB, Elasticsearch, RabbitMQ) successfully validated.
    - Endpoints operational and tested.
    - Full integration test suite passing with fallback support.
    - Observability (OpenTelemetry) configured and active.
- Current active work: `feature/phase-4-job-execution`.

## How Tests Work (notes)

- The integration test factory attempts to use Testcontainers when Docker is available. When Docker is unavailable or Testcontainers fail, the factory falls back to in-memory/test doubles and sets `Test:SkipExternalInitializers=true` so startup won't block on external services.
- When running tests locally without Docker, integration tests assert behavior using the in-memory `TestJobService` and `TestJobSearchService`.

## Running Locally (developer workflow)

1. Build the solution:

```bash
dotnet build JobFlow.sln
```

2. Run API locally (development):

```bash
dotnet run --project src/JobFlow.Api/JobFlow.Api.csproj --urls http://localhost:5274
```

3. Run tests (integration tests use fallbacks when Docker not available):

```bash
dotnet test tests/JobFlow.IntegrationTests/JobFlow.IntegrationTests.csproj
dotnet test tests/JobFlow.UnitTests/JobFlow.UnitTests.csproj
```

4. If you have Docker and want Testcontainers to start Mongo/Elasticsearch during tests, ensure Docker daemon is running and remove or allow Testcontainers in `tests/JobFlow.IntegrationTests/JobFlowApiFactory.cs`.

## CI / Docker Compose

- There are `docker-compose.yml` and `docker/docker-compose.phase3.yml` files in the repo to bring up Postgres, Mongo, Elasticsearch, RabbitMQ, and Keycloak for development.
- If running in CI, prefer spinning up services in the CI job (Docker-in-Docker or platform-provided services) or enable Testcontainers against the CI Docker daemon.

## Outstanding TODOs (high priority)

- Finalize `JobFlowApiFactory` to reliably start Testcontainers in Docker-enabled environments.
- Consider extracting test doubles and registration helpers into a dedicated `tests/` helper library for reuse.
- Add documentation for development environment setup (system dependencies, minimum memory for Elasticsearch, etc.).
- Remove temporary Console logs from test doubles once debugging is complete.

## Notes / Troubleshooting

- If you see startup timeouts related to MongoDB/Elasticsearch during development or tests, either run the services locally (docker-compose) or enable the test fallbacks by setting `Test:SkipExternalInitializers=true` in the test host configuration.
- If Testcontainers fail with Docker socket or address errors, check Docker daemon availability and permissions.

## Contact / Maintainers

- Current workspace owner: geetesh (local workspace path: `/Users/geetesh/JobFlow`).

---

If you want, I can also:

- push this README to a remote GitHub repo (I can create a commit locally — you will still need to push),
- expand the README with runbook commands for common tasks (migrations, seeding, docker-compose commands), or
- generate a minimal `dev-setup.md` with step-by-step Docker compose instructions.

## Developer Setup (Detailed)

This section lists step-by-step commands and recommended machine resources for local development and testing.

Prerequisites

- .NET 9 SDK
- Docker (optional but recommended for full integration with Postgres/Mongo/Elasticsearch/RabbitMQ/Keycloak)
- At least 8GB RAM available; Elasticsearch benefits from more (12GB+ recommended for running ES locally alongside other services).

Start full stack with docker-compose

```bash
# from repository root
docker-compose up -d
# or for phase3 compose
docker-compose -f docker/docker-compose.phase3.yml up -d
```

Confirm services

```bash
docker ps --filter "name=jobflow" --format "{{.Names}}: {{.Status}}"
```

Environment notes for Elasticsearch

- Elasticsearch requires sufficient JVM memory. If ES fails to start, increase Docker resources or set `ES_JAVA_OPTS` environment variable in the compose file, e.g. `-e ES_JAVA_OPTS='-Xms1g -Xmx1g'` for low-memory dev.

Database migrations & seeding

- Run EF Core migrations to apply to Postgres (from the Infrastructure or appropriate project):

```bash
dotnet ef database update --project src/JobFlow.Infrastructure --startup-project src/JobFlow.Api
```

Run the API locally (development)

```bash
dotnet run --project src/JobFlow.Api/JobFlow.Api.csproj --urls http://localhost:5274
```

Running integration tests

- Without Docker (fast, uses test doubles/fallbacks):

```bash
dotnet test tests/JobFlow.IntegrationTests/JobFlow.IntegrationTests.csproj
```

- With Docker/Testcontainers (requires Docker daemon available):
    - Ensure Docker is running.
    - Edit or allow `JobFlowApiFactory` to start Testcontainers and do not force `Test:SkipExternalInitializers=true`.
    - Then run the tests; Testcontainers will attempt to start Mongo and Elasticsearch containers and map ports for the test host.

Troubleshooting

- If integration tests fail with connection refused to `localhost:27017` or `localhost:9200`, either start the services with docker-compose above or run tests in the Docker-enabled mode.
- If Elasticsearch fails with memory errors, increase Docker memory and/or ES JVM settings.

Resource recommendations

- Developer laptop: 8-16GB RAM, modern CPU. For running Elasticsearch + other services concurrently, prefer 16GB.
- CI runners: use machines with at least 8GB; for Elasticsearch testcontainers consider specialized test images or remote hosts where Docker resources can be controlled.

Automation suggestions

- Add a small `scripts/` folder with helper scripts to start/stop compose, run migrations, and run tests with/without docker.
