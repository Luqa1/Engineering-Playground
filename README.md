# Engineering Playground

Engineering Playground is an internal engineering knowledge base of small, production-inspired Proofs of Concept (PoCs). It demonstrates software engineering principles and architectural patterns through focused, practical examples—not a collection of CRUD applications.

## Principles

Every PoC demonstrates one engineering concept and is:

- independent
- self-contained
- runnable
- production-inspired
- well documented

When applicable, each PoC includes source code, its own README, an architecture diagram, a sequence diagram, Docker Compose configuration, and screenshots.

Each PoC README answers:

- What problem does this solve?
- Why does this pattern exist?
- How does it work?
- What are the trade-offs?
- When should it be used?
- When should it be avoided?

## Roadmap

1. [Outbox Pattern](01-outbox-pattern/) — atomically persists business data and integration events, then publishes them asynchronously. **Status: Completed.**
2. Structured Logging & Observability
3. Optimistic Concurrency
4. Distributed Lock
5. Saga Pattern
6. CQRS

See [ROADMAP.md](ROADMAP.md) for the planned PoCs. More engineering topics will be added over time.

The complete Outbox Pattern PoC can be started from its directory with `docker compose up --build`. See its README for testing and cleanup commands.
