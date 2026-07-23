# JobFlow — Setup Guide

Step-by-step instructions to get the project running from a fresh clone.

---

## Prerequisites

| Tool | Version | Check Command |
|---|---|---|
| .NET SDK | 9.0.300+ | `dotnet --version` |
| Docker Desktop | Latest | `docker --version` |
| Docker Compose | v2+ | `docker compose version` |
| Git | Any | `git --version` |
| k6 (optional) | Latest | `k6 version` — for load testing |

---

## Step 1: Clone, Configure & Build

```bash
git clone https://github.com/geetesh2/JobFlow.git
cd JobFlow
```

Create the `.env` file (required by docker-compose):

```bash
cp .env.example .env
```

Edit `.env` with your local passwords:

```env
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your-strong-local-password
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=your-strong-local-password
```

Build the solution:

```bash
dotnet restore
dotnet build
```

Verify: `0 errors, 0 warnings`

---

## Step 2: Start Infrastructure (Docker)

```bash
docker-compose up -d postgres redis rabbitmq mongodb elasticsearch keycloak
```

Wait for all services to become healthy (~30-60 seconds on first run due to image pulls):

```bash
docker-compose ps
```

All containers should show `healthy` status. Keycloak is the slowest — it depends on PostgreSQL being ready first.

| Service | Port | Verify |
|---|---|---|
| PostgreSQL | 5432 | `docker exec jobflow-postgres pg_isready` |
| Redis | 6379 | `docker exec jobflow-redis redis-cli ping` → `PONG` |
| RabbitMQ | 5672, 15672 | http://localhost:15672 (`jobflow` / `jobflow-local-rabbitmq-password`) |
| MongoDB | 27017 | `docker exec jobflow-mongodb mongosh --eval "db.runCommand({ping:1})"` |
| Elasticsearch | 9200 | http://localhost:9200/_cluster/health |
| Keycloak | 8080 | http://localhost:8080 (credentials from your `.env`) |

---

## Step 3: Apply Database Migrations

Install the EF Core CLI tool (one-time):

```bash
dotnet tool install --global dotnet-ef
```

Apply migrations to create the `Jobs` and `OutboxMessages` tables:

```bash
dotnet ef database update --project src/JobFlow.Infrastructure --startup-project src/JobFlow.Api
```

Verify:

```bash
docker exec jobflow-postgres psql -U postgres -d JobFlowDb -c "\dt"
```

You should see:
- `Jobs`
- `OutboxMessages`
- `__EFMigrationsHistory`

---

## Step 4: Configure Keycloak

The realm is auto-imported from `docker/keycloak/realm/jobflow-realm.json` on first start. It includes a pre-configured client and demo user.

### 4a. Verify the Realm

1. Go to http://localhost:8080
2. Login with the admin credentials from your `.env` file
3. Select the `jobflow` realm from the dropdown (top-left)
4. Go to **Clients** → verify `jobflow-api` client exists (it's a **public client** — no secret required)
5. Go to **Users** → verify `demo` user exists with credentials `demo` / `demo123` and role `jobflow-user`

### 4b. Get an Access Token (using pre-configured demo user)

```bash
curl -s -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=jobflow-api" \
  -d "username=demo" \
  -d "password=demo123"
```

Save the `access_token` value:
```bash
export TOKEN="eyJhbGciOi..."
```

---

## Step 5: Run the API

```bash
cd src/JobFlow.Api
dotnet run
```

The API starts on `http://localhost:5274`.

Verify:
- Health check: `curl http://localhost:5274/health`
- Liveness: `curl http://localhost:5274/live`

---

## Step 6: Run the Worker

Open a **second terminal**:

```bash
cd src/JobFlow.Worker
dotnet run
```

You should see:
```
Worker started and listening for job.created messages.
```

---

## Step 7: Smoke Test

### Test 1: Data Processing (Simulation)

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "DataProcessing", "priority": "High"}'

# Response: {"id":"<GUID>"}
# After ~5-10 seconds, check status:
curl http://localhost:5274/api/v1/jobs/<GUID> -H "Authorization: Bearer $TOKEN"
# Status should be "Completed" with progressPercentage: 100
```

### Test 2: Image Resize (Real — downloads and resizes an image)

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ImageResize",
    "priority": "Normal",
    "payload": {
      "url": "https://picsum.photos/1920/1080",
      "width": 400,
      "height": 300,
      "format": "jpeg"
    }
  }'

# After completion, check the Worker's output/ directory for {jobId}_resized.jpeg
```

### Test 3: PDF Report (Real — queries jobs from DB and generates a styled PDF)

```bash
curl -X POST http://localhost:5274/api/v1/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "PdfReport",
    "payload": {
      "title": "My First Report",
      "maxRows": 10
    }
  }'

# After completion, check the Worker's output/ directory for {jobId}_report.pdf
```

### Test 4: Search Jobs

```bash
curl "http://localhost:5274/api/v1/jobs?status=Completed&page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

### Test 5: GraphQL

```bash
curl -X POST http://localhost:5274/graphql \
  -H "Content-Type: application/json" \
  -d '{"query": "{ job(id: \"<GUID>\") { id name status priority progressPercentage } }"}'
```

---

## Step 8: Start Observability Stack (Optional)

```bash
docker-compose up -d jaeger prometheus grafana kibana
```

| Tool | URL | Purpose |
|---|---|---|
| Jaeger | http://localhost:16686 | Distributed traces |
| Prometheus | http://localhost:9090 | Metrics |
| Grafana | http://localhost:3000 | Dashboards (admin/admin) |
| Kibana | http://localhost:5601 | Elasticsearch log viewer |
| RabbitMQ Management | http://localhost:15672 | Queue monitoring |

---

## Step 9: Run Everything in Docker (Alternative)

Instead of steps 5-6, you can run the API and Worker as containers:

```bash
docker-compose up -d
```

This builds and starts all 12 services. The API is at `http://localhost:5274`.

Make sure your `.env` file exists — docker-compose will fail without it.

---

## Running Tests

```bash
# From project root
dotnet test
```

Expected output: **43 unit tests + 13 integration tests = 56 total**, all passing.

Tests use in-memory fakes — no Docker infrastructure needed. External services (RabbitMQ, MongoDB, Elasticsearch) are skipped via the `Test:SkipExternalInitializers` flag.

---

## Load Testing (Optional)

Install [k6](https://k6.io/docs/get-started/installation/), then:

```bash
k6 run --env TOKEN=$TOKEN tests/load/load-test.js
```

Runs three scenarios over ~4 minutes: ramping job creation (up to 25 VUs), constant reads (5 VUs), and a spike test (50 VUs). Results are saved to `tests/load/results.json`.

See [tests/load/README.md](../tests/load/README.md) for details.

---

## Configuration

All infrastructure configuration is **required** — the application throws `InvalidOperationException` on startup if any value is missing. Local defaults are provided in `appsettings.json`; Docker overrides them via environment variables in `docker-compose.yml`.

| Key | Description |
|---|---|
| `ConnectionStrings:JobFlowDb` | PostgreSQL connection string |
| `Database:Password` | PostgreSQL password (in `appsettings.Development.json`) |
| `Redis:Configuration` | Redis connection (e.g., `localhost:6379`) |
| `MongoDb:ConnectionString` | MongoDB connection (e.g., `mongodb://localhost:27017`) |
| `Elasticsearch:Url` | Elasticsearch URL (e.g., `http://localhost:9200`) |
| `RabbitMq:Host` | RabbitMQ hostname |
| `RabbitMq:Username` | RabbitMQ user |
| `RabbitMq:Password` | RabbitMQ password |
| `Otlp:Endpoint` | OpenTelemetry collector endpoint |
| `Authentication:Authority` | Keycloak realm URL |
| `Worker:OutputDirectory` | Where image/PDF outputs are saved (default: `output/`) |

---

## Troubleshooting

### "Connection refused" to PostgreSQL/Redis/RabbitMQ
Docker containers aren't running. Run `docker-compose up -d` and wait for healthy status.

### docker-compose fails with "Set POSTGRES_USER in .env"
The `.env` file is missing. Run `cp .env.example .env` and fill in the values.

### EF Migration fails with "relation already exists"
The migration was already applied. This is safe to ignore. To verify: `dotnet ef migrations list --project src/JobFlow.Infrastructure --startup-project src/JobFlow.Api`

### Application fails with "X is not configured"
All infrastructure config values are now required. Make sure `appsettings.json` has all sections (`Redis`, `MongoDb`, `Elasticsearch`, `RabbitMq`, `Otlp`). For Docker, check the environment variables in `docker-compose.yml`.

### Keycloak returns 401 on token request
- Verify client secret matches (Keycloak admin → Clients → jobflow-api → Credentials)
- Verify the realm name is `jobflow` (not `master`)
- If using password grant, verify the user has the `jobflow-user` role assigned

### Worker says "Job not found while processing message"
The job was created in the API's database but the Worker can't find it. Both must point to the same PostgreSQL instance. Check `ConnectionStrings:JobFlowDb` in both `appsettings.json` files.

### RabbitMQ "queue not found" or messages not arriving
The exchange/queue/binding is declared on first use by both API (via OutboxProcessor → RabbitMqJobPublisher) and Worker. Restart the Worker if queues were deleted manually.

### Image resize fails with "response status code does not indicate success"
The image URL is unreachable or returned an error. Use a public URL like `https://picsum.photos/800/600`. The Worker needs internet access to download images.

### Port conflicts
Default ports used: 5274 (API), 5275 (Worker), 5432 (PG), 6379 (Redis), 5672/15672 (RabbitMQ), 27017 (Mongo), 9200 (ES), 8080 (Keycloak), 16686 (Jaeger), 9090 (Prometheus), 3000 (Grafana), 5601 (Kibana). Change in `docker-compose.yml` and `launchSettings.json` if any conflict.

---

## Quick Reference — Ports

```
API:            http://localhost:5274
Health:         http://localhost:5274/health
Worker:         http://localhost:5275
GraphQL:        http://localhost:5274/graphql
Keycloak:       http://localhost:8080
RabbitMQ Mgmt:  http://localhost:15672
Jaeger:         http://localhost:16686
Prometheus:     http://localhost:9090
Grafana:        http://localhost:3000
Kibana:         http://localhost:5601
PostgreSQL:     localhost:5432
Redis:          localhost:6379
MongoDB:        localhost:27017
Elasticsearch:  localhost:9200
```
