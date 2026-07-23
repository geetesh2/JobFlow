import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const jobCreationDuration = new Trend('job_creation_duration', true);
const jobRetrievalDuration = new Trend('job_retrieval_duration', true);
const jobSearchDuration = new Trend('job_search_duration', true);
const jobsCreated = new Counter('jobs_created');
const failedRequests = new Rate('failed_requests');

// Configuration — override with environment variables:
//   k6 run --env BASE_URL=http://localhost:5001 --env TOKEN=eyJ... load-test.js
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5001';
const TOKEN = __ENV.TOKEN || '';

const headers = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${TOKEN}`,
};

// Load test scenarios
export const options = {
    scenarios: {
        // Scenario 1: Ramp up job creation load
        create_jobs: {
            executor: 'ramping-vus',
            startVUs: 1,
            stages: [
                { duration: '30s', target: 10 },   // Ramp up to 10 users
                { duration: '1m', target: 10 },     // Hold at 10
                { duration: '30s', target: 25 },    // Ramp to 25
                { duration: '1m', target: 25 },     // Hold at 25
                { duration: '30s', target: 0 },     // Ramp down
            ],
            exec: 'createJobs',
        },
        // Scenario 2: Constant read load (search + get by ID)
        read_jobs: {
            executor: 'constant-vus',
            vus: 5,
            duration: '3m',
            exec: 'readJobs',
            startTime: '10s', // Start after some jobs exist
        },
        // Scenario 3: Spike test
        spike: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10s', target: 50 },    // Sudden spike
                { duration: '20s', target: 50 },    // Hold spike
                { duration: '10s', target: 0 },     // Drop
            ],
            exec: 'createJobs',
            startTime: '3m30s', // After main test completes
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<2000'],           // 95th percentile < 2s
        job_creation_duration: ['p(95)<1500'],       // Job creation < 1.5s at p95
        job_retrieval_duration: ['p(95)<500'],       // Job retrieval < 500ms at p95
        job_search_duration: ['p(95)<1000'],         // Search < 1s at p95
        failed_requests: ['rate<0.05'],              // < 5% failure rate
    },
};

// Shared array to store created job IDs for read operations
const createdJobIds = [];

const JOB_TYPES = ['DataProcessing', 'ImageResize', 'PdfReport', 'Email', 'DogApi'];
const PRIORITIES = ['Low', 'Normal', 'High', 'Critical'];

// Scenario: Create jobs with varying payloads
export function createJobs() {
    const jobType = JOB_TYPES[Math.floor(Math.random() * JOB_TYPES.length)];
    const priority = PRIORITIES[Math.floor(Math.random() * PRIORITIES.length)];

    const payload = buildPayload(jobType);

    const body = JSON.stringify({
        name: jobType,
        priority: priority,
        maxRetries: Math.floor(Math.random() * 5) + 1,
        payload: payload,
        tags: ['load-test', `vu-${__VU}`],
        source: 'k6-load-test',
    });

    const res = http.post(`${BASE_URL}/api/v1/jobs`, body, {
        headers: headers,
        tags: { name: 'POST /api/v1/jobs' },
    });

    const success = check(res, {
        'create: status is 201': (r) => r.status === 201,
        'create: has id in body': (r) => {
            try { return JSON.parse(r.body).id !== undefined; } catch { return false; }
        },
        'create: has Location header': (r) => r.headers['Location'] !== undefined,
    });

    if (success) {
        jobCreationDuration.add(res.timings.duration);
        jobsCreated.add(1);
        try {
            const id = JSON.parse(res.body).id;
            if (createdJobIds.length < 1000) {
                createdJobIds.push(id);
            }
        } catch (e) { /* ignore */ }
    } else {
        failedRequests.add(1);
    }

    sleep(Math.random() * 0.5 + 0.1);
}

// Scenario: Read jobs (search + get by ID)
export function readJobs() {
    group('search', () => {
        const page = Math.floor(Math.random() * 3) + 1;
        const pageSize = [5, 10, 20][Math.floor(Math.random() * 3)];
        const status = ['', 'Pending', 'Completed', 'Failed'][Math.floor(Math.random() * 4)];

        let url = `${BASE_URL}/api/v1/jobs?page=${page}&pageSize=${pageSize}`;
        if (status) url += `&status=${status}`;

        const res = http.get(url, {
            headers: headers,
            tags: { name: 'GET /api/v1/jobs (search)' },
        });

        const success = check(res, {
            'search: status is 200': (r) => r.status === 200,
            'search: has jobs array': (r) => {
                try { return JSON.parse(r.body).jobs !== undefined; } catch { return false; }
            },
        });

        if (success) {
            jobSearchDuration.add(res.timings.duration);
        } else {
            failedRequests.add(1);
        }
    });

    group('get by id', () => {
        if (createdJobIds.length === 0) {
            sleep(1);
            return;
        }

        const id = createdJobIds[Math.floor(Math.random() * createdJobIds.length)];
        const res = http.get(`${BASE_URL}/api/v1/jobs/${id}`, {
            headers: headers,
            tags: { name: 'GET /api/v1/jobs/{id}' },
        });

        const success = check(res, {
            'get: status is 200 or 404': (r) => r.status === 200 || r.status === 404,
        });

        if (success && res.status === 200) {
            jobRetrievalDuration.add(res.timings.duration);
        } else if (!success) {
            failedRequests.add(1);
        }
    });

    sleep(Math.random() * 0.5 + 0.2);
}

function buildPayload(jobType) {
    switch (jobType) {
        case 'ImageResize':
            return {
                url: 'https://picsum.photos/1920/1080',
                width: [200, 400, 800][Math.floor(Math.random() * 3)],
                height: [150, 300, 600][Math.floor(Math.random() * 3)],
                format: ['png', 'jpeg'][Math.floor(Math.random() * 2)],
            };
        case 'PdfReport':
            return {
                title: `Load Test Report - VU ${__VU}`,
                maxRows: [10, 25, 50][Math.floor(Math.random() * 3)],
            };
        case 'DataProcessing':
            return { inputFile: `data_${__VU}_${__ITER}.csv`, outputFormat: 'parquet' };
        case 'Email':
            return { to: `user${__VU}@test.com`, subject: `Test ${__ITER}` };
        default:
            return null;
    }
}

// Summary output
export function handleSummary(data) {
    const summary = {
        timestamp: new Date().toISOString(),
        duration: data.state.testRunDurationMs,
        vus_max: data.metrics.vus_max ? data.metrics.vus_max.values.max : 0,
        requests_total: data.metrics.http_reqs ? data.metrics.http_reqs.values.count : 0,
        requests_per_second: data.metrics.http_reqs ? data.metrics.http_reqs.values.rate : 0,
        jobs_created: data.metrics.jobs_created ? data.metrics.jobs_created.values.count : 0,
        p95_create: data.metrics.job_creation_duration ? data.metrics.job_creation_duration.values['p(95)'] : null,
        p95_retrieve: data.metrics.job_retrieval_duration ? data.metrics.job_retrieval_duration.values['p(95)'] : null,
        p95_search: data.metrics.job_search_duration ? data.metrics.job_search_duration.values['p(95)'] : null,
        failed_rate: data.metrics.failed_requests ? data.metrics.failed_requests.values.rate : 0,
    };

    console.log('\n========== LOAD TEST RESULTS ==========');
    console.log(`Duration:           ${(summary.duration / 1000).toFixed(0)}s`);
    console.log(`Peak VUs:           ${summary.vus_max}`);
    console.log(`Total requests:     ${summary.requests_total}`);
    console.log(`Requests/sec:       ${summary.requests_per_second.toFixed(1)}`);
    console.log(`Jobs created:       ${summary.jobs_created}`);
    console.log(`p95 Create:         ${summary.p95_create ? summary.p95_create.toFixed(0) + 'ms' : 'N/A'}`);
    console.log(`p95 Retrieve:       ${summary.p95_retrieve ? summary.p95_retrieve.toFixed(0) + 'ms' : 'N/A'}`);
    console.log(`p95 Search:         ${summary.p95_search ? summary.p95_search.toFixed(0) + 'ms' : 'N/A'}`);
    console.log(`Failed rate:        ${(summary.failed_rate * 100).toFixed(2)}%`);
    console.log('========================================\n');

    return {
        'tests/load/results.json': JSON.stringify(summary, null, 2),
    };
}
