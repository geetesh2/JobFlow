# Load Testing with k6

Performance tests for the JobFlow API using [k6](https://k6.io/).

## Prerequisites

Install k6:

```bash
# Windows (winget)
winget install k6

# macOS
brew install k6

# Linux
sudo snap install k6
```

## Get an Auth Token

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/realms/jobflow/protocol/openid-connect/token \
  -d "grant_type=client_credentials&client_id=jobflow-api&client_secret=YOUR_SECRET" \
  | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)
```

## Run the Full Test

```bash
k6 run --env TOKEN=$TOKEN tests/load/load-test.js
```

## Test Scenarios

The load test runs three scenarios:

| Scenario | Duration | VUs | What It Tests |
|---|---|---|---|
| **create_jobs** | 3m | 1 → 10 → 25 → 0 | Ramping job creation load |
| **read_jobs** | 3m | 5 constant | Search + get-by-ID under steady read load |
| **spike** | 40s | 0 → 50 → 0 | Sudden traffic spike after main test |

## Thresholds

| Metric | Target | Description |
|---|---|---|
| `http_req_duration p(95)` | < 2000ms | Overall request latency |
| `job_creation_duration p(95)` | < 1500ms | POST /api/v1/jobs |
| `job_retrieval_duration p(95)` | < 500ms | GET /api/v1/jobs/{id} |
| `job_search_duration p(95)` | < 1000ms | GET /api/v1/jobs (search) |
| `failed_requests` | < 5% | Error rate |

## Quick Smoke Test (lower load)

```bash
k6 run --env TOKEN=$TOKEN --vus 5 --duration 30s tests/load/load-test.js
```

## Output

Results are saved to `tests/load/results.json` after each run with key metrics:
- Total requests and requests/sec
- Jobs created count
- p95 latencies for create, retrieve, and search
- Failure rate

## Sample Output

```
========== LOAD TEST RESULTS ==========
Duration:           250s
Peak VUs:           50
Total requests:     4823
Requests/sec:       19.3
Jobs created:       2156
p95 Create:         342ms
p95 Retrieve:       45ms
p95 Search:         128ms
Failed rate:        0.12%
========================================
```
