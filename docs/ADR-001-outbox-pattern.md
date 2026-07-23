# ADR-001: Outbox Pattern over Direct Message Publishing

**Status:** Accepted  
**Date:** 2026-07-20  
**Context:** Job creation needs to persist a job to PostgreSQL AND publish a message to RabbitMQ. These are two separate systems that cannot participate in the same transaction.

---

## Problem

When a client creates a job, two things must happen:

1. The job is saved to PostgreSQL (source of truth)
2. A `JobCreatedMessage` is published to RabbitMQ so the Worker can process it

The naive approach is to save to the database, then publish to RabbitMQ:

```
BEGIN TRANSACTION
  INSERT INTO Jobs (...)
COMMIT

Publish to RabbitMQ   ← This can fail after the commit
```

**What can go wrong:**

| Failure Scenario | Result |
|---|---|
| DB save succeeds, RabbitMQ is down | Job exists in DB but Worker never processes it. The job is stuck in `Pending` forever. |
| DB save succeeds, app crashes before publish | Same as above — message is lost. |
| RabbitMQ publish succeeds, DB save fails | Worker processes a job that doesn't exist in the database. |

These are not edge cases. RabbitMQ restarts, network blips, and process crashes are normal in distributed systems.

## Decision

Use the **Transactional Outbox Pattern**: write the message to an `OutboxMessages` table in the **same database transaction** as the job. A background processor reads unprocessed outbox messages and publishes them to RabbitMQ.

```
BEGIN TRANSACTION
  INSERT INTO Jobs (...)
  INSERT INTO OutboxMessages (Type='JobCreatedMessage', Payload='...')
COMMIT

-- Background (OutboxProcessor, every 5 seconds):
BEGIN TRANSACTION
  SELECT * FROM OutboxMessages
    WHERE ProcessedAtUtc IS NULL AND RetryCount < 5
    FOR UPDATE SKIP LOCKED
  Publish each message to RabbitMQ
  UPDATE OutboxMessages SET ProcessedAtUtc = NOW()
COMMIT
```

## Why This Works

1. **Atomicity** — Job and outbox message are in the same PostgreSQL transaction. Either both are saved or neither is.
2. **Guaranteed delivery** — If RabbitMQ is down, messages accumulate in the outbox table. When RabbitMQ recovers, the processor catches up.
3. **At-least-once semantics** — Messages may be published more than once (if the processor crashes between publish and marking as processed). The Worker must be idempotent.
4. **No distributed transactions** — No 2PC, no XA, no saga needed for the publish step. Just a single PostgreSQL transaction.

## Concurrency Protection

Multiple API instances can run the `OutboxProcessor` simultaneously. Without protection, they would pick up the same messages and publish duplicates.

We use PostgreSQL's `FOR UPDATE SKIP LOCKED` within an explicit transaction:

- `FOR UPDATE` — Locks the selected rows for the duration of the transaction
- `SKIP LOCKED` — If another instance already locked a row, skip it instead of waiting

This guarantees each message is processed by exactly one instance per polling cycle.

## Alternatives Considered

### 1. Direct Publish (no outbox)

```
Save to DB → Publish to RabbitMQ
```

**Rejected.** No atomicity between DB and broker. Message loss on broker failures or app crashes.

### 2. Publish First, Then Save

```
Publish to RabbitMQ → Save to DB
```

**Rejected.** If the DB save fails, the Worker processes a ghost job. Worse failure mode than option 1.

### 3. RabbitMQ Publisher Confirms + Retry

```
Save to DB → Publish with confirms → Retry on failure
```

**Rejected.** Adds complexity without solving the crash-between-save-and-publish case. Still loses messages if the process dies at the wrong moment.

### 4. Change Data Capture (CDC)

Use Debezium or similar to tail the PostgreSQL WAL and emit events to RabbitMQ automatically.

**Rejected for now.** Adds significant infrastructure complexity (Kafka Connect, Debezium, schema registry). The outbox pattern achieves the same guarantee with zero additional infrastructure. CDC would be the right choice at much larger scale.

### 5. Saga with Compensation

Roll back the DB save if RabbitMQ publish fails.

**Rejected.** The job is already visible to the user after the DB save. Rolling it back creates a confusing UX ("I just created a job and now it's gone"). The outbox pattern avoids this entirely.

## Consequences

**Positive:**
- Zero message loss under any single-point-of-failure scenario
- No additional infrastructure beyond PostgreSQL (which we already have)
- Simple to understand, debug, and monitor (just query the `OutboxMessages` table)
- Concurrency-safe across multiple API instances

**Negative:**
- Adds 0-5 second latency between job creation and Worker processing (polling interval)
- At-least-once delivery means the Worker must handle duplicate messages (idempotent processing)
- The `OutboxMessages` table grows and needs periodic cleanup of processed messages

## Related

- `src/JobFlow.Application/Commands/CreateJob/CreateJobCommandHandler.cs` — Writes job + outbox message atomically
- `src/JobFlow.Infrastructure/Services/OutboxProcessor.cs` — Background processor with `FOR UPDATE SKIP LOCKED`
- `src/JobFlow.Domain/Entities/OutboxMessage.cs` — Outbox message entity
- `src/JobFlow.Infrastructure/Migrations/20260720053603_AddOutboxTable.cs` — Migration creating the table with filtered index
