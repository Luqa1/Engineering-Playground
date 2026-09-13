# Distributed Lock

## Goal

Demonstrate why coordinating work across multiple application instances requires more than an in-process lock, and how a distributed lock can ensure that only one instance performs a protected operation at a time.

The PoC should teach the engineering problem first and the locking mechanism second.

The focus is:

- duplicate execution across multiple instances;
- shared coordination;
- lock acquisition;
- lock ownership;
- lock expiration;
- safe release;
- failure behavior.

## Business Scenario

Use a small scheduled background-processing scenario.

Multiple instances of the same application periodically attempt to run the same job.

Example conceptual job: `DailyReportJob`.

The job itself should remain intentionally simple. Conceptually, it may:

- write a record indicating the job ran;
- increment a processing counter;
- persist a deterministic execution result.

The exact business action should be minimal and exist only to make duplicate execution observable.

The important scenario is:

```text
Instance A wakes up
Instance B wakes up

Both decide the job should run

Without distributed coordination:

A executes the job
B executes the job

Result:
the same logical job ran twice
```

The PoC should then demonstrate how a distributed lock changes that behavior so only one instance enters the protected section.

Do not build a general-purpose job scheduler. Do not introduce Hangfire, Quartz, or similar frameworks.

## M1 – Repository Skeleton

Goal: Prepare the PoC structure and implementation plan.

Expected outcome:

- directory structure;
- initial README;
- PLAN;
- configuration placeholders.

## M2 – Running Application

Status: Complete.

Goal: Create the smallest working .NET 10 application needed for the multi-instance job scenario.

Expected outcome:

- .NET solution;
- background Worker application;
- small Domain project if useful;
- Infrastructure project;
- PostgreSQL only if required for observable job results;
- Docker Compose;
- one-command startup according to `AGENTS.md`.

The application should be runnable with multiple Worker instances. At this milestone there should be no distributed locking. The business job should be deterministic and easy to observe.

Avoid generic scheduling abstractions.

## M3 – Duplicate Execution Scenario

Status: Complete.

Goal: Demonstrate the problem before implementing the solution.

Create a deterministic scenario where two application instances attempt to execute the same logical job.

Without distributed locking:

- both instances are allowed to enter the protected work;
- the same logical operation executes twice;
- duplicate execution is observable.

Expected outcome:

- reproducible duplicate execution;
- tests or a controlled integration scenario proving both instances can execute;
- concise documentation explaining why an in-process lock would not solve coordination between separate processes.

Do not implement the distributed lock yet.

## M4 – Distributed Lock

Goal: Introduce a distributed lock so only one application instance can execute the protected operation at a time.

Use the simplest appropriate mechanism for this PoC. The implementation should demonstrate:

- lock acquisition;
- exclusive ownership;
- protected critical section;
- release after completion.

Select the backing mechanism during M4 based on clarity and educational value. Compare a simple Redis-based lock with PostgreSQL advisory locking, but implement only one.

Choose the mechanism that most clearly demonstrates distributed coordination with minimal incidental complexity. Do not introduce a generic distributed-lock framework unless required.

Expected outcome:

```text
Instance A acquires lock
Instance B attempts lock
Instance B does not enter protected section

Instance A executes job
Instance A releases lock
```

## M5 – Lock Expiration & Ownership

Goal: Demonstrate that distributed locks require explicit ownership semantics and failure handling.

Cover the scenario where an instance acquires the lock but terminates or becomes unavailable before releasing it. The lock mechanism must avoid leaving the system permanently blocked.

Expected concepts:

- lock ownership;
- unique owner token or equivalent mechanism;
- expiration, lease, or session semantics depending on the selected lock implementation;
- safe release;
- ensuring one instance cannot release another instance's lock.

If Redis is selected:

- use an ownership token;
- use expiration;
- release only when the stored token matches the owner;
- do not implement unsafe `GET` followed by `DEL` as separate non-atomic operations.

If PostgreSQL advisory locks are selected:

- document their connection/session ownership semantics;
- ensure the implementation correctly releases ownership when the session ends.

Do not build a complete lease-renewal subsystem unless absolutely required for the chosen demonstration.

## M6 – Multi-Instance Scenario

Goal: Demonstrate the complete behavior with multiple application instances.

Create a deterministic walkthrough:

```text
Worker A attempts job
Worker B attempts job

Worker A acquires lock

Worker B cannot acquire lock
        ↓
Worker B skips this execution

Worker A performs protected work
Worker A releases lock
```

Verify:

- only one instance executes the logical job;
- the second instance does not duplicate the protected work;
- lock release allows a later execution to acquire the lock;
- failure behavior matches the selected lock mechanism.

Clearly distinguish:

- acquiring a lock;
- owning a lock;
- executing the protected operation;
- releasing the lock.

The PoC should not imply that distributed locking automatically guarantees exactly-once processing in all distributed-system failure modes.

## M7 – Documentation

Goal: Turn the completed PoC into a concise engineering knowledge-base example.

Expected outcome:

- final README;
- architecture diagram;
- sequence diagram;
- duplicate-execution explanation;
- multi-instance walkthrough;
- lock ownership explanation;
- expiration and failure semantics;
- trade-offs;
- when to use;
- when not to use;
- production considerations.

The README should answer:

- What problem does a distributed lock solve?
- Why is `lock` or `SemaphoreSlim` insufficient across multiple processes?
- How does one instance become the lock owner?
- What happens when the owner crashes?
- Why does lock expiration or session ownership matter?
- Why must release be ownership-aware?
- What are the trade-offs?
- When should a distributed lock be avoided?

## Final Review

Perform a focused engineering review after M7.

Review:

- simplicity;
- correctness;
- ownership semantics;
- failure behavior;
- lock release;
- multi-instance behavior;
- tests;
- educational value;
- documentation accuracy.

Do not add functionality during Final Review.

## Design Constraints

### Technology

Use:

- .NET 10;
- C#;
- Docker Compose.

Use PostgreSQL only if needed for persistence or if PostgreSQL advisory locking is selected. Use Redis only if Redis-based locking is selected.

Follow all general conventions from `AGENTS.md`.

### Application Type

Prefer a Worker Service or similarly small background process.

The PoC should demonstrate multiple independent processes competing for the same protected operation. Do not introduce an ASP.NET Core API unless it is genuinely needed for the demonstration.

The default should be:

```text
Worker A
Worker B
    ↓
shared lock mechanism
    ↓
protected job
```

### Architecture

Keep the architecture intentionally small.

Do not introduce:

- CQRS;
- MediatR;
- repositories unless genuinely required;
- event-driven architecture;
- messaging;
- generic scheduler frameworks;
- generic lock abstractions with multiple providers;
- additional architectural layers.

This PoC demonstrates distributed coordination, not application architecture.

### In-Process Locks

Do not use an in-process lock as the solution.

Mechanisms such as `lock`, `Monitor`, a process-scoped `Mutex`, and `SemaphoreSlim` do not coordinate independent application instances. They may be mentioned for comparison, but they are not the distributed solution.

### Lock Ownership

The final implementation must have clear ownership semantics. A process must not be able to accidentally release a lock owned by another process.

Do not use a release strategy that blindly removes shared lock state without verifying ownership.

### Lock Expiration

The final implementation must address what happens if the lock owner disappears before normal release.

The exact mechanism depends on the selected implementation:

- expiration or lease;
- connection or session lifetime;
- another provider-specific ownership mechanism.

Do not leave this as an unexplained edge case.

### Exactly-Once Semantics

The PoC must not claim that a distributed lock creates universal exactly-once execution.

Documentation should eventually explain that failures can still occur around boundaries such as:

```text
perform side effect
        ↓
process crashes
        ↓
lock released/expires
```

Whether work may be repeated depends on the complete business operation and persistence design. Do not solve exactly-once processing in this PoC.

### Testing

Tests and scenarios must be deterministic.

Do not rely on:

- arbitrary `Task.Delay`;
- random timing;
- probabilistic races.

Coordinate competing workers explicitly where required so duplicate execution and protected execution are reproducible.

### Lock Mechanism Decision

Do not permanently select the lock provider during M1.

During M4, choose between a simple Redis-based lock and PostgreSQL advisory locking based on:

1. clarity of ownership semantics;
2. clarity of failure behavior;
3. minimal incidental infrastructure;
4. educational value;
5. fit with the multi-instance job scenario.

Do not add both mechanisms. One PoC should demonstrate one distributed-lock implementation clearly.
