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
