# Structured Logging & Observability

This Proof of Concept will demonstrate how structured, contextual, and centralized logs improve production troubleshooting.

> **Work in Progress:** The running application is available. Structured and centralized logging will be added in later milestones.

## Problem

TODO.

## Why structured logging exists

TODO.

## Example scenario

The API processes a small payment flow through `POST /payments`. It validates the request, creates a pending payment, calls a deterministic simulated payment gateway, marks the payment as completed, and persists it in PostgreSQL.

## Architecture

TODO.

## How it works

TODO.

## Correlation and context

TODO.

## Centralized logging

TODO.

## Failure scenarios

TODO.

## Trade-offs

TODO.

## When to use

TODO.

## When not to use

TODO.

## Running the example

Start the API and PostgreSQL from this directory:

```bash
docker compose up --build
```

Docker Compose waits for PostgreSQL to become healthy before starting the API. In non-production environments, the API applies pending EF Core migrations automatically. Production environments require migrations to be applied through a controlled deployment process.

Create a payment:

```bash
curl -X POST http://localhost:8080/payments \
  -H "Content-Type: application/json" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","amount":100.00,"currency":"EUR"}'
```

The response contains the payment identifier, customer identifier, amount, currency, `Completed` status, and creation timestamp.
