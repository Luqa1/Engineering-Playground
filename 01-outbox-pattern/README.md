# Outbox Pattern

A Proof of Concept demonstrating the Outbox Pattern.

**Work in Progress**

## Problem

## Why this pattern exists

## Architecture

## How it works

## Trade-offs

## When to use

## When not to use

## Production retry scheduling

The current Worker polls at a fixed interval. During each polling cycle, any unprocessed Outbox message below `MaxRetriesCount` is eligible for publication. A message that reaches the limit remains unprocessed so it can be investigated and reprocessed manually.

Production systems commonly delay subsequent attempts instead of selecting every failed message on every polling cycle. One extension is to add a nullable `NextAttemptAtUtc` column to `OutboxMessage` and select a message only when that value is `NULL` or less than or equal to the current UTC time. After a failure, the Worker would calculate the next attempt from configurable delays—for example, 1, 2, 5, 8, and 13 minutes—or another configurable backoff policy. Automatic processing would still stop at `MaxRetriesCount`, requiring manual intervention.

This scheduling mechanism is intentionally outside this PoC. The example focuses on the Outbox Pattern itself; retry scheduling adds configuration, persistence, and time-based logic that would distract from that educational objective. More advanced implementations may also add exponential backoff, jitter, dead-letter handling, and operational tooling for manual replay.

## Running the example

## Failure scenarios

Start the services with `docker compose up --build`. Inspect the relevant records with:

```powershell
docker compose exec postgres psql -U postgres -d outbox -c 'SELECT "Id", "ProcessedAtUtc", "RetryCount", "Error" FROM "OutboxMessages" ORDER BY "CreatedAtUtc" DESC;'
docker compose exec postgres psql -U postgres -d outbox -c 'SELECT "Id", "CustomerId", "TotalAmount", "CreatedAtUtc" FROM "Orders" ORDER BY "CreatedAtUtc" DESC;'
```

### Broker unavailable

1. Stop RabbitMQ with `docker compose stop rabbitmq`.
2. Create an Order:

   ```powershell
   Invoke-RestMethod -Method Post -Uri http://localhost:8080/orders -ContentType application/json -Body '{"customerId":"11111111-1111-1111-1111-111111111111","totalAmount":42.00}'
   ```

3. Run both SQL checks. The Order and Outbox message remain stored, `ProcessedAtUtc` is null, and each failed polling attempt increments `RetryCount` once and replaces `Error` with the latest failure.
4. Observe the Worker with `docker compose logs -f worker`. It continues polling while the message remains below `MaxRetriesCount`.

### Recovery

Before the message reaches `MaxRetriesCount`, start RabbitMQ with `docker compose start rabbitmq`. Observe the successful publication log, then run the Outbox SQL check. `ProcessedAtUtc` is populated, `Error` is cleared, and `RetryCount` retains the number of earlier failures. Later polling cycles do not select the message again.

### Worker restart

With RabbitMQ stopped and an Outbox message still below `MaxRetriesCount`, stop the Worker with `docker compose stop worker`. Run the Outbox SQL check to confirm the pending state remains in PostgreSQL. Start a new Worker instance with `docker compose start worker`; it selects the existing pending message again. If RabbitMQ is available, processing succeeds without relying on state held by the previous Worker process.

### Retry limit

Keep RabbitMQ unavailable and follow `docker compose logs -f worker` until `RetryCount` reaches `MaxRetriesCount`. The Worker logs that manual investigation and reprocessing are required. Run the Outbox SQL check to verify the message remains stored with a null `ProcessedAtUtc` and its final `Error`. Further polling cycles do not select it automatically; the Worker does not delete or reset it.
