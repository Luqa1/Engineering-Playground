# CQRS Implementation Plan

**Status: Completed.** M1 through M8 are complete.

The scenario is a Product Catalog & Inventory Dashboard. The educational goal is to separate write-side business behavior from read-side presentation needs while retaining one API and one PostgreSQL database.

## M1 — Repository Skeleton

**Status: Completed.**

- Create the README, implementation plan, and empty `docs/`, `diagrams/`, `screenshots/`, `src/`, and `tests/` directories.
- Add comment-only Docker Compose and environment placeholders.
- Link the PoC from the root README and roadmap as In Progress.
- Validate the structure and scope without creating .NET projects, a solution, or production code.

## M2 — Running Application

**Status: Completed.**

- Create a small .NET 10 Product Catalog API using ASP.NET Core Controllers, PostgreSQL, and EF Core.
- Use a shared Product domain/persistence model for reads and writes, with request DTOs for writes. M2 intentionally returns Product directly, as an explicit exception to the repository's response DTO convention.
- Keep controllers thin and use constructor injection; put business rules in Domain and persistence in Infrastructure.
- Keep DbContext in Infrastructure and entity mapping in `IEntityTypeConfiguration<T>` implementations outside `Program.cs`.
- Provide `docker compose up --build` with API and database services, practical healthchecks, and reliable startup ordering.
- Have only the API apply EF Core migrations during non-production startup. Require controlled migrations in Production; do not use `EnsureCreated()`.

Completion: the shared-model application runs locally with one command and no manual database creation or migration steps.

## M3 — The Read/Write Model Problem

**Status: Completed.**

- Introduce inventory dashboard requirements combining product information and stock availability.
- Demonstrate how presentation needs differ from business-oriented writes and expose limitations of the shared model.
- Document concrete coupling and query-shape limitations without prematurely implementing CQRS.

Completion: a working example makes the motivation for separating responsibilities understandable.

## M4 — Command Side

**Status: Completed.**

- Introduce explicit write operations for product price updates and inventory adjustments.
- Preserve domain invariants and independent input validation.
- Use explicit services and methods with thin controllers and request/response DTOs.

Completion: each write operation has clear intent and enforces its business rules independently of read requirements.

## M5 — Query Side

**Status: Completed.**

- Introduce dedicated read DTOs for product details and the inventory dashboard.
- Use efficient EF Core database projections to retrieve the data each view needs.
- Keep read results independent of write-side entity shapes and behavior.

Completion: both read scenarios use dedicated projections from the same PostgreSQL database.

## M6 — CQRS Separation

**Status: Completed.**

- Make command and query responsibilities explicit and independently understandable.
- Retain one API and one database; prefer simple services and methods.
- Review separation without adding frameworks, infrastructure, or generic dispatch abstractions.

Completion: readers can trace each command and query independently and explain how both share persisted data.

## M7 — Integration Tests & Trade-offs

**Status: Completed.**

- Add xUnit integration tests for write invariants, read projections, and separation of responsibilities.
- Verify deterministic end-to-end behavior with isolated test data and controlled database setup.
- Evaluate when CQRS improves clarity and query behavior, and when its additional code is unnecessary complexity.

Completion: integration tests verify the scenarios and the documented trade-offs reflect the implemented design.

## M8 — Documentation

**Status: Completed.**

- Complete the README, architecture and applicable sequence diagrams, API examples, limitations, and trade-offs.
- Document when to use and when not to use this approach.
- Verify local running instructions, started services, automatic non-production migrations, and controlled Production migration execution.
- Add screenshots only when they improve understanding.

Completion: documentation matches the working example and explains the full educational progression.

Final Review & Polish will be a separate task after M8.

## Planned Project Boundaries

Create projects only in later milestones when needed:

- `EngineeringPlayground.Cqrs.Api`: Controllers and composition; depends on Domain and Infrastructure.
- `EngineeringPlayground.Cqrs.Domain`: write-side business rules; no project dependencies.
- `EngineeringPlayground.Cqrs.Infrastructure`: persistence and query projections; depends on Domain.
- `EngineeringPlayground.Cqrs.IntegrationTests`: xUnit integration verification.

Do not add an Application project solely for architectural purity, shared libraries across PoCs, or separately deployable read/write services.

## Design Constraints

- Keep each milestone small, focused, and independently understandable; implement only the current milestone.
- Keep domain entities focused on write-side rules and use efficient EF Core projections for read DTOs.
- CQRS does not require separate databases, microservices, asynchronous messaging, MediatR, or Event Sourcing, and does not automatically imply eventual consistency.
- Do not introduce generic command/query buses, generic handler pipelines, or a mediator abstraction.
- Do not add Outbox, message brokers, or an asynchronously maintained materialized read database.
