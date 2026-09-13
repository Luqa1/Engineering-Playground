# Distributed Lock

This Proof of Concept will demonstrate how multiple application instances coordinate access to the same protected operation with a distributed lock.

> **Work in Progress**
>
> The implementation and documentation will be completed in later milestones.

## Problem

Two independent Worker processes can decide to run the same logical job. Without shared coordination, each process can execute the operation successfully.

## Duplicate Execution

TODO.

## Why Distributed Locks Exist

TODO.

## Example Scenario

`DailyReportJob` is a deliberately small business operation. Each execution writes a `JobExecution` row containing the job name, Worker instance, start time, and completion time.

The local environment runs the same Worker image twice:

- `worker-a` executes the job once;
- `worker-b` executes the job once;
- PostgreSQL stores both execution records.

## How It Works

Each Worker has a stable identifier supplied through `WORKER_INSTANCE`. A standard `BackgroundService` resolves and executes `DailyReportJob` once after startup, then stops its host. The job first persists that execution started, then marks the record complete and persists the completion time.

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
docker compose up --build
```

This starts PostgreSQL, Worker A, and Worker B. PostgreSQL must become healthy before Worker A starts. In the local `Development` environment, Worker A is the only migration owner (`APPLY_MIGRATIONS=true`). After Worker A has migrated the database and completed its one-shot execution, Compose starts Worker B. This explicit startup order prevents Worker B from using the schema before it exists; it does not prevent either Worker from executing the job.

No manual database creation or migration command is required. Automatic migrations are disabled in `Production`; production deployments require a controlled migration step.

To inspect the executions while the PostgreSQL container is running:

```bash
docker compose exec postgres psql -U postgres -d distributed_lock -c 'SELECT "JobName", "WorkerInstance", "StartedAtUtc", "CompletedAtUtc" FROM "JobExecutions" ORDER BY "StartedAtUtc";'
```
