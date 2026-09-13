# Distributed Lock

This Proof of Concept will demonstrate how multiple application instances coordinate access to the same protected operation with a distributed lock.

> **Work in Progress**
>
> The implementation and documentation will be completed in later milestones.

## Problem

Two independent Worker processes can decide to run the same logical job. Without shared coordination, each process can execute the operation successfully.

## Duplicate Execution

Both Workers receive the same logical job identity:

```text
JobName       ExecutionKey
daily-report  2026-09-13
```

`JobName` identifies the kind of operation. `ExecutionKey` identifies the one
execution window that should be performed. `WorkerInstance` identifies the
application instance that attempted it; it is not part of the logical job
identity.

Without shared coordination, the scenario is:

```text
Worker A                       Worker B
   |                              |
   | daily-report / 2026-09-13    | daily-report / 2026-09-13
   |                              |
   | execute                      | execute
   |                              |
   +---------- PostgreSQL --------+
              two records
```

Worker A has only its own process state, and Worker B has separate process
state. Neither instance knows that the other instance has decided to perform
the same logical operation. PostgreSQL intentionally has no unique constraint
on `(JobName, ExecutionKey)`, so both executions are valid from the database's
perspective and both are persisted.

The problem is not merely that two inserts occurred. The inserts are durable
evidence that two independent application instances both believed they were
allowed to perform one logical business operation.

The integration test reproduces this deterministically. It creates two
`DailyReportJob` objects backed by separate `DbContext` instances and uses a
test-only rendezvous before their first save. Both participants therefore
commit to the same job before either persists its execution. The rendezvous is
test orchestration only; no synchronization mechanism is added to production.

## Why Distributed Locks Exist

Shared mutual exclusion is needed when only one application instance may enter
a protected section. A later milestone will introduce that mechanism. It will
not claim universal exactly-once execution, which also depends on business side
effects and behavior at failure boundaries.

### Why `lock` Is Not Enough

An in-process critical section could serialize threads in one Worker:

```csharp
lock (_gate)
{
    ExecuteJob();
}
```

It cannot coordinate Worker A and Worker B because each process has its own
memory and its own `_gate` object. A process-local `SemaphoreSlim` has the same
limitation. Neither mechanism tells another application instance that the job
is already running.

## Example Scenario

`DailyReportJob` is a deliberately small business operation. Each execution writes a `JobExecution` row containing the job name, execution key, Worker instance, start time, and completion time.

The local environment runs the same Worker image twice:

- `worker-a` executes the job once;
- `worker-b` executes the job once;
- both use execution key `2026-09-13`;
- PostgreSQL stores both execution records for that key.

## How It Works

Each Worker has a stable identifier supplied through `WORKER_INSTANCE`. Both
receive the same `JOB_EXECUTION_KEY`. A standard `BackgroundService` resolves
and executes `DailyReportJob` once after startup, then stops its host. The job
first persists that execution started, then marks the record complete and
persists the completion time.

Worker A and Worker B are independent. There is no mechanism coordinating access to the logical job, and PostgreSQL intentionally accepts both records.

**Distributed locking is intentionally not implemented yet.** PostgreSQL is currently used only to make job executions observable; this milestone does not select the future lock provider.

## Lock Ownership

TODO.

## Lock Expiration

TODO.

## Failure Scenarios

TODO.

## Trade-offs

TODO.

## When to Use

TODO.

## When Not to Use

TODO.

## Running the Example

From this directory, run:

```bash
docker compose down -v
docker compose up --build
```

This starts PostgreSQL, Worker A, and Worker B. PostgreSQL must become healthy before Worker A starts. In the local `Development` environment, Worker A is the only migration owner (`APPLY_MIGRATIONS=true`). After Worker A has migrated the database and completed its one-shot execution, Compose starts Worker B. This explicit startup order prevents Worker B from using the schema before it exists; it does not prevent either Worker from executing the job.

No manual database creation or migration command is required. Automatic migrations are disabled in `Production`; production deployments require a controlled migration step.

To inspect the executions while the PostgreSQL container is running:

```bash
docker compose exec postgres psql -U postgres -d distributed_lock -c 'SELECT "JobName", "ExecutionKey", "WorkerInstance" FROM "JobExecutions" ORDER BY "WorkerInstance";'
```

The expected durable result is two executions of the same logical job:

```text
   JobName   | ExecutionKey | WorkerInstance
-------------+--------------+----------------
 daily-report | 2026-09-13   | worker-a
 daily-report | 2026-09-13   | worker-b
```
