# Structured Logging & Observability

This Proof of Concept will demonstrate how structured, contextual, and centralized logs improve production troubleshooting.

> **Work in Progress:** Structured application logging, request correlation, and centralized log search are available. The diagnostic scenario and final documentation will be added in later milestones.

## Problem

Plain-text log messages are readable but difficult to query reliably when several payments and requests are processed at the same time. Useful diagnostic context needs to remain separate data instead of being embedded only in prose.

## Why structured logging exists

Structured logging emits an event with named properties. Serilog writes each event as JSON so tools can later filter by fields such as `PaymentId`, `CustomerId`, `Amount`, `Currency`, `PaymentStatus`, `Gateway`, `Operation`, and `SourceContext`.

Message templates keep those values structured. For example, `Processing payment {PaymentId}` preserves `PaymentId` as a searchable property, while string interpolation turns the identifier into part of an unstructured message.

Stable contextual data belongs to a logging scope, while data describing one event belongs in that event's message template. The payment flow attaches `PaymentId`, `CustomerId`, and `Operation` once; every log emitted while that scope is active automatically receives those structured properties. Event-specific values such as `Amount`, `Currency`, or `PaymentStatus` remain on the relevant event.

```csharp
using var scope = logger.BeginScope(new Dictionary<string, object>
{
    ["PaymentId"] = payment.Id,
    ["CustomerId"] = payment.CustomerId,
    ["Operation"] = "ProcessPayment"
});

logger.LogInformation(
    "Payment processing started with amount {Amount} {Currency}",
    payment.Amount,
    payment.Currency);
```

An unstructured event might contain only:

```text
Processing payment 9f...
```

The structured event conceptually contains:

```json
{
  "PaymentId": "9f...",
  "CustomerId": "11111111-1111-1111-1111-111111111111",
  "Amount": 100.00,
  "Currency": "EUR"
}
```

## Example scenario

The API processes a small payment flow through `POST /payments`. It validates the request, creates a pending payment, calls a deterministic simulated payment gateway, marks the payment as completed, and persists it in PostgreSQL.

## Architecture

TODO.

## How it works

TODO.

## Correlation and context

Structured information is attached at the narrowest useful level:

### Request context

`CorrelationId` identifies every log produced while one HTTP request is processed.

### Operation context

`PaymentId`, `CustomerId`, and `Operation` describe the payment-processing operation. They are added once when that operation begins and are inherited by its nested logs.

### Event-specific data

`Amount`, `Currency`, and `PaymentStatus` describe an individual event and remain properties of that event.

Separating these levels avoids repeating stable request and operation properties in every log statement while keeping all values structured and searchable.

Clients may send a correlation identifier in the `X-Correlation-ID` request header. The API preserves a valid value, generates a compact GUID when the header is missing or invalid, returns the identifier in the `X-Correlation-ID` response header, and includes it in every log for that request. Externally supplied values are length-limited because correlation is diagnostic metadata, not trusted business data.

In a distributed system, the correlation identifier would normally be propagated to downstream services through HTTP or message headers. The simulated payment gateway in this PoC runs in-process, so distributed propagation is intentionally not implemented.

## Centralized logging

Container console logs are useful during development, but they become difficult to search once events are spread across requests, containers, and restarts. The API therefore ships the same structured Serilog events directly to Loki, while retaining console output for immediate local inspection. The sink batches delivery asynchronously, so a temporary Loki outage does not make payment processing fail.

Loki stores and queries the logs. Grafana provides the Explore interface used to search them, and Docker Compose provisions Loki as the default Grafana data source automatically. Structured fields such as `CorrelationId`, `PaymentId`, `CustomerId`, `Operation`, and `PaymentStatus` remain in each JSON log line after centralization.

Only the low-cardinality `service_name` and `level` values are Loki labels. Request, payment, and customer identifiers are deliberately not labels because creating a stream for every unique identifier would increase Loki index cardinality. They remain searchable JSON fields instead.

In Grafana Explore, select the provisioned `Loki` data source and use these queries:

All Structured Logging API logs:

```logql
{service_name="structured-logging-api"}
```

All logs for one request:

```logql
{service_name="structured-logging-api"} | json | CorrelationId="replace-with-correlation-id"
```

All logs for one payment:

```logql
{service_name="structured-logging-api"} | json | PaymentId="replace-with-payment-id"
```

Error-level logs:

```logql
{service_name="structured-logging-api", level="error"}
```

## Failure scenarios

TODO.

## Trade-offs

TODO.

## When to use

TODO.

## When not to use

TODO.

## Running the example

Start the API, PostgreSQL, Loki, and Grafana from this directory:

```bash
docker compose up --build
```

Docker Compose waits for PostgreSQL to become healthy before starting the API. In non-production environments, the API applies pending EF Core migrations automatically. Production environments require migrations to be applied through a controlled deployment process. Loki stores logs in a local Docker volume, and Grafana starts with Loki already provisioned as its default data source.

Create a payment:

```bash
curl -i -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","amount":100.00,"currency":"EUR"}'
```

The response contains the payment identifier, customer identifier, amount, currency, `Completed` status, creation timestamp, and an `X-Correlation-ID` header.

Copy the correlation identifier, open [Grafana](http://localhost:3000), and sign in with the local credentials from `.env` or the defaults `admin` / `admin`. Open **Explore**, select the provisioned **Loki** data source, and run the correlation query from the centralized logging section with the copied value.

Inspect the API's JSON logs locally:

```bash
docker compose logs -f api
```

Each HTTP request produces one concise completion event with the request method, path, response status code, and elapsed time. Payment processing events include only useful operational context and do not log request bodies or sensitive configuration.
