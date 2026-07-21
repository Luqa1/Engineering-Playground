# Outbox Pattern

## Overview

The Outbox Pattern reliably publishes integration events by storing them in the same database transaction as the business change. A separate worker later reads those durable records and publishes them to a message broker.

## The Problem

Creating business data and publishing an event are two writes to different systems. A database transaction cannot normally make both PostgreSQL and RabbitMQ commit atomically.

Consider an API that first saves an Order and then publishes `order.created`:

- The database commit succeeds, but the API crashes before publishing. The Order exists, but downstream services never learn about it.
- RabbitMQ is unavailable after the Order is committed. Returning an error does not undo the committed Order, and retrying the request may create another Order.
- Publishing succeeds, but the database operation fails or is rolled back. Consumers receive an event for an Order that does not exist.
- A network timeout leaves the publisher uncertain whether RabbitMQ accepted the event. Retrying can produce a duplicate.

Changing the order of the two writes only changes which inconsistent state can occur. It does not make the operation reliable.

## Why the Outbox Pattern Exists

The pattern replaces the broker write in the request path with a database write. The application saves the business entity and a serialized integration event in one local transaction. Either both records commit or neither does.

Publishing then happens asynchronously. PostgreSQL is immediately consistent about the Order and its Outbox record, while RabbitMQ and its consumers become consistent later. This is **eventual consistency**: a committed event may not be visible to consumers immediately, but the durable Outbox record allows publication to continue after transient failures.

## Example Scenario

This PoC creates an Order and announces it with the stable event name `order.created`:

1. A client sends `POST /orders` with a customer ID and total amount.
2. The API creates an `Order` and an `OutboxMessage` containing the event payload.
3. Entity Framework Core saves both records to PostgreSQL in one transaction.
4. The Worker polls for unprocessed Outbox messages.
5. The Worker publishes the event to RabbitMQ and records `ProcessedAtUtc`.

The API response does not wait for RabbitMQ. Broker availability is decoupled from accepting the business operation.

## Architecture

See the [architecture diagram](diagrams/architecture.mmd).

- **API** validates the request and creates the Order and Outbox message.
- **PostgreSQL** provides the transaction boundary and durable storage.
- **Orders** stores the committed business state.
- **OutboxMessages** stores event type, JSON payload, processing state, retry count, and the latest error.
- **Worker** polls pending messages, publishes them, and updates their state.
- **RabbitMQ** exposes the durable topic exchange `engineering-playground.events`; the routing key is the stable event name `order.created`.

## Sequence

See the [sequence diagram](diagrams/sequence.mmd).

The request transaction ends after PostgreSQL saves the Order and Outbox message. In a separate asynchronous flow, the Worker polls PostgreSQL, publishes the stored payload to RabbitMQ, and then updates `ProcessedAtUtc`. These flows do not share a distributed transaction.

## Implementation

### Order creation

`POST /orders` accepts a `CustomerId` and positive `TotalAmount`. The API creates an Order and an `OrderCreatedIntegrationEvent` with the same identifiers, amount, and creation time.

### Outbox message creation

The event is serialized as JSON into an `OutboxMessage`. Its type is stored as `order.created`, independently of the C# class or namespace, so refactoring .NET types does not silently change the integration contract.

### Single transaction

The API adds the Order and Outbox message to the same `OutboxDbContext` and calls `SaveChangesAsync` once. EF Core wraps that save in a PostgreSQL transaction. A failed commit persists neither record; a successful commit persists both.

### Background Worker

The Worker polls PostgreSQL every five seconds by default. It selects unprocessed messages whose `RetryCount` is below `MaxRetriesCount`, oldest first.

### Publishing

Each selected message is published as persistent JSON to the RabbitMQ topic exchange using the stored event type as its routing key.

### Marking messages as processed

After a successful publish, the Worker sets `ProcessedAtUtc`, clears the latest error, and saves the change. Processed messages are excluded from later polling cycles.

Publishing and marking a message as processed are separate operations. A Worker crash between them can cause the same event to be published again. The Outbox Pattern therefore provides at-least-once publication in this failure window, and consumers should be idempotent.

## Failure Scenarios

### RabbitMQ unavailable

Order creation still succeeds because it depends only on PostgreSQL. The Worker keeps the Outbox message unprocessed, records the publication error, and increments `RetryCount` once per failed attempt.

### Worker restart

Pending messages live in PostgreSQL rather than Worker memory. A restarted Worker polls and resumes processing messages that remain below the retry limit.

### Retry limit reached

When `RetryCount` reaches `MaxRetriesCount`, the message remains unprocessed but is no longer selected automatically. Its latest error is retained for investigation. An operator must correct the cause and explicitly prepare the record for reprocessing according to the application's operational procedure.

### Successful recovery

If RabbitMQ recovers before the retry limit, a later polling cycle publishes the event. The Worker sets `ProcessedAtUtc`, clears `Error`, and preserves the retry count as failure history.

## Retry Strategy

`MaxRetriesCount` defaults to `5` and is configurable through `OUTBOX_MAX_RETRIES_COUNT` in Docker Compose. Automatic attempts stop at the limit to prevent a permanently failing message from consuming every polling cycle indefinitely. Manual intervention is then required to inspect the error, correct the underlying problem, and decide whether replay is safe.

This PoC intentionally retries eligible messages on the fixed polling interval. Delayed retry scheduling is omitted to keep the example focused on atomic persistence and asynchronous publication.

Production systems may add a persisted `NextAttemptAtUtc`, exponential or Fibonacci backoff, jitter, and dead-letter queues. Those mechanisms require additional scheduling, storage, and operational decisions and are deliberate non-goals here.

## Running the Example

### Prerequisites

- .NET 10 SDK
- Docker with Docker Compose
- PowerShell (for the example commands)

Run all commands from `01-outbox-pattern`.

### Configure and start dependencies

```powershell
Copy-Item .env.example .env
docker compose up -d postgres rabbitmq
```

### Apply the database migration

```powershell
dotnet ef database update --project src/EngineeringPlayground.Outbox.Infrastructure --startup-project src/EngineeringPlayground.Outbox.Api
```

The default local connection string targets PostgreSQL at `localhost:5432`.

### Start the application

```powershell
docker compose up --build -d api worker
docker compose logs -f api worker
```

### Create an Order

```powershell
$body = @{
    customerId = "11111111-1111-1111-1111-111111111111"
    totalAmount = 42.00
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri http://localhost:8080/orders -ContentType application/json -Body $body
```

### Verify RabbitMQ

Open [http://localhost:15672](http://localhost:15672), sign in with `guest` / `guest`, and inspect the `engineering-playground.events` exchange. The Worker logs also record successful publication of the Outbox message.

The PoC declares an exchange but no consumer queue. To observe routed messages, bind a queue to the exchange with the `order.created` routing key before creating the Order.

### Verify PostgreSQL

```powershell
docker compose exec postgres psql -U postgres -d outbox -c 'SELECT "Id", "CustomerId", "TotalAmount", "CreatedAtUtc" FROM "Orders" ORDER BY "CreatedAtUtc" DESC;'
docker compose exec postgres psql -U postgres -d outbox -c 'SELECT "Id", "Type", "ProcessedAtUtc", "RetryCount", "Error" FROM "OutboxMessages" ORDER BY "CreatedAtUtc" DESC;'
```

## Verifying the Pattern

1. Start PostgreSQL and RabbitMQ, apply the migration, and start the API and Worker as described above.
2. Stop the broker: `docker compose stop rabbitmq`.
3. Create an Order with `POST /orders`. The API should still return `201 Created`.
4. Query both tables. The Order and Outbox message should exist; `ProcessedAtUtc` should be null.
5. Inspect `docker compose logs worker`. Failed attempts should increment `RetryCount` and update `Error`.
6. Before the fifth failed attempt, restart the broker: `docker compose start rabbitmq`.
7. Query `OutboxMessages` again after the next polling cycle. `ProcessedAtUtc` should be populated and `Error` should be null.
8. Restart the Worker with `docker compose restart worker` and query again. The processed message should remain unchanged and should not be selected again.

If the message reaches five failed attempts, automatic processing stops as designed. Create another Order to repeat the recovery walkthrough, or perform an intentional manual replay after investigating the failed record.

## Trade-offs

### Advantages

- Removes the unreliable database-and-broker dual write from the request path.
- Atomically records business state and the intent to publish.
- Survives broker outages and Worker restarts.
- Keeps the API independent of broker latency and short outages.

### Disadvantages

- Delivers events eventually rather than immediately.
- Adds an Outbox table, polling Worker, and cleanup requirements.
- Can publish duplicates when publishing succeeds but recording success fails.
- Preserves event payloads that must remain compatible, secure, and manageable over time.

### Operational considerations

Teams must monitor pending age, retry counts, and terminal failures; define safe replay and retention procedures; make consumers idempotent; and plan for throughput and concurrent workers. The simple Worker in this PoC is intentionally not a complete production operating model.

## When to Use

Use the Outbox Pattern when a committed database change must reliably trigger asynchronous work or notify another system. Examples include creating an Order and starting fulfillment, recording a payment and updating accounting, or changing an account and refreshing search or analytics projections.

It is especially useful when temporary broker unavailability must not reject or lose a valid business transaction.

## When NOT to Use

Do not use an Outbox merely because an application has a database. If all required changes occur in one database and no external message must be published, a normal synchronous database transaction is simpler and sufficient.

It may also be unnecessary when losing the notification is acceptable, when the operation can safely remain fully synchronous, or when the added eventual consistency and operational workload outweigh the reliability requirement.

## Possible Extensions

- Delayed retry scheduling
- Dead-letter queue
- Idempotent consumers
- OpenTelemetry
- Distributed locking for multiple workers
- Batching

## References

- [Transactional Outbox pattern — microservices.io](https://microservices.io/patterns/data/transactional-outbox.html)
- [Transactional outbox pattern — AWS Prescriptive Guidance](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html)
- [Integration event-based microservice communications — Microsoft](https://learn.microsoft.com/dotnet/architecture/microservices/multi-container-microservice-net-applications/integration-event-based-microservice-communications)
