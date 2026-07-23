# JobFlow — API Usage Guide

Complete end-to-end flow for interacting with the JobFlow API.

> **Note:** All examples use `http://localhost:5274` (API) and `http://localhost:8080` (Keycloak).  
> A pre-configured user exists in the realm: `demo` / `demo123` with the `jobflow-user` role.
> To disable RabbitMQ/MongoDB/Elasticsearch initialization at startup (e.g., for testing), set `Test:SkipExternalInitializers=true` in `appsettings.Development.json`.

---

## Authentication Flow

JobFlow uses Keycloak for OAuth 2.0 / OpenID Connect authentication. Every API request requires a valid JWT Bearer token.

```
┌──────┐         ┌──────────┐         ┌────────────┐
│ User │──(1)───→│ Keycloak │         │ JobFlow API│
│      │←─token──│          │         │            │
│      │──(2)──Bearer token──────────→│            │
│      │←─────── response ───────────│            │
└──────┘         └──────────┘         └────────────┘
```

### Step 1: Get an Access Token

The realm comes with a pre-configured demo user. Use **Password Grant** for human-driven flows:

```bash
curl -s -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=jobflow-api" \
  -d "username=demo" \
  -d "password=demo123"
```

> The `jobflow-api` client is a **public client** (no `client_secret` needed) with `Direct Access Grants Enabled`.

**Response:**

```json
{
  "access_token": "eyJhbGciOi...",
  "expires_in": 300,
  "refresh_expires_in": 1800,
  "refresh_token": "eyJhbGciOi...",
  "token_type": "Bearer"
}
```

Save both tokens:

```bash
export TOKEN="eyJhbGciOi..."
export REFRESH_TOKEN="eyJhbGciOi..."
```

| Token | Lifetime | Purpose |
|---|---|---|
| `access_token` | 5 minutes (300s) | Sent with every API request |
| `refresh_token` | 30 minutes (1800s) | Used to get a new access token without re-entering credentials |

### Step 2: Refresh an Expired Token

When the access token expires (after ~5 minutes), use the refresh token to get a new one without re-authenticating:

```bash
curl -s -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -d "grant_type=refresh_token" \
  -d "client_id=jobflow-api" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "refresh_token=$REFRESH_TOKEN"
```

This returns a new `access_token` and a new `refresh_token`. Update both:

```bash
export TOKEN="<new access_token>"
export REFRESH_TOKEN="<new refresh_token>"
```

When the refresh token itself expires (after ~30 minutes), you must re-authenticate from scratch (Step 1).

> **Note:** Client credentials grant (`grant_type=client_credentials`) does not return a refresh token. Only the password grant does.

---

## Creating Jobs

### Create an Image Resize Job

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
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

**Response (201 Created):**

```json
{ "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }
```

The Worker downloads the image, resizes it, and saves to `output/{jobId}_resized.jpeg`.

### Create a PDF Report Job

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
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

The Worker queries jobs from PostgreSQL, generates a styled PDF, and saves to `output/{jobId}_report.pdf`.

### Create a Data Processing Job (Simulation)

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "DataProcessing",
    "priority": "High",
    "payload": {"inputFile": "data.csv", "outputFormat": "parquet"}
  }'
```

### Create a Job with Idempotency Key

Prevents duplicate job creation if the request is retried:

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: unique-request-id-123" \
  -d '{"name": "DataProcessing", "priority": "Normal"}'
```

A second request with the same `Idempotency-Key` within 10 minutes returns `409 Conflict`.

### Create a Job with Minimal Fields

Only `name` is required:

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Email"}'
```

Defaults: `priority=Normal`, `maxRetries=3`, no payload.

---

## Checking Job Status

### Get Job by ID

```bash
curl http://localhost:5274/api/v1/jobs/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer $TOKEN"
```

**Response (200 OK):**

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "ImageResize",
  "status": "Completed",
  "priority": "High",
  "retryCount": 0,
  "maxRetries": 3,
  "progressPercentage": 100,
  "errorMessage": null,
  "createdBy": null,
  "createdAtUtc": "2026-07-20T10:30:00Z",
  "updatedAtUtc": "2026-07-20T10:30:12Z",
  "completedAtUtc": "2026-07-20T10:30:12Z"
}
```

**Job Status Values:**

| Status | Meaning |
|---|---|
| `Pending` | Job created, waiting for Worker to pick it up |
| `Processing` | Worker is executing the job |
| `Completed` | Job finished successfully |
| `Failed` | Job failed after all retry attempts (sent to dead-letter queue) |

### Search Jobs

```bash
curl "http://localhost:5274/api/v1/jobs?query=resize&status=Completed&page=1&pageSize=10&sortBy=CreatedAtUtc&sortOrder=desc" \
  -H "Authorization: Bearer $TOKEN"
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `query` | string | — | Full-text search across name and payload |
| `status` | string | — | Filter: `Pending`, `Processing`, `Completed`, `Failed` |
| `createdAfterUtc` | DateTime | — | Jobs created after this date |
| `createdBeforeUtc` | DateTime | — | Jobs created before this date |
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Results per page (1-100) |
| `sortBy` | string | `CreatedAtUtc` | Sort field: `CreatedAtUtc` or `UpdatedAtUtc` |
| `sortOrder` | string | `desc` | `asc` or `desc` |

---

## GraphQL

```bash
curl -X POST http://localhost:5274/graphql \
  -H "Content-Type: application/json" \
  -d '{
    "query": "{ job(id: \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\") { id name status priority progressPercentage } }"
  }'
```

---

## gRPC

```bash
grpcurl -plaintext -d '{"job_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"}' \
  localhost:5274 jobservice.JobGrpcService/GetJobStatus
```

---

## Identity

Check who you're authenticated as:

```bash
curl http://localhost:5274/api/v1/identity/me \
  -H "Authorization: Bearer $TOKEN"
```

**Response:**

```json
{
  "subject": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "username": "testuser",
  "roles": ["jobflow-user"]
}
```

---

## Health Checks

```bash
# Full health check (all dependencies)
curl http://localhost:5274/health

# Readiness (all dependency checks)
curl http://localhost:5274/ready

# Liveness (no dependency checks — just "is the process alive?")
curl http://localhost:5274/live
```

---

## Error Responses

### 400 Bad Request (Validation Error)

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "", "maxRetries": 99}'
```

```json
{
  "errors": [
    "'Name' must not be empty.",
    "'Max Retries' must be between 0 and 10."
  ]
}
```

### 401 Unauthorized (Missing or invalid token)

No `Authorization` header, or token is expired.

### 404 Not Found

```bash
curl http://localhost:5274/api/v1/jobs/00000000-0000-0000-0000-000000000000 \
  -H "Authorization: Bearer $TOKEN"
```

### 409 Conflict (Duplicate idempotency key)

```json
{ "message": "Request with this Idempotency-Key is currently being processed." }
```

### 429 Too Many Requests (Rate limited)

Global limit: 100 requests/minute per IP.

---

## What Happens Behind the Scenes

```
┌──────┐    ┌──────────┐    ┌────────────┐    ┌──────────┐    ┌────────┐
│ User │───→│ Keycloak │───→│ JobFlow API│───→│ RabbitMQ │───→│ Worker │
│      │ 1. │ Get JWT  │ 2. │ Create Job │ 3. │ Message  │ 4. │Process │
│      │←───│          │←───│ 201 + ID   │    │ Queue    │    │& Save  │
│      │    └──────────┘    └─────┬──────┘    └──────────┘    └───┬────┘
│      │                          │                               │
│      │                    ┌─────┴──────┐                  ┌─────┴─────┐
│      │                    │ PostgreSQL │                  │ output/   │
│      │                    │ MongoDB    │                  │ files     │
│      │                    │ Elastic    │                  └───────────┘
│      │                    │ Redis      │
│      │ 5. GET /jobs/{id}  └────────────┘
│      │───→ API reads from PostgreSQL ───→ Returns job status
└──────┘
```

**Detailed flow for job creation:**

1. **User** authenticates with Keycloak, gets JWT
2. **User** sends `POST /api/v1/jobs` with Bearer token
3. **API middleware pipeline**: rate limiter → exception handler → idempotency check → correlation ID → JWT validation → authorization
4. **MediatR pipeline**: FluentValidation → CreateJobCommandHandler
5. **Handler** saves `Job` + `OutboxMessage` in a single PostgreSQL transaction
6. **Domain events** fire: job synced to MongoDB + indexed in Elasticsearch
7. **OutboxProcessor** (every 5s) picks up the outbox message, publishes to RabbitMQ
8. **Worker** consumes the message, loads the job, marks it `Processing`
9. **Job handler** executes (image resize, PDF generation, etc.)
10. **Worker** marks the job `Completed` (or retries → `Failed` → dead-letter queue)
11. **User** queries `GET /jobs/{id}` to check status
