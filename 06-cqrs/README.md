# CQRS — Product Catalog & Inventory Dashboard

**Status: Work in Progress.** M1 — Repository Skeleton is complete. The application and CQRS behavior are planned for later milestones; no runnable application exists yet. See [PLAN.md](PLAN.md).

## Overview

This PoC will demonstrate Command Query Responsibility Segregation (CQRS): separating behavior that changes state from queries that present it. The focus is read/write responsibilities in one small API with one PostgreSQL database.

## The Problem

Writes enforce business rules, while reads serve presentation needs. Using the same model for both can couple business behavior to dashboard layouts and make queries load more data than they need. CQRS gives each responsibility an explicit shape when those needs diverge.

## Example Scenario

The planned Product Catalog & Inventory Dashboard will support:

- Writes: update a product price and adjust inventory quantity.
- Reads: get product details and an inventory dashboard with product information and stock availability.

## Shared Model

M2 will start with a shared domain/persistence model for reads and writes. Controllers will use request and response DTOs without exposing EF Core entities. M3 will show the limitations of the shared model as dashboard requirements grow, before introducing CQRS.

## Command Side

Planned: explicit services and methods for price updates and inventory adjustments, with independent input validation and domain invariants. Controllers will stay thin; business rules will live in Domain and persistence in Infrastructure.

## Query Side

Planned: dedicated queries for product details and the inventory dashboard, using efficient EF Core projections directly into read DTOs.

## Read Models

Planned read models will contain the fields each view needs, including stock availability for the dashboard. They will be query results projected from the shared database, without an asynchronously maintained materialized read database.

## CQRS Without Separate Databases

The planned final design will keep one API and one PostgreSQL database. Commands and queries will have distinct responsibilities while sharing persisted data. This design does not introduce asynchronous propagation between read and write stores.

## What CQRS Does Not Require

CQRS does not require separate databases, microservices, asynchronous messaging, MediatR, or Event Sourcing, and does not automatically imply eventual consistency. This PoC will use explicit services and methods without generic buses, handler pipelines, a mediator abstraction, Outbox, or message brokers.

## Trade-offs

Dedicated write behavior and read projections can make business rules and query intent clearer. They also add types and mapping code, and read DTOs must evolve with presentation requirements. Sharing a database keeps operations simple but does not provide independent read/write storage scaling.

## When to Use

Consider this approach when business-oriented writes and presentation-oriented reads have meaningfully different requirements, and separating them improves clarity or query efficiency.

## When Not to Use

Prefer a simpler shared model when straightforward CRUD meets the requirements and separate command/query code would add little value.

## Running the Example

M1 contains placeholders only: `docker-compose.yml` has no services and `.env.example` defines no variables. The example cannot be started yet.

From M2, the planned primary local workflow from `06-cqrs/` will be:

```bash
docker compose up --build
```

The planned environment will start the API and PostgreSQL with healthchecks and reliable startup ordering. The API will apply EF Core migrations automatically only in non-production environments. Production will require controlled migration execution; background services will never apply migrations.

Planned stack: .NET 10, C#, ASP.NET Core Controllers, PostgreSQL, EF Core, Docker Compose, and xUnit integration tests. Architecture and sequence diagrams, API examples, and useful screenshots will be completed in later milestones.
