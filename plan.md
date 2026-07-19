# Plan: Implement Polly Resilience and Correlation ID Middleware

## Objectives
1. **Implement Correlation ID Middleware**:
   - Create a middleware in `JobFlow.Api` to capture or generate `X-Correlation-ID`.
   - Propagate this ID through the system (logs, headers).
2. **Implement Polly Resilience**:
   - Define standard resilience pipelines (retry/timeout) in `JobFlow.Infrastructure`.
   - Wrap external service calls (MongoDB, Elasticsearch, RabbitMQ) using these policies.

## Implementation Details
1. **Correlation ID**:
   - Create `src/JobFlow.Api/Middleware/CorrelationIdMiddleware.cs`.
   - Register it in `Program.cs`.
   - Update Serilog logging enrichment.
2. **Polly**:
   - Add `Polly.Extensions` if necessary (or use standard Polly v8).
   - Create a `ResiliencePipeline` builder in `DependencyInjection.cs`.
   - Update `JobService` and `RabbitMqJobPublisher` to use the injected pipeline.

## Verification
- API receives/generates correlation IDs and logs them.
- Service calls are wrapped in retry policies.
- Build verifies successfully.
