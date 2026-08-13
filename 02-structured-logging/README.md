# Structured Logging & Observability

This Proof of Concept will demonstrate how structured, contextual, and centralized logs improve production troubleshooting.

> **Work in Progress:** Structured application logging, request correlation, centralized log search, and the diagnostic scenario are available. Final documentation will be added in M7.

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
{service_name="structured-logging-api"}
| json
| CorrelationId="<value>"
```

All logs for one payment:

```logql
{service_name="structured-logging-api"}
| json
| PaymentId="<value>"
```

Error-level logs:

```logql
{service_name="structured-logging-api"}
| json
| _l="Error"
```

`RenderedCompactJsonFormatter` writes a non-information log level in the JSON field `@l`. Loki's automatic JSON parser normalizes that field to the queryable name `_l`, which is why the error query filters on `_l="Error"`. These request, payment, and error queries were verified against the Loki configuration used by this stack.

## Diagnostic scenario

The simulated payment gateway fails deterministically when `Amount` is exactly `13.37`. Every other valid amount follows the unchanged successful flow. The failure is a controlled `PaymentGatewayException` with a concrete simulated-gateway reason, and the payment processor records that exception once while its structured operation scope is still active.

To reproduce and diagnose the failure:

1. Start the complete stack:

   ```bash
   docker compose up --build
   ```

2. Send a payment request with the diagnostic amount:

   ```bash
   curl -i -X POST http://localhost:8080/payments \
     -H "Content-Type: application/json" \
     -d '{"customerId":"11111111-1111-1111-1111-111111111111","amount":13.37,"currency":"EUR"}'
   ```

   The API returns `500 Internal Server Error` with a generic problem-details body. Internal exception details are not returned to the caller. The response still contains `X-Correlation-ID` so the caller can report a diagnostic identifier.

3. Copy the `X-Correlation-ID` response-header value.

4. Open [Grafana Explore](http://localhost:3000/explore) and select the provisioned `Loki` data source.

5. Find every event for the failed request:

   ```logql
   {service_name="structured-logging-api"}
   | json
   | CorrelationId="<correlation-id>"
   ```

6. Read `PaymentId`, `CustomerId`, and `Operation` from the payment events. The trail shows payment processing starting, the `SimulatedPaymentGateway` invocation starting, the gateway failure, and the HTTP request completing with status `500`.

7. Inspect the payment-processor error event. Its exception identifies the simulated gateway failure and the diagnostic amount. The same event retains `CorrelationId`, `PaymentId`, `CustomerId`, `Operation`, `Amount`, `Currency`, and `PaymentStatus` as structured properties.

8. Optionally narrow the search to the affected payment:

   ```logql
   {service_name="structured-logging-api"}
   | json
   | PaymentId="<payment-id>"
   ```

9. To inspect error-level events across requests, use the formatter-aware level query:

   ```logql
   {service_name="structured-logging-api"}
   | json
   | _l="Error"
   ```

The payment object is created in memory before the gateway call, which gives the diagnostic trail a `PaymentId`. The existing flow persists only after a successful gateway result, so the failed payment is not stored in PostgreSQL. No transaction or persistence redesign is introduced for this scenario.

With only plain-text, uncorrelated logs, an engineer would need to align timestamps and infer which interleaved gateway and HTTP messages belong together. Here, `CorrelationId` reconstructs the request, `PaymentId` narrows the affected operation, and the exception plus structured fields establish the customer, failure location, payment state, and concrete reason without relying on message-text searches.

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
