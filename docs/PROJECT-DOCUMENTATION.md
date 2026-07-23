# JobFlow — Distributed Job Processing Platform

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture](#2-architecture)
3. [Technology Stack](#3-technology-stack)
4. [Project Structure](#4-project-structure)
5. [Design Patterns Implemented](#5-design-patterns-implemented)
6. [Component Deep Dive](#6-component-deep-dive)
7. [Request Flow — End to End](#7-request-flow--end-to-end)
8. [API Reference](#8-api-reference)
9. [Worker Processing Pipeline](#9-worker-processing-pipeline)
10. [Resilience & Fault Tolerance](#10-resilience--fault-tolerance)
11. [Observability](#11-observability)
12. [Security](#12-security)
13. [Infrastructure & DevOps](#13-infrastructure--devops)
14. [Testing Strategy](#14-testing-strategy)
15. [Configuration Reference](#15-configuration-reference)
16. [How to Run](#16-how-to-run)

---

## 1. Project Overview

JobFlow is a production-grade distributed job processing platform built with .NET 9. Clients submit jobs (image resizing, report generation, PDF conversion, data processing, etc.) through secured APIs. The system persists job information, queues jobs for background processing via RabbitMQ, processes them concurrently using worker services with retry/timeout/cancellation support, and provides full observability through distributed tracing, structured logging, and metrics.

### What Makes This Different from a CRUD App

- **Distributed Architecture** — API and Worker are separate deployable services communicating via RabbitMQ message broker
- **Polyglot Persistence** — PostgreSQL (source of truth), MongoDB (document store), Elasticsearch (search), Redis (caching/idempotency)
- **Production Patterns** — Outbox pattern for reliable messaging, Saga pattern for distributed transactions, CQRS for read/write separation, Domain Events for decoupled side effects
- **Enterprise Resilience** — Circuit breakers, exponential retry with jitter, dead-letter queues, bulkhead isolation via named resilience pipelines
- **Full Observability** — Distributed tracing (Jaeger), metrics (Prometheus/Grafana), structured logging (Serilog), correlation IDs across the entire request flow

---

## 2. Architecture

### Clean Architecture Layers

```
                    +------------------+
                    |    JobFlow.Api    |  <-- Minimal API endpoints, middleware, GraphQL, gRPC
                    +--------+---------+
                             |
                    +--------+---------+
                    | JobFlow.Application| <-- Commands, Queries, DTOs, Validators, Interfaces
                    +--------+---------+
                             |
                    +--------+---------+
                    |  JobFlow.Domain   |  <-- Entities, Value Objects, Enums, Domain Events
                    +--------+---------+
                             |
                    +--------+---------+
                    |JobFlow.Infrastructure| <-- EF Core, MongoDB, Elasticsearch, RabbitMQ, Redis
                    +------------------+

  +------------------+     +------------------+
  | JobFlow.Worker   |     | JobFlow.Contracts|  <-- Shared messages & gRPC protos
  +------------------+     +------------------+
```

**Dependency Rule:** Dependencies point inward. Domain has zero dependencies. Application depends only on Domain. Infrastructure implements Application interfaces. API and Worker depend on all layers.

### System Architecture

```
   Client (HTTP/GraphQL/gRPC)
         |
         v
  +------+-------+       +-----------+
  |  JobFlow.Api  +------>| Keycloak  |  (JWT validation)
  +------+-------+       +-----------+
         |
    +----+----+----+----+
    |    |    |    |    |
    v    v    v    v    v
  [PG] [Mongo][ES][Redis][RabbitMQ]
                              |
                              v
                     +--------+--------+
                     | JobFlow.Worker   |
                     +--------+--------+
                              |
                    +---------+---------+
                    |    |    |    |    |
                    v    v    v    v    v
                  [PG] [Mongo][ES][Redis][DLQ]
```

---

## 3. Technology Stack

### Why Each Technology Was Chosen

| Technology | Role | Why This Choice |
|---|---|---|
| **ASP.NET Core 9** | API framework | Minimal APIs for lightweight endpoints, native DI, high performance |
| **PostgreSQL 17** | Primary database | Open-source, ACID-compliant, rich ecosystem, EF Core support |
| **MongoDB 7.0** | Document store | Flexible schema for job payloads, demonstrates polyglot persistence |
| **Elasticsearch 8.11** | Search engine | Full-text search, filtering, aggregations on job metadata |
| **Redis 8** | Cache + idempotency | Sub-millisecond reads, distributed cache, idempotency key store |
| **RabbitMQ 3** | Message broker | Reliable message delivery, dead-letter exchanges, management UI |
| **Keycloak 26.2** | Identity provider | OAuth 2.0 / OIDC / JWT, RBAC, realm management, Docker-friendly |
| **MediatR 13** | CQRS mediator | Decouples handlers from endpoints, pipeline behaviors for cross-cutting concerns |
| **FluentValidation 11** | Request validation | Fluent API, testable validators, integrates as MediatR pipeline behavior |
| **Polly 8** | Resilience | Retry, circuit breaker, timeout — composable resilience pipelines |
| **Serilog 9** | Structured logging | Enrichment (correlation IDs), multiple sinks, configuration-driven |
| **OpenTelemetry 1.15** | Distributed tracing | Vendor-neutral, traces across API → RabbitMQ → Worker |
| **HotChocolate 16** | GraphQL | .NET-native, annotation-based, filtering/sorting/paging built-in |
| **gRPC** | Internal communication | High-performance binary protocol for service-to-service calls |
| **EF Core 9** | ORM | Code-first migrations, LINQ queries, owned types for value objects |
| **xUnit + FluentAssertions** | Testing | Industry standard, expressive assertions, parallel test execution |
| **Docker Compose** | Local infrastructure | Single command to spin up 12 services locally |
| **GitHub Actions** | CI/CD | Build, test, Docker image creation on push/PR |

---

## 4. Project Structure

```
JobFlow/
|-- src/
|   |-- JobFlow.Api/                  # REST endpoints, middleware, GraphQL, gRPC
|   |   |-- Authentication/           # Keycloak JWT setup, claims transformation
|   |   |-- Endpoints/                # Minimal API route handlers
|   |   |-- Extensions/               # Rate limiting configuration
|   |   |-- GraphQL/                   # HotChocolate query types
|   |   |-- Middleware/                # Exception handling, idempotency, correlation ID
|   |   |-- Services/                  # gRPC service implementation
|   |   |-- Program.cs                 # Application entry point & DI composition
|   |   +-- Dockerfile                 # Multi-stage Docker build
|   |
|   |-- JobFlow.Application/          # Use cases (no infrastructure dependencies)
|   |   |-- Abstractions/             # Interfaces: IUnitOfWork, IRepository, IJobSynchronizer
|   |   |-- Behaviors/                # MediatR pipeline: ValidationBehavior
|   |   |-- Commands/                  # CQRS commands: CreateJob, UpdateJobStatus
|   |   |-- Queries/                   # CQRS queries: GetJobById, SearchJobs
|   |   |-- DTOs/                      # Request/response models
|   |   |-- Interfaces/                # IJobService, IJobPublisher, IJobSearchService
|   |   |-- Models/                    # JobExecutionResult, JobExecutionContext
|   |   +-- Validators/               # FluentValidation validators
|   |
|   |-- JobFlow.Domain/               # Pure domain logic (zero dependencies)
|   |   |-- Common/                    # BaseEntity, IDomainEvent
|   |   |   +-- Events/               # Concrete domain events
|   |   |-- Entities/                  # Job, OutboxMessage
|   |   |-- Enums/                     # JobStatus, JobPriority
|   |   +-- ValueObjects/             # JobMetadata
|   |
|   |-- JobFlow.Infrastructure/       # External service implementations
|   |   |-- DependencyInjection/       # Central DI registration
|   |   |-- EventHandlers/             # Domain event handlers (Mongo/ES sync)
|   |   |-- Migrations/                # EF Core migrations
|   |   |-- Models/                    # MongoDB document models
|   |   |-- Persistence/               # DbContext, EF configurations, repositories
|   |   +-- Services/                  # All service implementations
|   |
|   |-- JobFlow.Worker/               # Background job processor
|   |   |-- Cancellation/             # Per-job cancellation token management
|   |   |-- Configuration/            # WorkerOptions
|   |   |-- Execution/                # JobExecutor with timeout wrapping
|   |   |-- Handlers/                 # Pluggable job handlers (5 types)
|   |   |-- Messaging/                # DeadLetterPublisher
|   |   |-- Progress/                 # Progress reporting to DB
|   |   |-- Retry/                    # Exponential backoff with jitter
|   |   +-- Worker.cs                 # RabbitMQ consumer BackgroundService
|   |
|   |-- JobFlow.Contracts/            # Shared message contracts + gRPC proto
|   +-- JobFlow.Shared/               # Worker gRPC proto
|
|-- tests/
|   |-- JobFlow.UnitTests/            # 43 unit tests (validators, DTOs, domain, services)
|   +-- JobFlow.IntegrationTests/     # 13 integration tests (WebApplicationFactory)
|
|-- docker/                            # Docker infrastructure configs
|   |-- keycloak/realm/               # Keycloak realm import
|   |-- postgres/init/                # DB initialization SQL
|   +-- prometheus/                   # Prometheus scrape config
|
|-- docker-compose.yml                 # 12 services: infra + API + Worker
+-- .github/workflows/dotnet-ci.yml   # CI pipeline
```

---

## 5. Design Patterns Implemented

### CQRS (Command Query Responsibility Segregation)

Commands and queries are separate objects dispatched via MediatR. This allows independent scaling of read and write paths and clean separation of concerns.

```
POST /api/v1/jobs  -->  CreateJobCommand  -->  CreateJobCommandHandler
                                                  |
                                                  +--> UnitOfWork.SaveChanges()
                                                  +--> OutboxMessage (atomic with job)

GET /api/v1/jobs/{id}  -->  GetJobByIdQuery  -->  GetJobByIdQueryHandler
                                                     |
                                                     +--> UnitOfWork.Jobs.GetByIdAsync()
```

### Repository Pattern + Unit of Work

Services never touch `DbContext` directly. All data access goes through `IJobRepository` (read/write operations) coordinated by `IUnitOfWork` (transaction boundary + domain event dispatch).

### Domain Events

State transitions on the `Job` entity automatically raise domain events:
- `JobCreatedEvent` → Syncs job to MongoDB and Elasticsearch
- `JobStatusChangedEvent` → Updates status in MongoDB and Elasticsearch
- `JobCompletedEvent` → Logged for audit
- `JobFailedEvent` → Logged for alerting

Events are dispatched **after** `SaveChangesAsync()` succeeds (dispatch-after-save pattern), ensuring events only fire for persisted changes.

### Outbox Pattern

The `CreateJobCommandHandler` writes both the `Job` entity and an `OutboxMessage` in a single `SaveChangesAsync()` call — one database transaction. A background `OutboxProcessor` polls for unprocessed messages every 5 seconds and publishes them to RabbitMQ. This guarantees **at-least-once delivery** even if RabbitMQ is temporarily unavailable.

The `OutboxProcessor` uses PostgreSQL `FOR UPDATE SKIP LOCKED` within an explicit transaction to prevent duplicate processing when multiple API instances are running.

```
CreateJobCommandHandler:
  BEGIN TRANSACTION
    INSERT INTO Jobs (...)
    INSERT INTO OutboxMessages (Type='JobCreatedMessage', Payload='...')
  COMMIT

OutboxProcessor (every 5s):
  BEGIN TRANSACTION
    SELECT * FROM OutboxMessages
      WHERE ProcessedAtUtc IS NULL AND RetryCount < 5
      FOR UPDATE SKIP LOCKED   -- locks rows, skips already-locked ones
    FOR EACH message:
      Publish to RabbitMQ
      UPDATE OutboxMessages SET ProcessedAtUtc = NOW()
  COMMIT                       -- releases row locks
```

### Producer-Consumer Pattern

The API publishes messages to RabbitMQ (producer). The Worker consumes messages from the queue (consumer). They are fully decoupled — the API doesn't know about the Worker's existence.

---

## 6. Component Deep Dive

### Job Entity

The core domain entity with rich behavior:

```
Job
├── Properties: Name, Status, Priority, Payload, RetryCount, MaxRetries,
│               CompletedAtUtc, ErrorMessage, CreatedBy, ProgressPercentage,
│               Metadata (Tags, Source, ScheduledAtUtc)
├── State Machine (enforced with guards):
│     Pending ──→ Processing ──→ Completed (terminal)
│       │              │  ↺
│       │              │ (retry)
│       └──→ Failed ←──┘
│              │
│              └──→ Processing (retry path)
│     Completed → (anything) throws InvalidOperationException
├── Methods: MarkAsProcessing(), MarkAsCompleted(), MarkAsFailed(error),
│            IncrementRetry(), CanRetry(), UpdateProgress(%), SetError()
└── Events: Raises domain events on every state transition
```

### Job Handlers (Worker)

Five pluggable handlers, each identified by `JobType`. Two perform real work, three are simulations:

| Handler | JobType | What It Does |
|---|---|---|
| `ImageResizeJobHandler` | `ImageResize` | **Real** — Downloads image from URL, resizes with ImageSharp, saves to disk |
| `PdfJobHandler` | `PdfReport` | **Real** — Queries jobs from PostgreSQL, generates styled PDF report with QuestPDF |
| `ExternalApiJobHandler` | `DogApi` | **Real** — HTTP call to `https://dog.ceo/api/breeds/image/random` |
| `DataProcessingJobHandler` | `DataProcessing` | Simulation — 10 batches, 500ms each, progress 10→100% |
| `EmailJobHandler` | `Email` | Simulation — SMTP send, 2s delay |

The `JobExecutor` resolves the correct handler by matching `job.Name` to `handler.JobType`. If no handler matches, it runs a default 5-second simulation with progress updates.

#### Image Resize Payload

```json
{
  "url": "https://picsum.photos/1920/1080",
  "width": 800,
  "height": 600,
  "format": "png"
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `url` | string | *required* | Public URL of the image to download |
| `width` | int | 800 | Target width (maintains aspect ratio) |
| `height` | int | 600 | Target height (maintains aspect ratio) |
| `format` | string | `png` | Output format: `png`, `jpeg`, `webp`, `bmp` |

Output is saved to `{Worker:OutputDirectory}/{jobId}_resized.{format}`.

#### PDF Report Payload

```json
{
  "title": "Weekly Job Summary",
  "status": "Completed",
  "maxRows": 50
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `title` | string | `JobFlow Status Report` | Report title |
| `status` | string? | null (all statuses) | Filter by status: `Pending`, `Processing`, `Completed`, `Failed` |
| `maxRows` | int | 50 | Max jobs to include in the detail table |

Output is saved to `{Worker:OutputDirectory}/{jobId}_report.pdf`. The report includes a summary section (total/pending/processing/completed/failed counts) and a detail table with job name, status, priority, and creation date.

---

## 7. Request Flow — End to End

### What Happens When You Create a Job

```
1. Client sends POST /api/v1/jobs with Bearer token
     |
2. Middleware Pipeline:
     ├── RateLimiter: Check global (100/min) + job-submission (5/10s) limits
     ├── ExceptionHandlingMiddleware: Catches unhandled errors → proper HTTP codes
     ├── IdempotencyMiddleware: If Idempotency-Key header present, check Redis
     ├── CorrelationIdMiddleware: Read/generate X-Correlation-ID, push to Serilog context
     ├── Authentication: Validate JWT against Keycloak, extract realm_access roles
     └── Authorization: Verify JobFlowUserAccess policy (jobflow-user OR jobflow-admin)
     |
3. Endpoint Handler (JobEndpoints.cs):
     ├── Maps JobCreateRequest → CreateJobCommand
     └── Sends via MediatR ISender
     |
4. MediatR Pipeline:
     ├── ValidationBehavior: Runs CreateJobCommandValidator
     │   ├── Name required, max 200 chars
     │   ├── Priority must be valid enum
     │   ├── MaxRetries 0-10
     │   └── Payload max 1MB
     └── CreateJobCommandHandler:
          |
5. Command Handler:
     ├── Creates Job entity (raises JobCreatedEvent)
     ├── Creates OutboxMessage (serialized JobCreatedMessage)
     ├── UnitOfWork.SaveChangesAsync():
     │   ├── EF Core saves Job + OutboxMessage in ONE transaction
     │   └── Dispatches domain events:
     │       └── JobCreatedEventHandler:
     │           ├── Syncs to MongoDB (via IJobSynchronizer + Polly)
     │           └── Indexes in Elasticsearch (via IJobIndexer + Polly)
     └── Returns Job.Id
     |
6. Response: 201 Created with { id: "..." } and Location header
     |
7. OutboxProcessor (background, every 5s):
     ├── Reads unprocessed OutboxMessages from PostgreSQL
     ├── Deserializes JobCreatedMessage
     ├── Publishes to RabbitMQ exchange "jobflow.exchange" with routing key "job.created"
     └── Marks OutboxMessage as processed
     |
8. Worker (BackgroundService):
     ├── Consumer receives message from queue "jobflow.job-created"
     ├── Deserializes JobCreatedMessage
     ├── Loads Job entity from PostgreSQL via IUnitOfWork
     ├── Registers cancellation token via IJobCancellationService
     ├── Marks job as Processing (domain event → Mongo/ES sync)
     ├── Executes via IJobExecutor:
     │   ├── Resolves handler by job.Name
     │   ├── Wraps in timeout (default 300s)
     │   └── Handler runs with progress reporting
     ├── On success: marks Completed, ACKs message
     └── On failure: retry loop with exponential backoff
         ├── If retries exhausted: marks Failed, publishes to DLQ, ACKs
         └── DLQ: "jobflow.dead-letter" queue via "jobflow.dlx" exchange
```

---

## 8. API Reference

### Authentication

All endpoints require a valid JWT Bearer token from Keycloak with either `jobflow-user` or `jobflow-admin` realm role.

**Get a token:**
```bash
curl -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=jobflow-api" \
  -d "client_secret=YOUR_CLIENT_SECRET"
```

Use the `access_token` from the response as `Authorization: Bearer <token>` in subsequent requests.

### Create Job

```bash
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: unique-key-123" \
  -d '{
    "name": "DataProcessing",
    "priority": "High",
    "payload": {"inputFile": "data.csv", "outputFormat": "parquet"},
    "maxRetries": 5,
    "tags": ["etl", "batch"],
    "source": "analytics-service"
  }'
```

**Response (201 Created):**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```
**Headers:** `Location: /api/v1/jobs/a1b2c3d4-e5f6-7890-abcd-ef1234567890`

### Create Image Resize Job

```bash
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ImageResize",
    "priority": "High",
    "payload": {
      "url": "https://picsum.photos/1920/1080",
      "width": 400,
      "height": 300,
      "format": "jpeg"
    }
  }'
```

The Worker downloads the image, resizes it to 400x300 (maintaining aspect ratio), and saves the output to `output/{jobId}_resized.jpeg`.

### Create PDF Report Job

```bash
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "PdfReport",
    "priority": "Normal",
    "payload": {
      "title": "Weekly Job Summary",
      "status": "Completed",
      "maxRows": 25
    }
  }'
```

The Worker queries all completed jobs from PostgreSQL, generates a styled PDF report, and saves it to `output/{jobId}_report.pdf`.

### Create PDF Report (All Jobs, No Filter)

```bash
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "PdfReport"
  }'
```

When no payload is provided, the report includes all jobs with the default title "JobFlow Status Report".

### Get Job by ID

```bash
curl http://localhost:5001/api/v1/jobs/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer <token>"
```

**Response (200 OK):**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "DataProcessing",
  "status": "Completed",
  "priority": "High",
  "retryCount": 0,
  "maxRetries": 5,
  "progressPercentage": 100,
  "errorMessage": null,
  "createdBy": null,
  "createdAtUtc": "2026-07-20T10:30:00Z",
  "updatedAtUtc": "2026-07-20T10:30:25Z",
  "completedAtUtc": "2026-07-20T10:30:25Z"
}
```

### Search Jobs

```bash
curl "http://localhost:5001/api/v1/jobs?query=data&status=Completed&page=1&pageSize=10&sortBy=CreatedAtUtc&sortOrder=desc" \
  -H "Authorization: Bearer <token>"
```

**Response (200 OK):**
```json
{
  "jobs": [
    {
      "id": "a1b2c3d4-...",
      "name": "DataProcessing",
      "status": "Completed",
      "priority": "High",
      "retryCount": 0,
      "maxRetries": 5,
      "progressPercentage": 100,
      "errorMessage": null,
      "createdBy": null,
      "createdAtUtc": "2026-07-20T10:30:00Z",
      "updatedAtUtc": "2026-07-20T10:30:25Z",
      "completedAtUtc": "2026-07-20T10:30:25Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "total": 1
}
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `query` | string? | null | Full-text search across name and payload |
| `status` | string? | null | Filter by status: Pending, Processing, Completed, Failed |
| `createdAfterUtc` | DateTime? | null | Filter jobs created after this date |
| `createdBeforeUtc` | DateTime? | null | Filter jobs created before this date |
| `page` | int | 1 | Page number (1-based) |
| `pageSize` | int | 20 | Results per page (1-100) |
| `sortBy` | string | CreatedAtUtc | Sort field: CreatedAtUtc or UpdatedAtUtc |
| `sortOrder` | string | desc | Sort direction: asc or desc |

### Get Current Identity

```bash
curl http://localhost:5001/api/v1/identity/me \
  -H "Authorization: Bearer <token>"
```

**Response (200 OK):**
```json
{
  "subject": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "username": "john.doe",
  "roles": ["jobflow-admin", "jobflow-user"]
}
```

### Validation Error Response (400)

```bash
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"name": "", "maxRetries": 99}'
```

**Response (400 Bad Request):**
```json
{
  "errors": [
    "'Name' must not be empty.",
    "'Max Retries' must be between 0 and 10."
  ]
}
```

### Rate Limited Response (429)

```json
// After exceeding 5 job submissions in 10 seconds
// HTTP 429 Too Many Requests
```

### Idempotent Duplicate Response (409)

```bash
# Second request with same Idempotency-Key within 10 minutes
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Idempotency-Key: unique-key-123" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"name": "DuplicateJob"}'
```

**Response (409 Conflict):**
```json
{
  "message": "Duplicate request detected"
}
```

### GraphQL

```bash
curl -X POST http://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -d '{
    "query": "{ job(id: \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\") { id name status priority progressPercentage } }"
  }'
```

**Response:**
```json
{
  "data": {
    "job": {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "name": "DataProcessing",
      "status": "Completed",
      "priority": "High",
      "progressPercentage": 100
    }
  }
}
```

### gRPC (via grpcurl)

```bash
grpcurl -plaintext -d '{"job_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"}' \
  localhost:5001 jobservice.JobGrpcService/GetJobStatus
```

**Response:**
```json
{
  "jobId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Completed",
  "updatedAtUtc": "2026-07-20T10:30:25.000Z"
}
```

### Health Checks

```bash
# Full health check (all dependencies)
curl http://localhost:5001/health

# Readiness (all checks)
curl http://localhost:5001/ready

# Liveness (no dependency checks)
curl http://localhost:5001/live
```

**Response (200 OK):** `Healthy`
**Response (503 Service Unavailable):** `Unhealthy`

---

## 9. Worker Processing Pipeline

```
Message arrives from RabbitMQ
  |
  v
Deserialize JobCreatedMessage
  |
  v
Load Job from PostgreSQL (via IUnitOfWork)
  |
  v
Register cancellation token (IJobCancellationService)
  |
  v
Mark as Processing + Save (domain event → Mongo/ES sync)
  |
  v
Execute via IJobExecutor
  |-- Resolve handler by job.Name → handler.JobType
  |-- Wrap in timeout (default 300s)
  |-- Handler runs (with progress reporting via IProgressReporter)
  |
  +--[SUCCESS]--→ Mark Completed + ACK message
  |
  +--[FAILURE]--→ Retry Loop
                    |
                    +-- While CanRetry() && ShouldRetry():
                    |     |-- IncrementRetry()
                    |     |-- Wait: baseDelay * 2^attempt + jitter
                    |     |     (2s base: ~4s, ~8s, ~16s)
                    |     |-- Re-execute
                    |     +-- If success: Mark Completed + ACK
                    |
                    +-- Retries exhausted:
                          |-- Mark Failed
                          |-- Publish to Dead Letter Queue
                          +-- ACK original message
```

### Dead Letter Queue

Failed messages are published to `jobflow.dlx` (fanout exchange) → `jobflow.dead-letter` (durable queue) with:
- `x-death-reason` header containing the failure reason
- All original message headers preserved with `x-original-` prefix
- Persistent delivery mode

---

## 10. Resilience & Fault Tolerance

### Polly Resilience Pipelines

Four named pipelines registered via a shared `BuildResiliencePipeline()` helper:

| Pipeline | Retry | Circuit Breaker | Timeout |
|---|---|---|---|
| **Default** | 3 attempts, 2s exponential | No | No |
| **RabbitMQ** (`rabbitmq`) | 3 attempts, 1s exponential | 50% failure ratio, 30s break | 10s |
| **MongoDB** (`mongodb`) | 3 attempts, 1s exponential | 50% failure ratio, 30s break | 15s |
| **Elasticsearch** (`elasticsearch`) | 3 attempts, 1s exponential | 50% failure ratio, 30s break | 15s |

### Circuit Breaker Behavior

When a downstream service fails:
1. **Closed** (normal) — requests pass through, failures are counted
2. **Open** (tripped) — if failure ratio exceeds 50% within 30s (min 5 requests), circuit opens for 30s. All requests fail fast.
3. **Half-Open** — after break duration, one request is allowed through to test if the service recovered

### Worker Retry Policy

Exponential backoff with jitter prevents thundering herd:
- Delay = `baseDelay * 2^attempt + random(0-1s)`
- With default 2s base: ~4s, ~8s, ~16s
- `OperationCanceledException` is never retried
- Max retries configurable via `Worker:MaxRetries` (default 3)

### Outbox Pattern (Reliable Messaging)

Guarantees message delivery even if RabbitMQ is temporarily down:
- Job + OutboxMessage saved in single DB transaction
- Background processor polls every 5 seconds within an explicit PostgreSQL transaction
- Uses `FOR UPDATE SKIP LOCKED` to prevent duplicate processing across multiple API instances
- Retries up to 5 times per message
- Failed messages retain error details for debugging

---

## 11. Observability

### Structured Logging (Serilog)

Every log entry includes:
- Timestamp, log level, message
- `CorrelationId` (from `X-Correlation-ID` header, auto-generated if missing)
- Machine name, thread ID
- Serilog enrichment from LogContext

```
[10:30:00 INF] abc123 Processing job: a1b2c3d4-e5f6-7890-abcd-ef1234567890
[10:30:05 INF] abc123 Job a1b2c3d4-e5f6-7890-abcd-ef1234567890 completed in 00:00:05.
```

### Distributed Tracing (OpenTelemetry + Jaeger)

Traces span the entire flow: API → RabbitMQ → Worker → PostgreSQL → MongoDB → Elasticsearch.

- Instrumented: ASP.NET Core, HTTP client, EF Core, custom `JobFlow.Worker` activity source
- Export: OTLP to Jaeger (port 4317)
- Jaeger UI: `http://localhost:16686`

### Metrics (Prometheus + Grafana)

- Prometheus scrapes `/metrics` endpoint on the API
- Instrumented: ASP.NET Core request metrics
- Prometheus UI: `http://localhost:9090`
- Grafana dashboards: `http://localhost:3000`

### Correlation IDs

Every request gets a `X-Correlation-ID` header (read from request or auto-generated). This ID:
- Is pushed into Serilog LogContext (appears in all log entries)
- Is echoed back on the response
- Is included in RabbitMQ message headers
- Enables tracing a single request across all services and logs

---

## 12. Security

### Authentication Flow

```
Client → Keycloak (OAuth2/OIDC) → JWT Token → API (Bearer validation)
```

1. Client authenticates with Keycloak (`http://localhost:8080/realms/jobflow`)
2. Receives JWT containing `realm_access.roles`
3. Sends JWT as `Authorization: Bearer <token>` to API
4. API validates token signature against Keycloak's JWKS endpoint
5. `KeycloakRealmRoleClaimsTransformation` extracts realm roles into `ClaimTypes.Role`

### Authorization (RBAC)

| Policy | Required Roles | Endpoints |
|---|---|---|
| `JobFlowUserAccess` | `jobflow-user` OR `jobflow-admin` | All endpoints |

### Rate Limiting

| Policy | Window | Limit | Scope |
|---|---|---|---|
| Global | 1 minute | 100 requests | Per IP |
| Job Submission | 10 seconds | 5 requests | Per authenticated user |

### Idempotency

POST endpoints accept an `Idempotency-Key` header. The key is stored in Redis with a 10-minute TTL. Duplicate requests within the window return `409 Conflict`.

### Input Validation

FluentValidation runs as a MediatR pipeline behavior **before** the command handler executes:
- Job name: required, max 200 chars
- Priority: must be a valid enum value
- Max retries: 0-10
- Payload: max 1MB
- Search page size: 1-100

---

## 13. Infrastructure & DevOps

### Docker Compose Services (12 total)

```bash
docker-compose up -d
```

| Service | Port | UI |
|---|---|---|
| PostgreSQL | 5432 | — |
| Keycloak | 8080 | http://localhost:8080 |
| Redis | 6379 | — |
| RabbitMQ | 5672, 15672 | http://localhost:15672 |
| MongoDB | 27017 | — |
| Elasticsearch | 9200 | — |
| Kibana | 5601 | http://localhost:5601 |
| Jaeger | 16686, 4317 | http://localhost:16686 |
| Prometheus | 9090 | http://localhost:9090 |
| Grafana | 3000 | http://localhost:3000 |
| JobFlow API | 5001 | http://localhost:5001/swagger |
| JobFlow Worker | (internal) | — |

### CI Pipeline (GitHub Actions)

Triggers on push to `main`/`feature/*` and PRs to `main`:

1. Checkout code
2. Setup .NET 9.0.x
3. `dotnet restore`
4. `dotnet build --configuration Release`
5. `dotnet test --configuration Release`
6. Build Docker images (`jobflow-api:ci`, `jobflow-worker:ci`)

### Dockerfiles

Both API and Worker use multi-stage builds:
- **Build stage:** .NET SDK 9.0 — restore, publish
- **Runtime stage:** .NET ASP.NET 9.0 — minimal image, port 8080

---

## 14. Testing Strategy

### Unit Tests (43 tests)

- **Domain entity tests** — Job state machine with guards (valid/invalid transitions), constructor behavior
- **DTO tests** — Record construction and property access
- **Validator tests** — CreateJobCommand, SearchJobsQuery, UpdateJobStatusCommand validation rules
- **Service tests** — RedisIdempotencyService, ElasticsearchIndexInitializer

### Integration Tests (13 tests)

- **WebApplicationFactory** — Spins up the API in-memory with test doubles
- **Job creation** — Happy path (201 + Location header), minimal fields, validation errors (empty name, long name, invalid priority, MaxRetries limit)
- **Job retrieval** — GET by ID returns full response, 404 for nonexistent jobs
- **Search** — Paged results, create-then-get roundtrip
- **Middleware** — Correlation ID echoed back, auto-generated when absent
- **Health checks** — Liveness endpoint returns 200

### Test Doubles

| Real Service | Test Double |
|---|---|
| `IUnitOfWork` | `TestUnitOfWork` (in-memory collections) |
| `IJobRepository` | `TestJobRepository` (in-memory dictionary) |
| `IJobSynchronizer` | No-op implementation |
| `IJobIndexer` | No-op implementation |
| `IJobPublisher` | `TestJobPublisher` (no-op) |
| `IJobSearchService` | `TestJobSearchService` (in-memory) |
| Redis | `DistributedMemoryCache` |
| `IIdempotencyService` | `RedisIdempotencyService` (backed by `DistributedMemoryCache`) |
| `ResiliencePipeline` | Default (no retry) |
| RabbitMQ | `RabbitMqConnectionInitializer` skips via `Test:SkipExternalInitializers` |

---

## 15. Configuration Reference

### API `appsettings.json`

All infrastructure configuration values are **required** — the application throws `InvalidOperationException` on startup if any are missing. Local defaults are provided in `appsettings.json`; Docker overrides them via environment variables in `docker-compose.yml`.

| Key | Required | Description |
|---|---|---|
| `ConnectionStrings:JobFlowDb` | Yes | PostgreSQL connection string |
| `Redis:Configuration` | Yes | Redis connection |
| `Redis:InstanceName` | No (default: `JobFlow:`) | Redis key prefix |
| `RabbitMq:Host` | Yes | RabbitMQ hostname |
| `RabbitMq:Username` | Yes | RabbitMQ user |
| `RabbitMq:Password` | Yes | RabbitMQ password |
| `MongoDb:ConnectionString` | Yes | MongoDB connection |
| `MongoDb:Database` | No (default: `jobflow`) | MongoDB database name |
| `Elasticsearch:Url` | Yes | Elasticsearch URL |
| `Elasticsearch:DefaultIndex` | No (default: `jobflow-jobs`) | Elasticsearch index name |
| `Otlp:Endpoint` | Yes | OpenTelemetry collector endpoint |
| `Authentication:Authority` | Yes | Keycloak realm URL |
| `Authentication:Audience` | Yes | JWT audience |

### Worker-Specific

| Key | Default | Description |
|---|---|---|
| `Worker:MaxConcurrency` | 4 | Max concurrent job processing |
| `Worker:MaxRetries` | 3 | Retry attempts before DLQ |
| `Worker:RetryBaseDelaySeconds` | 2 | Base delay for exponential backoff |
| `Worker:JobTimeoutSeconds` | 300 | Per-job execution timeout |

---

## 16. How to Run

### Prerequisites

- .NET 9 SDK
- Docker & Docker Compose

### Start Infrastructure

```bash
# Start all backing services
docker-compose up -d postgres redis rabbitmq mongodb elasticsearch keycloak jaeger prometheus grafana kibana
```

### Apply Database Migrations

```bash
dotnet ef database update --project src/JobFlow.Infrastructure --startup-project src/JobFlow.Api
```

### Run the API

```bash
cd src/JobFlow.Api
dotnet run
```

API available at `http://localhost:5001` | Swagger at `http://localhost:5001/swagger`

### Run the Worker

```bash
cd src/JobFlow.Worker
dotnet run
```

### Run Everything in Docker

```bash
docker-compose up -d
```

This starts all 12 services including the API (port 5001) and Worker.

### Run Tests

```bash
dotnet test JobFlow.sln
```

### Quick Smoke Test

```bash
# 1. Get a token from Keycloak (configure client credentials first)
TOKEN=$(curl -s -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -d "grant_type=client_credentials&client_id=jobflow-api&client_secret=YOUR_SECRET" \
  | python -m json.tool | grep access_token | cut -d'"' -f4)

# 2. Create a job
curl -X POST http://localhost:5001/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "DataProcessing", "priority": "High"}'

# 3. Check job status (replace ID)
curl http://localhost:5001/api/v1/jobs/{JOB_ID} \
  -H "Authorization: Bearer $TOKEN"

# 4. Search jobs
curl "http://localhost:5001/api/v1/jobs?status=Completed&page=1&pageSize=5" \
  -H "Authorization: Bearer $TOKEN"

# 5. Check health
curl http://localhost:5001/health
```
