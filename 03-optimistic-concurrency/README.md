# Optimistic Concurrency

This Proof of Concept will demonstrate how optimistic concurrency detects and prevents lost updates in a small inventory scenario.

> **Work in Progress**

## Problem

Two clients can read the same inventory quantity and then submit different updates. Without concurrency protection, the later update silently replaces the earlier one.

M2 intentionally implements that unsafe baseline. Concurrency tokens, conflict detection, and `409 Conflict` responses will be added in later milestones.

## Inventory Scenario

The application starts with this deterministic item:

- ID: `11111111-1111-1111-1111-111111111111`
- Name: `Demo Item`
- Quantity: `100`

The current API performs ordinary EF Core updates. If Client A writes quantity `90` and Client B later writes quantity `80`, the final quantity is `80` and no conflict is reported. This behavior is intentional for the current milestone.

## Lost Update Scenario

A lost update occurs when two clients read the same state, make independent changes, and the later write silently replaces the earlier write. A successful HTTP response and a successful database write only confirm that an operation completed; without concurrency protection, they do not prove that the operation was based on the latest state.

```text
Client A                Client B

GET Quantity = 100      GET Quantity = 100

PUT 90
                        PUT 80

Database:

Quantity = 80
```

Both clients operate on the stale quantity `100`, and both operations succeed. No conflict is detected, so Client B's second write changes the database to `80` and silently overwrites Client A's update to `90`. The final value does not preserve Client A's update. M4 will solve this problem; this milestone intentionally documents the unsafe behavior without introducing the solution.

## Running the Example

Start the API and PostgreSQL from this directory:

```bash
docker compose up --build
```

Docker Compose waits for PostgreSQL to become healthy before starting the API. In non-production environments, the API applies pending EF Core migrations and safely ensures that the demo item exists. Production environments do not apply migrations automatically and require a controlled migration step.

Default local ports are:

- API: `http://localhost:8083`
- PostgreSQL: `localhost:5432`

Copy `.env.example` to `.env` only when the default development values need to be overridden.

## Endpoints

Retrieve the demo item:

```http
GET /inventory/11111111-1111-1111-1111-111111111111
```

Update its quantity:

```http
PUT /inventory/11111111-1111-1111-1111-111111111111
Content-Type: application/json

{
  "quantity": 90
}
```

Both endpoints return `404 Not Found` when the item does not exist. A negative quantity is rejected with `400 Bad Request`.

## Current Limitation

Concurrency protection is intentionally not implemented yet. Requests and responses have no version field, EF Core uses normal tracked updates, and the API does not detect lost updates. This unsafe behavior is demonstrated by the deterministic M3 integration test and provides the baseline for M4.
