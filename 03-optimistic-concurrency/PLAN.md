# Optimistic Concurrency

## Goal

Demonstrate how concurrent updates can silently overwrite each other and how optimistic concurrency prevents lost updates without locking records for the entire business operation.

The PoC should teach the engineering problem first and the EF Core implementation second.

The focus is:

- concurrent modification;
- lost updates;
- version-based concurrency;
- conflict detection;
- explicit conflict handling.

## Business Scenario

Use a small inventory scenario.

The main entity is `InventoryItem`. It should conceptually contain:

- `Id`;
- `Name`;
- `Quantity`;
- concurrency version.

The exact representation of the concurrency version should be decided during implementation based on PostgreSQL and EF Core capabilities.

Do not build a complete inventory system. The scenario exists only to make concurrent modification easy to understand.

## M1 – Repository Skeleton

### Goal

Prepare the PoC structure and implementation plan.

### Expected outcome

- directory structure;
- initial README;
- PLAN;
- configuration placeholders.

## M2 – Running Application

**Status: Completed**

### Goal

Create the smallest working .NET 10 application needed for the concurrency scenario.

### Expected outcome

- .NET solution;
- ASP.NET Core API using Controllers;
- small `InventoryItem` domain model;
- PostgreSQL;
- EF Core;
- Docker Compose;
- initial migration;
- one-command startup according to `AGENTS.md`.

Provide only the API operations required to demonstrate the concurrency problem.

Avoid generic CRUD functionality.

## M3 – Lost Update Scenario

**Status: Completed**

### Goal

Demonstrate the problem before implementing the solution.

Create a deterministic scenario where two clients:

1. read the same `InventoryItem` state;
2. independently modify the quantity;
3. save their changes;
4. one update silently overwrites the other.

The implementation at this milestone should intentionally not use optimistic concurrency protection.

### Expected outcome

- reproducible lost-update scenario;
- tests demonstrating the problem;
- concise documentation explaining why the final database state is incorrect.

The unsafe behavior is intentional and temporary.

Do not implement the solution during this milestone.

## M4 – Optimistic Concurrency

**Status: Completed**

### Goal

Prevent silent lost updates using optimistic concurrency.

Introduce a concurrency version for `InventoryItem`.

Use the simplest mechanism appropriate for:

- .NET 10;
- EF Core;
- PostgreSQL.

The implementation should ensure that an update succeeds only when the entity version still matches the version originally read by the client.

When another client has already modified the record, the second update must be detected as a concurrency conflict rather than silently overwriting data.

### Expected outcome

- concurrency token;
- EF Core concurrency configuration;
- conflict detection;
- handling of `DbUpdateConcurrencyException` or the appropriate EF Core mechanism;
- tests proving that silent lost updates no longer occur.

Do not introduce pessimistic locking.

## M5 – Conflict API Behavior

**Status: Completed**

### Goal

Expose concurrency conflicts through a clear HTTP contract.

When the client attempts to update a stale version:

- do not silently overwrite current state;
- return an appropriate `409 Conflict`;
- provide enough information for the client to understand that its representation is stale.

The API should expose the concurrency version explicitly through the existing request/response model or another simple HTTP mechanism.

Keep the contract easy to understand.

Do not implement automatic conflict resolution.

Do not automatically overwrite newer data.

Do not automatically retry a stale business operation.

### Expected outcome

- explicit version contract;
- `409 Conflict`;
- useful conflict response;
- tests for stale updates.

## M6 – Concurrent Requests Scenario

### Goal

Demonstrate the complete behavior using two competing clients.

Create a deterministic walkthrough:

```text
Client A reads version 1
Client B reads version 1

Client A updates successfully
        ↓
version becomes 2

Client B attempts update using version 1
        ↓
409 Conflict
```

Verify that:

- Client A succeeds;
- Client B cannot silently overwrite Client A;
- the database contains the correct current state;
- Client B can retrieve the latest representation and decide what to do.

The PoC should clearly distinguish:

- detecting a conflict;
- resolving a conflict.

Optimistic concurrency detects the conflict.

The business/client layer decides how to resolve it.

Do not implement automatic merging.

## M7 – Documentation

### Goal

Turn the completed PoC into a concise engineering knowledge-base example.

### Expected outcome

- final README;
- architecture diagram;
- sequence diagram;
- lost-update explanation;
- concurrent-client walkthrough;
- HTTP examples;
- trade-offs;
- when to use;
- when not to use;
- production considerations.

The README should answer:

- What problem does optimistic concurrency solve?
- What is a lost update?
- Why does optimistic concurrency exist?
- How does version checking detect concurrent modification?
- What happens when a conflict occurs?
- Who should resolve the conflict?
- What are the trade-offs?
- When should optimistic concurrency be avoided?

## Final Review

Perform a focused engineering review after M7.

Review:

- simplicity;
- correctness;
- concurrency semantics;
- API behavior;
- EF Core configuration;
- tests;
- educational value;
- documentation accuracy.

Do not add functionality during Final Review.

## Design Constraints

### Technology

Use:

- .NET 10;
- ASP.NET Core Controllers;
- Entity Framework Core;
- PostgreSQL;
- Docker Compose.

Follow all general conventions from `AGENTS.md`.

### Architecture

Keep the architecture intentionally small.

Do not introduce:

- CQRS;
- MediatR;
- repositories unless genuinely required;
- event-driven architecture;
- messaging;
- distributed locks;
- Redis;
- background workers;
- additional architectural layers.

This PoC demonstrates optimistic concurrency, not application architecture.

### Concurrency

Do not use:

- distributed locking;
- database advisory locks;
- explicit row locking;
- `SELECT FOR UPDATE`;
- application-level mutexes.

Those solve different concurrency problems and would obscure the concept being demonstrated.

Use optimistic conflict detection.

### Conflict Resolution

The PoC should detect conflicts but should not attempt sophisticated automatic resolution.

Do not:

- automatically overwrite the newer value;
- automatically merge concurrent changes;
- blindly retry stale commands.

A concurrency conflict represents a business decision point.

### Testing

Concurrency tests must be deterministic.

Do not depend on:

- arbitrary `Task.Delay`;
- random timing;
- probabilistic race conditions.

Coordinate concurrent operations explicitly where required so the lost-update and protected-update scenarios are repeatable.
