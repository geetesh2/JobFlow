# Plan: Enhance Observability with Distributed Tracing in Worker

## Objective
Enable full end-to-end distributed tracing across the JobFlow microservice, specifically connecting the API (where jobs are created) to the Worker (where jobs are processed).

## Proposed Changes

1.  **Enhance `JobCreatedMessage`**:
    *   Add a `TraceId` or `ParentId` field to the message contract to ensure the consumer can reconstruct the trace context.

2.  **Instrumentation in `RabbitMqJobPublisher`**:
    *   Inject the current `Activity.Current` context into RabbitMQ headers before publishing.

3.  **Instrumentation in `Worker`**:
    *   Extract the trace context from RabbitMQ headers in `Worker.HandleMessageAsync`.
    *   Start a new `Activity` using the extracted context to maintain the trace continuity.
    *   Add custom counters (metrics) to `Worker` to track processing throughput and latency.

4.  **Infrastructure Updates**:
    *   Configure OpenTelemetry instrumentation for RabbitMQ in `DependencyInjection.cs`.

## Implementation Strategy

1.  **Phase 1: Contract Update**: Modify `JobCreatedMessage` in `src/JobFlow.Contracts` to include optional tracing headers.
2.  **Phase 2: Publisher Injection**: Update `RabbitMqJobPublisher` to inject `Activity.Current` headers into RabbitMQ `BasicProperties`.
3.  **Phase 3: Consumer Extraction**: Update `Worker` to extract headers and start a new `Activity` upon receipt.
4.  **Phase 4: Verification**: Run existing integration tests to ensure message flow remains intact and verify trace continuity using a mock/test-based trace listener if possible.

## Risks/Considerations
- Backward compatibility: Existing messages in the queue (if any) won't have the new headers; the consumer must handle missing headers gracefully.
- Dependency overhead: Adding OTel instrumentation for RabbitMQ may increase dependencies.
