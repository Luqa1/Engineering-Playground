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
