# Optimistic Concurrency

This Proof of Concept will demonstrate how optimistic concurrency detects and prevents lost updates in a small inventory scenario.

> **Work in Progress**

## Problem

Two clients can read the same inventory quantity and then submit different updates. Without concurrency protection, the later update silently replaces the earlier one.

M3 intentionally demonstrated that unsafe baseline. M4 added version-based conflict detection, and M5 exposes it through an explicit HTTP contract.

## Inventory Scenario

The application starts with this deterministic item:

- ID: `11111111-1111-1111-1111-111111111111`
- Name: `Demo Item`
- Quantity: `100`

The M3 baseline used ordinary EF Core updates. If Client A wrote quantity `90` and Client B later wrote quantity `80`, the final quantity was `80` and no conflict was reported.

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

Both clients operate on the stale quantity `100`, and both operations succeed. No conflict is detected, so Client B's second write changes the database to `80` and silently overwrites Client A's update to `90`. The final value does not preserve Client A's update. This was the intentional M3 behavior that motivates the protection added in M4.

## Optimistic Concurrency

`InventoryItem.Version` is an explicit numeric EF Core concurrency token. Each successful update advances it, and EF Core includes the version originally read by the client in the database update condition.

```text
Client A reads Version = 1
Client B reads Version = 1

Client A updates with Version = 1
        ↓
success
        ↓
Version becomes 2

Client B updates with Version = 1
        ↓
no matching row
        ↓
concurrency conflict
```

The database write itself detects that the row changed after the client read it. EF Core reports the zero-row update as `DbUpdateConcurrencyException`, so the stale write cannot silently replace the newer quantity.

Optimistic concurrency detects stale writes; it does not lock the inventory item while a client is working.

## Conflict Handling

The API includes the numeric `Version` in every inventory item representation. A client reads that version and sends the same value with its proposed update:

```text
GET resource
     ↓
receive Version = 1
     ↓
modify locally
     ↓
PUT with Version = 1
     ↓
database checks original version
     ↓
success OR conflict
```

The version property communicates the client's expectation, but including it in the request does not by itself prevent a race. Protection comes from the atomic database update, conceptually:

```sql
UPDATE inventory_items
SET quantity = ..., version = 2
WHERE id = ...
  AND version = 1;
```

If another update has already advanced the version, no row matches the condition. EF Core interprets the zero affected rows as a concurrency conflict, and the API returns `409 Conflict` without overwriting the newer state.

```text
Client A reads Quantity = 100, Version = 1
Client B reads Quantity = 100, Version = 1

Client A sends Quantity = 90, Version = 1
        ↓
200 OK: Quantity = 90, Version = 2

Client B sends Quantity = 80, Version = 1
        ↓
409 Conflict: current Quantity = 90, Version = 2
```

The conflict response contains a short message and the current resource representation so the client can make an explicit recovery decision:

```json
{
  "message": "The inventory item was modified by another client.",
  "current": {
    "id": "11111111-1111-1111-1111-111111111111",
    "name": "Demo Item",
    "quantity": 90,
    "version": 2
  }
}
```

### Detection

Optimistic concurrency detects: "The state I am trying to modify is no longer the state I originally read."

### Resolution

Optimistic concurrency does not determine whether the client should discard its changes, reload and try again, merge changes, ask the user, or execute another business operation. That is a business or application decision. The API does not automatically retry or merge a stale update.

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
  "quantity": 90,
  "version": 1
}
```

`GET` and successful `PUT` responses contain `id`, `name`, `quantity`, and `version`. Both endpoints return `404 Not Found` when the item does not exist. A negative quantity or invalid version is rejected with `400 Bad Request`. A valid update based on a stale version returns `409 Conflict` with the current resource state.

## Conflict Resolution Scope

The PoC detects and reports conflicts only. It intentionally leaves the resolution decision to the client and does not implement automatic retries, merging, or overwriting of newer data.

## Concurrent Clients Scenario

Two clients are concurrent when they operate on overlapping logical state. Their requests do not need to run during the same CPU instant: it is enough that both clients read the same version before either submits an update.

```text
Client A                  API / Database                  Client B
   |                            |                            |
   | GET                        |                            |
   |--------------------------->|                            |
   | Quantity=100, Version=1    |                            |
   |<---------------------------|                            |
   |                            |<---------------------------| GET
   |                            |--------------------------->|
   |                            | Quantity=100, Version=1    |
   |                            |                            |
   | PUT 90, Version=1          |                            |
   |--------------------------->|                            |
   |                            | update succeeds            |
   |                            | Version -> 2               |
   | Quantity=90, Version=2     |                            |
   |<---------------------------|                            |
   |                            |                            |
   |                            |<---------------------------| PUT 80, Version=1
   |                            | stale version              |
   |                            |--------------------------->|
   |                            | 409, current state = 90/2  |
   |                            |                            |
   |                            |<---------------------------| PUT 80, Version=2
   |                            | explicit new decision      |
   |                            |--------------------------->|
   |                            | success, Version -> 3      |
```

Here, **stale** means Client B's representation was valid when read but became outdated when Client A committed its change. A **conflict** means the database found that Client B's expected version no longer matched the current version, so it rejected the write atomically. **Resolution** starts only after that detection: the client or business workflow can discard the change, reload, merge, ask a user, or submit a deliberate new operation. No one strategy is correct for every domain, and the API never retries the stale operation automatically.

### Manual walkthrough

Start from a clean database so the values below begin at `Quantity = 100, Version = 1`:

```bash
docker compose down -v
docker compose up --build
```

In another terminal, read the item once as Client A:

```bash
curl -i http://localhost:8083/inventory/11111111-1111-1111-1111-111111111111
```

Read it independently as Client B before making any update:

```bash
curl -i http://localhost:8083/inventory/11111111-1111-1111-1111-111111111111
```

Both responses contain quantity `100` and version `1`. Client A now updates using the version it read:

```bash
curl -i -X PUT http://localhost:8083/inventory/11111111-1111-1111-1111-111111111111 \
  -H "Content-Type: application/json" \
  -d '{"quantity":90,"version":1}'
```

The response is `200 OK` with quantity `90` and version `2`. Client B still holds version `1`, so its update is stale:

```bash
curl -i -X PUT http://localhost:8083/inventory/11111111-1111-1111-1111-111111111111 \
  -H "Content-Type: application/json" \
  -d '{"quantity":80,"version":1}'
```

The response is `409 Conflict`, and its `current` property contains quantity `90` and version `2`. Confirm that Client A's value remains stored:

```bash
curl -i http://localhost:8083/inventory/11111111-1111-1111-1111-111111111111
```

After considering the conflict, Client B can make a deliberate new request using the current version:

```bash
curl -i -X PUT http://localhost:8083/inventory/11111111-1111-1111-1111-111111111111 \
  -H "Content-Type: application/json" \
  -d '{"quantity":80,"version":2}'
```

This final request succeeds with quantity `80` and version `3`. It is a new client decision based on fresh state, not an automatic retry of the stale request. If the database volume was not reset, use the versions returned by the two initial `GET` requests instead of assuming version `1`.
