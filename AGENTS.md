# Repository Guidelines for AI Agents

These rules apply repository-wide to all future AI-assisted work.

## General Principles

- Demonstrate one engineering concept per PoC.
- Keep every example independent and self-contained.
- Prefer production-inspired approaches.
- Favor simplicity over completeness.
- Favor readability over cleverness.

## Technology Stack

- .NET 10
- C#
- PostgreSQL
- Docker Compose
- Visual Studio 2022

## Repository Conventions

- Every PoC lives in its own folder.
- Do not create shared libraries between PoCs.
- Use minimal dependencies.
- Keep examples small and focused.

## Documentation Expectations

Each PoC should include, when applicable:

- a README
- an architecture diagram
- a sequence diagram
- screenshots when they improve understanding

## Implementation Philosophy

Always explain:

- the problem
- the motivation
- the implementation
- the trade-offs
- when to use the approach
- when not to use the approach

Avoid:

- unnecessary abstractions
- overengineering
- combining multiple engineering concepts in one PoC
- unnecessary infrastructure

## Project naming

Every project must use the following naming convention:

EngineeringPlayground.<Topic>.<Project>

Examples:

EngineeringPlayground.Outbox.Api
EngineeringPlayground.Outbox.Worker
EngineeringPlayground.Outbox.Domain
EngineeringPlayground.Outbox.Infrastructure
EngineeringPlayground.Outbox.IntegrationTests

## API Conventions

The repository uses standard ASP.NET Core Controllers by default.

Rules:

- Use ASP.NET Core Controllers by default.
- Do not use Minimal APIs unless a PoC explicitly demonstrates them.
- Keep controllers thin.
- Business logic must not live inside controllers.
- Use request and response DTOs.
- Never expose EF Core entities directly from API endpoints.
- Use constructor dependency injection.
- Prefer explicit and readable code over compact implementations.

## Architecture Philosophy

The goal of this repository is to demonstrate engineering concepts, not architectural styles.

Rules:

- Keep every PoC as small as possible while remaining production-inspired.
- Prefer simple architecture over unnecessary abstractions.
- Do not introduce additional layers unless they are required by the pattern being demonstrated.
- Avoid overengineering.
- Every project should have a clear responsibility.

## Layering

The standard project structure is:

API

Domain

Infrastructure

Worker

Rules:

- Domain must not depend on any other project.
- Infrastructure depends on Domain.
- API depends on Domain and Infrastructure.
- Worker depends on Domain and Infrastructure.
- Keep business rules inside the Domain project.
- Keep persistence and external integrations inside Infrastructure.
- Dependencies must always point toward the Domain layer.

# Engineering Conventions

The following conventions apply to every Proof of Concept unless explicitly overridden.

## Technology

- .NET 10
- C#
- PostgreSQL
- Docker Compose
- Visual Studio 2022

## API

- Use ASP.NET Core Controllers by default.
- Do not use Minimal APIs unless a PoC explicitly demonstrates them.
- Keep controllers thin.
- Use request and response DTOs.
- Never expose EF Core entities directly from API endpoints.
- Use constructor dependency injection.

## Architecture

- Keep every PoC as small as possible while remaining production-inspired.
- Demonstrate one engineering concept per PoC.
- Avoid unnecessary abstractions.
- Avoid overengineering.
- Every project should have a single clear responsibility.

## Layering

Projects should follow this dependency direction:

API
↓
Domain
↑
Infrastructure

Worker
↓
Domain
↑
Infrastructure

Rules:

- Domain must not depend on any other project.
- Infrastructure depends on Domain.
- API depends on Domain and Infrastructure.
- Worker depends on Domain and Infrastructure.

## Entity Framework Core

- Configure entities using IEntityTypeConfiguration<T>.
- Keep DbContext inside Infrastructure.
- Keep EF Core configuration outside Program.cs.

## Messaging

- Integration event names must be stable and independent of .NET type names.
- Use lowercase dot notation.

Examples:

- order.created
- order.cancelled
- payment.completed

Never store:

- CLR type names
- Assembly-qualified names
- Namespace-qualified names

## Documentation

Every completed PoC should include:

- README
- architecture diagram
- sequence diagram (when applicable)
- Docker Compose
- screenshots (when useful)

Every README should explain:

- the problem
- why the pattern exists
- implementation
- trade-offs
- when to use
- when not to use

# Decision Making

## Simplicity First

Do not introduce new:

- libraries
- architectural patterns
- abstractions
- project structure
- frameworks

unless they are required to demonstrate the engineering concept implemented by the current PoC.

## Choosing Between Multiple Solutions

When multiple valid solutions exist:

- choose the simplest solution;
- choose the solution that best supports the educational goal of the PoC;
- prefer readability over flexibility;
- prefer explicit code over clever code;
- avoid optimizing for hypothetical future requirements.

## Scope

Implement only what is required for the current milestone.

Do not anticipate future milestones.

Avoid adding infrastructure or extensibility that is not yet needed.

## Goal

The purpose of this repository is to teach engineering concepts through small, production-inspired examples.

Every implementation decision should support that goal.

## Local Development

Future PoCs should provide the best possible local developer experience while remaining simple.

### Docker Compose

Every PoC should be runnable locally using a single command:

```bash
docker compose up --build
```

A developer should not be required to manually:

- create the database;
- execute EF Core migrations;
- start individual services in a specific order.

Docker Compose should orchestrate the complete local environment whenever practical.

### Automatic EF Core migrations

When a PoC uses Entity Framework Core:

- the API is responsible for applying EF Core migrations during startup;
- automatic migrations are enabled only for non-production environments;
- Production must never apply migrations automatically;
- the Worker and other background services must never execute migrations.

Use EF Core migrations.

Do not use:

- EnsureCreated()
- automatic schema generation outside EF Core migrations.

### Service startup

Docker Compose should:

- start all required infrastructure services;
- use healthchecks whenever practical;
- start application services in a reliable order.

Avoid startup scripts if Docker Compose healthchecks and application startup logic are sufficient.

### Documentation

Every PoC README should make the primary local startup workflow:

```bash
docker compose up --build
```

The README should clearly explain:

- what services are started;
- that migrations are applied automatically in non-production environments;
- that Production environments require controlled migration execution.
