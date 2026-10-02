# Saga Pattern

## Goal

Demonstrate how a multi-step business process can remain logically consistent when its steps cannot be covered by one atomic database transaction.

The PoC should teach the problem before the pattern.

The core progression is:

```text
multi-step business operation
        ↓
each step commits independently
        ↓
later step fails
        ↓
partial business state remains
        ↓
Saga orchestration
        ↓
compensating actions
        ↓
explicit process outcome
```

The focus is:

* partial failure;
* independently committed business steps;
* explicit Saga orchestration;
* compensating actions;
* Saga/process state;
* compensation failure;
* deterministic recovery behavior.

The PoC should remain intentionally small.

---

# Business Scenario

Use a small order-processing scenario.

The logical business process is:

```text
Create Order
      ↓
Reserve Inventory
      ↓
Process Payment
      ↓
Complete Order
```

Successful flow:

```text
Order created
      ↓
Inventory reserved
      ↓
Payment succeeds
      ↓
Order completed
```

Failure flow:

```text
Order created
      ↓
Inventory reserved
      ↓
Payment fails
      ↓
inventory reservation already exists
      ↓
partial business state
```

The Saga solution should later introduce compensation:

```text
Order created
      ↓
Inventory reserved
      ↓
Payment fails
      ↓
Release Inventory
      ↓
Cancel Order
```

The exact domain should remain minimal.

Do not turn this into a complete e-commerce system.

---

# Core Learning Goal

The PoC must clearly demonstrate why this cannot be treated as one simple rollback when the business steps commit independently.

For example:

```text
Inventory transaction
        ↓
COMMIT

Payment operation
        ↓
FAIL
```

At that point the inventory reservation has already been committed.

A normal rollback of the failed payment operation cannot undo the previously committed inventory transaction.

The system needs a new business operation:

`Release Inventory`

That is a compensating action.

This distinction is central to the PoC.

---

# M1 – Repository Skeleton

Status: **Completed**.

Goal:

Prepare the PoC structure and implementation plan.

Expected outcome:

* directory structure;
* initial README;
* PLAN;
* configuration placeholders.

No application implementation.

---

# M2 – Running Application

Status: **Completed**.

Goal:

Create the smallest runnable .NET 10 application required for the order-processing scenario.

Expected domain concepts:

* `Order`;
* inventory state or reservation;
* payment result/state only where necessary.

Expected business operations:

* create order;
* reserve inventory;
* process payment;
* complete order.

At this milestone:

* implement the successful business flow;
* persist the relevant business state;
* use PostgreSQL;
* support one-command Docker startup.

Do NOT implement Saga orchestration yet.

Do NOT implement compensating actions yet.

Keep the business flow explicit.

Avoid generic workflow abstractions.

---

# M3 – Partial Failure Scenario

Status: **Completed**.

Goal:

Demonstrate the problem before implementing the Saga solution.

Introduce a deterministic payment failure.

Required scenario:

```text
Create Order
      ↓
Reserve Inventory
      ↓
COMMIT reservation
      ↓
Process Payment
      ↓
FAIL
```

Verify that after the payment failure:

* the order exists;
* inventory has already been reserved;
* payment did not succeed;
* the business process is incomplete;
* a normal rollback of the failed step does not undo the previously committed reservation.

The failure must be deterministic.

Do not use randomness.

Do not implement compensation yet.

The milestone should finish with deliberately inconsistent/incomplete business state so the reason for Saga is visible.

---

# M4 – Saga Orchestration

Status: **Completed**.

Goal:

Introduce explicit orchestration for the multi-step order process.

Use an orchestration-based Saga.

The orchestrator should make the process visible:

```text
Create Order
      ↓
Reserve Inventory
      ↓
Process Payment
      ↓
Complete Order
```

The orchestrator decides which step executes next.

Do not introduce a generic workflow engine.

Prefer an explicit class with a clear domain-specific name such as:

`OrderSaga`

or:

`OrderProcessingSaga`

The exact name may be chosen during implementation.

Avoid generic abstractions such as:

* `ISagaEngine`;
* `IWorkflowEngine`;
* generic step pipelines;
* generic state machines;
* reflection-based orchestration.

The PoC demonstrates Saga semantics, not framework design.

---

# M5 – Compensating Actions

Status: **Completed**.

Goal:

Add compensation for the deterministic payment failure.

Required failure flow:

```text
Create Order
      ↓
Reserve Inventory
      ↓
Process Payment
      ↓
FAIL
      ↓
Release Inventory
      ↓
Cancel Order
```

The important concept is:

> Compensation is a new business operation, not a rollback of the original committed transaction.

`Release Inventory` must explicitly reverse the business effect of the previous reservation.

The final state should clearly show:

* payment failed;
* inventory is no longer reserved for the failed order;
* order is cancelled.

Keep compensation explicit in the orchestration code.

Do not hide it behind generic framework behavior.

---

# M6 – Saga State & Compensation Failure

Status: **Completed**.

Goal:

Demonstrate that compensating actions can also fail.

Introduce explicit Saga/process state only to the extent required to make the failure visible and recoverable.

Example scenario:

```text
Create Order
      ↓
Reserve Inventory
      ↓
Payment fails
      ↓
Release Inventory
      ↓
FAIL
```

The system must not incorrectly report the Saga as successfully compensated.

Persist enough Saga state to distinguish outcomes such as:

* Running;
* Completed;
* Compensating;
* Compensated;
* CompensationFailed.

Use the smallest state model that accurately represents the implemented workflow.

Do not build a generic Saga state machine.

Do not implement a full production retry scheduler.

The important lesson is:

> Compensation is distributed work too, and it can fail.

The failure must remain observable.

---

# M7 – End-to-End Scenarios

Status: **Completed**.

Goal:

Demonstrate the complete Saga behavior through deterministic integration scenarios.

Cover at least:

## Successful flow

```text
Create Order
      ↓
Reserve Inventory
      ↓
Payment succeeds
      ↓
Complete Order
```

Verify final business state.

## Payment failure with successful compensation

```text
Create Order
      ↓
Reserve Inventory
      ↓
Payment fails
      ↓
Release Inventory
      ↓
Cancel Order
```

Verify final compensated state.

## Compensation failure

```text
Create Order
      ↓
Reserve Inventory
      ↓
Payment fails
      ↓
Release Inventory fails
      ↓
Saga remains visibly unresolved
```

All scenarios must be deterministic.

Do not use:

* random failures;
* arbitrary `Task.Delay`;
* probabilistic races.

---

# M8 – Documentation

Status: **Completed**.

Goal:

Turn the completed PoC into a concise engineering knowledge-base example.

The final README should explain:

* what problem Saga solves;
* why one ACID transaction is insufficient for independently committed operations;
* what partial failure means;
* what Saga orchestration does;
* what a compensating action is;
* why compensation is not rollback;
* why compensation itself can fail;
* why Saga state matters;
* successful flow;
* failed flow;
* compensated flow;
* unresolved compensation flow;
* trade-offs;
* when to use;
* when not to use;
* production considerations.

Include:

* architecture diagram;
* successful sequence;
* failure/compensation sequence;
* deterministic test scenarios;
* local running instructions.

Do not oversell Saga guarantees.

---

# Final Review

After M8, perform a separate Final Review & Polish.

This is not another numbered milestone.

Review:

* architecture simplicity;
* transaction boundaries;
* partial-failure correctness;
* orchestration clarity;
* compensation correctness;
* Saga state transitions;
* compensation-failure behavior;
* deterministic tests;
* documentation accuracy;
* Docker startup;
* repository consistency.

Do not add new functionality during Final Review.

---

# Design Constraints

The following decisions guide later milestones.

---

## Technology

Use:

* .NET 10;
* C#;
* PostgreSQL;
* Docker Compose.

Use ASP.NET Core Controllers if an HTTP entry point is required.

Never use Minimal APIs for this PoC.

Follow all repository-wide conventions from `AGENTS.md`.

---

# Architecture Direction

Keep the architecture intentionally small.

Do not choose a microservice architecture merely because Saga is commonly discussed in microservice contexts.

The Saga pattern can be demonstrated through independently committed business operations without requiring deployment into multiple services.

Prefer the smallest architecture that clearly exposes:

* separate transaction boundaries;
* partial failure;
* orchestration;
* compensation.

Do not introduce separate deployable services unless a later milestone demonstrates that they are necessary for the educational goal.

---

# Important: Transaction Boundaries

The PoC must not fake the Saga problem by wrapping the entire workflow in one EF Core/database transaction.

The relevant business steps must have independent commit boundaries.

Conceptually:

```text
Reserve Inventory
      ↓
COMMIT

Process Payment
      ↓
independent operation
```

This is essential.

If one transaction can simply roll everything back, the example does not demonstrate the problem Saga is meant to solve.

---

# Persistence

PostgreSQL may contain all PoC data.

Using one PostgreSQL instance does not mean all Saga steps should share one atomic transaction.

The PoC should intentionally establish separate transaction boundaries between relevant business operations.

The educational boundary is more important than pretending to have physically separate databases.

Do not introduce multiple PostgreSQL instances merely to simulate microservices.

---

# Saga Style

Use:

**orchestration-based Saga**

The process should have an explicit coordinator.

Do not use choreography for this PoC.

The purpose is to make:

* step ordering;
* failure decisions;
* compensation ordering;
* Saga state

easy to see in code.

---

# Messaging

Do NOT commit to RabbitMQ or another message broker in M1.

The core Saga concept does not require a broker to be demonstrated.

Do not add:

* RabbitMQ;
* Kafka;
* Azure Service Bus;
* MassTransit;
* NServiceBus.

A later implementation decision may introduce messaging only if there is a clear educational reason.

Default assumption:

> Keep orchestration synchronous and explicit unless asynchronous messaging becomes necessary to demonstrate a specific Saga property.

The PoC is about Saga, not messaging infrastructure.

---

# Payment

Do not integrate a real payment provider.

Use a deterministic simulated payment operation.

It must support at least:

* success;
* deterministic failure.

The exact trigger should be explicit and testable.

Do not use randomness.

---

# Inventory

Keep inventory deliberately small.

It only needs enough behavior to demonstrate:

```text
available
    ↓
reserve
    ↓
reserved
```

and later:

```text
reserved
    ↓
release
    ↓
available
```

Do not build:

* warehouse management;
* multiple warehouses;
* allocation algorithms;
* stock replenishment;
* complex inventory models.

---

# Order

Keep Order state minimal.

It should eventually be able to represent only the states required by the scenario, such as:

* Pending;
* Completed;
* Cancelled.

Do not design a full order lifecycle.

---

# Compensation

Compensation must be explicit business behavior.

Examples:

```text
ReserveInventory
```

is compensated by:

```text
ReleaseInventory
```

The compensation should not be described as:

```text
rollback ReserveInventory
```

after the reservation transaction has already committed.

The compensating action is a new transaction.

---

# Compensation Ordering

If multiple completed steps eventually require compensation, compensate in reverse logical order where appropriate.

For the initial scenario, keep the number of compensating operations minimal.

Do not add artificial steps solely to demonstrate reverse ordering.

---

# Saga State

Do not introduce persisted Saga state before it becomes useful.

M2–M5 should remain as small as possible.

M6 introduces explicit Saga/process state because compensation failure creates a concrete need to distinguish unresolved process outcomes.

Avoid speculative state-machine design in earlier milestones.

---

# Failure Injection

Failures must be deterministic and explicit.

Acceptable examples:

```text
PaymentMode = Fail
```

or an equivalent test/configuration mechanism.

Likewise, M6 may deterministically trigger compensation failure.

Do not use:

* randomness;
* timing;
* network chaos;
* arbitrary exceptions scattered through business code.

Failure injection should remain obvious to a reader.

---

# Exactly-Once Semantics

The PoC must NOT claim that Saga provides exactly-once execution.

Saga coordinates a sequence of local transactions and compensating actions.

It does not automatically guarantee:

* exactly-once side effects;
* exactly-once message delivery;
* automatic idempotency;
* automatic recovery from every failure.

Document these limitations later.

Do not solve them all in this PoC.

---

# Distributed Transactions

Do not use:

* MSDTC;
* two-phase commit;
* distributed ACID transactions.

The point of the PoC is to demonstrate coordination without one global atomic transaction.

---

# Idempotency

Do not build a general idempotency framework.

Compensating operations should be reasonably safe and explicit, but full production-grade idempotency belongs outside this PoC unless required for correctness of a specific implemented scenario.

Document it later as a production consideration.

---

# Outbox

Do not implement the Outbox Pattern as part of this PoC.

Outbox is a separate repository topic.

If asynchronous messaging is eventually introduced, do not silently expand this Saga PoC into another Outbox implementation.

---

# Testing

Integration tests must demonstrate business state, not merely method calls.

Use real PostgreSQL where transaction boundaries and persistence matter.

Do not use EF Core InMemory as proof of Saga transaction behavior.

Tests must be deterministic.

The important assertions should inspect persisted final state after each scenario.

---

# Local Developer Experience

Eventually the PoC must support:

```bash
docker compose up --build
```

with no manual:

* database creation;
* migration execution;
* service startup sequencing.

Follow the migration conventions from `AGENTS.md`.

Do not implement this in M1.
