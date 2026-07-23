# JobFlow

A production-grade distributed job processing platform built with .NET 9. Demonstrates enterprise backend architecture beyond CRUD: distributed messaging, polyglot persistence, CQRS, domain-driven design, resilience patterns, and full observability.

## Architecture

```
   Client (REST / GraphQL / gRPC)
         |
    [JobFlow.Api]  ──  Keycloak (JWT auth)
         |
   ┌─────┼─────┬─────┬─────┐
   PG   Mongo  ES  Redis  RabbitMQ
                              |
                        [JobFlow.Worker]
                              |
                   ┌──────────┼──────────┐
                   PG      Mongo/ES     DLQ
```

**Clean Architecture** with strict dependency inversion:

| Layer | Responsibility |
|---|---|
| **Domain** | Entities, value objects, enums, domain events (zero dependencies) |
| **Application** | Commands, queries, validators, interfaces (depends only on Domain) |
| **Infrastructure** | EF Core, MongoDB, Elasticsearch, RabbitMQ, Redis implementations |
| **API** | REST endpoints, middleware, GraphQL, gRPC |
| **Worker** | RabbitMQ consumer, job execution, retry, dead-letter handling |

## Tech Stack

| Technology | Purpose |
|---|---|
| .NET 9 | Minimal APIs, background services |
| PostgreSQL 17 | Primary database (EF Core) |
| MongoDB 7.0 | Document store for job payloads |
| Elasticsearch 8.11 | Full-text search and filtering |
| Redis 8 | Distributed cache, idempotency |
| RabbitMQ 3 | Async messaging with dead-letter queues |
| Keycloak 26.2 | OAuth 2.0 / OIDC / JWT / RBAC |
| MediatR | CQRS command/query dispatch |
| FluentValidation | Request validation pipeline |
| Polly 8 | Retry, circuit breaker, timeout |
| HotChocolate 16 | GraphQL server |
| gRPC | Internal service-to-service communication |
| OpenTelemetry | Distributed tracing (Jaeger) + metrics (Prometheus) |
| Serilog | Structured logging with correlation IDs |
| ImageSharp | Real image resizing job handler |
| QuestPDF | Real PDF report generation job handler |
| Docker Compose | 12-service local development stack |
| GitHub Actions | CI pipeline |

## Key Design Patterns

- **CQRS** — Separate command and query models via MediatR
- **Outbox Pattern** — Job + outbox message saved atomically; background processor publishes to RabbitMQ with `FOR UPDATE SKIP LOCKED` concurrency protection
- **Domain Events** — State transitions raise events dispatched after persistence (Mongo/ES sync)
- **Domain State Machine** — `Job` entity enforces valid transitions with guards (`Completed` is terminal)
- **Repository + Unit of Work** — Abstracts persistence, coordinates transactions
- **Resilience Pipelines** — Per-service retry + circuit breaker + timeout via Polly
- **Dead Letter Queue** — Failed jobs published to DLQ after retry exhaustion

## Real Job Handlers

| Handler | Type | Description |
|---|---|---|
| **ImageResize** | Real | Downloads image from URL, resizes with ImageSharp, saves to disk |
| **PdfReport** | Real | Queries jobs from PostgreSQL, generates styled PDF report with QuestPDF |
| **DogApi** | Real | HTTP call to public Dog API |
| DataProcessing | Simulation | 10-batch processing with progress reporting |
| Email | Simulation | SMTP send simulation |

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)

### 1. Clone and build

```bash
git clone https://github.com/geetesh2/JobFlow.git
cd JobFlow
cp .env.example .env   # Edit with your local passwords
dotnet build
```

### 2. Start infrastructure

```bash
docker-compose up -d
```

This starts 12 services: PostgreSQL, MongoDB, Elasticsearch, Redis, RabbitMQ, Keycloak, Jaeger, Prometheus, Grafana, Kibana, API, and Worker.

### 3. Run locally (without Docker for API/Worker)

```bash
# Terminal 1 — API
cd src/JobFlow.Api && dotnet run

# Terminal 2 — Worker
cd src/JobFlow.Worker && dotnet run
```

> **Note:** The API auto-applies EF Core migrations on startup when not in test mode. No manual `dotnet ef database update` needed.

### 4. Smoke test

```bash
# Get a token from Keycloak
TOKEN=$(curl -s -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -d "grant_type=client_credentials&client_id=jobflow-api&client_secret=YOUR_SECRET" \
  | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

# Create an image resize job
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ImageResize",
    "priority": "High",
    "payload": {"url": "https://picsum.photos/1920/1080", "width": 400, "height": 300}
  }'

# Create a PDF report job
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "PdfReport", "payload": {"title": "Job Summary"}}'

# Check job status
curl http://localhost:5274/api/v1/jobs/{JOB_ID} -H "Authorization: Bearer $TOKEN"

# Search jobs
curl "http://localhost:5274/api/v1/jobs?status=Completed&page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

## API Endpoints

| Method | Route | Description |
|---|---|---|
| POST | `/api/v1/jobs` | Create a job |
| GET | `/api/v1/jobs/{id}` | Get job by ID |
| GET | `/api/v1/jobs` | Search jobs (full-text, filters, pagination) |
| GET | `/api/v1/identity/me` | Get current user info |
| POST | `/graphql` | GraphQL queries |
| GET | `/health` | Health check (all dependencies) |
| GET | `/live` | Liveness probe |
| GET | `/ready` | Readiness probe |

All endpoints require JWT Bearer authentication from Keycloak.

## Worker Processing Pipeline

```
Message from RabbitMQ
  → Deserialize → Load job from DB
  → Mark as Processing (domain event → Mongo/ES sync)
  → Execute handler (with timeout)
  → Success: Mark Completed + ACK
  → Failure: Retry with exponential backoff
     → Retries exhausted: Mark Failed → Dead Letter Queue
```

## Observability

| Tool | URL | Purpose |
|---|---|---|
| Swagger | http://localhost:5274/swagger | API documentation |
| Jaeger | http://localhost:16686 | Distributed traces |
| Prometheus | http://localhost:9090 | Metrics |
| Grafana | http://localhost:3000 | Dashboards |
| RabbitMQ | http://localhost:15672 | Queue management |
| Kibana | http://localhost:5601 | Log viewer |
| Keycloak | http://localhost:8080 | Identity management |

## Testing

```bash
dotnet test
```

**56 tests total** (43 unit + 13 integration), all passing:

- **Domain** — Job state machine guards, valid/invalid transitions
- **Validators** — CreateJobCommand, UpdateJobStatusCommand, SearchJobsQuery
- **DTOs** — Record construction and property mapping
- **Services** — Idempotency service, Elasticsearch initializer
- **Integration** — Job CRUD (create, get, search), validation (400 responses), middleware (correlation ID), health checks

Tests use in-memory fakes — no Docker needed.

## Load Testing

```bash
# Install k6: https://k6.io/docs/get-started/installation/
k6 run --env TOKEN=$TOKEN tests/load/load-test.js
```

Three scenarios run over ~4 minutes: ramping creation load (1→25 VUs), constant read load (5 VUs), and a spike test (50 VUs). See [tests/load/README.md](tests/load/README.md) for details.

**Thresholds:**
- Job creation p95 < 1.5s
- Job retrieval p95 < 500ms
- Search p95 < 1s
- Error rate < 5%

## Configuration

All infrastructure values are **required** at startup (no silent defaults). Local values are in `appsettings.json`; Docker overrides via environment variables.

| Key | Description |
|---|---|
| `ConnectionStrings:JobFlowDb` | PostgreSQL connection |
| `Redis:Configuration` | Redis connection |
| `MongoDb:ConnectionString` | MongoDB connection |
| `Elasticsearch:Url` | Elasticsearch URL |
| `RabbitMq:Host/Username/Password` | RabbitMQ credentials |
| `Otlp:Endpoint` | OpenTelemetry collector |
| `Worker:OutputDirectory` | Where image/PDF outputs are saved (default: `output/`) |

## Project Structure

```
src/
  JobFlow.Api/            — REST, GraphQL, gRPC, middleware
  JobFlow.Application/    — Commands, queries, validators, interfaces
  JobFlow.Domain/         — Entities, value objects, domain events
  JobFlow.Infrastructure/ — EF Core, MongoDB, Elasticsearch, RabbitMQ, Redis
  JobFlow.Worker/         — Job execution, handlers, retry, dead-letter
  JobFlow.Contracts/      — Shared message contracts + gRPC protos
  JobFlow.Shared/         — Worker gRPC protos
tests/
  JobFlow.UnitTests/      — 43 unit tests
  JobFlow.IntegrationTests/ — 13 integration tests
docs/
  PROJECT-DOCUMENTATION.md — Detailed architecture and design docs
  SETUP-GUIDE.md           — Step-by-step setup instructions
```

## Documentation

- [Project Documentation](docs/PROJECT-DOCUMENTATION.md) — Architecture deep dive, design patterns, component details
- [Setup Guide](docs/SETUP-GUIDE.md) — Step-by-step local development setup
- [API Usage Guide](docs/API-USAGE-GUIDE.md) — Authentication flow, curl examples for every endpoint, refresh tokens, error responses
- [ADR-001: Outbox Pattern](docs/ADR-001-outbox-pattern.md) — Why outbox over direct publish, alternatives considered, trade-offs
- [Load Test Guide](tests/load/README.md) — k6 performance testing setup and thresholds

## License

This project is for educational and portfolio purposes.