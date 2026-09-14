# Distributed Lock

This Proof of Concept demonstrates how multiple application instances coordinate
access to the same protected operation with a distributed lock.

> **Work in Progress**
>
> M4 adds the core mutual-exclusion mechanism. Later milestones will focus on
> failure semantics and the final documentation review.

## Problem

Two independent Worker processes can decide to run the same logical job. Without
shared coordination, each process can execute the operation successfully.

Both Workers receive the same logical job identity:

```text
JobName       ExecutionKey
daily-report  2026-09-13
```

`JobName` identifies the operation. `ExecutionKey` identifies the execution
window. `WorkerInstance` identifies the application instance that attempted the
job; it is not part of the logical job identity.

## Duplicate Execution

Before M4, the scenario was:

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

PostgreSQL intentionally has no unique constraint on `(JobName, ExecutionKey)`.
The M3 integration test preserves evidence of this underlying unsafe path by
executing two unprotected `DailyReportJob` instances. Both records are accepted,
showing that persistence alone does not coordinate the Workers.

An in-process `lock` or `SemaphoreSlim` can coordinate threads that share one
process:

```text
lock / SemaphoreSlim
        ↓
coordinates threads in one process
```

Worker A and Worker B have separate memory, so each would own a different
in-process lock:

```text
PostgreSQL advisory lock
        ↓
coordinates independent processes through shared infrastructure
```

## Distributed Lock

M4 uses a **PostgreSQL session-level advisory lock**. PostgreSQL was selected
because it already exists in this PoC, advisory locks coordinate independent
database sessions rather than process-local memory, session ownership gives the
lock a clear lifecycle, and no Redis service or distributed-lock library is
needed.

Each Worker tries the lock once with `pg_try_advisory_lock`. This is non-blocking:
the Worker either becomes the owner immediately or logs that another instance
owns the lock and skips the current execution. There is no polling, retry, or
backoff.

```text
Worker A                PostgreSQL                Worker B

try lock(key)
    ----------------------->
                        lock granted

                                                try lock(key)
                                                ----------->
                                                not granted

execute DailyReportJob

unlock(key)
    ----------------------->

                                                try lock(key)
                                                ----------->
                                                lock granted
```

`DailyReportJobRunner` protects only the business operation. `DailyReportJob`
still creates and completes the `JobExecution` record; unrelated Worker startup
and database-migration behavior remain outside the lock.

## Lock Key

The logical lock identity is `JobName + ExecutionKey`. The implementation joins
their UTF-8 representations with an explicit null separator, hashes that value
with SHA-256, and interprets the first eight hash bytes as a signed 64-bit
big-endian integer for PostgreSQL's advisory-lock key space.

This conversion is deterministic across processes and runtime restarts.
`string.GetHashCode()` is not used because it is runtime-randomized and is not a
stable coordination contract. As with any fixed-size hash, a collision is
theoretically possible.

## Connection and Session Ownership

> A session-level PostgreSQL advisory lock belongs to the database session that
> acquired it.

```text
lock ownership lifetime
        =
PostgreSQL session lifetime
```

`PostgresAdvisoryLock` opens a dedicated `NpgsqlConnection` for each acquisition.
When acquisition succeeds, the connection remains open while `DailyReportJob`
executes. The lease explicitly calls `pg_advisory_unlock` on that same connection
in a `finally` cleanup path, then disposes the connection. A failed acquisition
closes its connection immediately and never attempts an unlock.

The ordinary EF Core `DbContext` connection lifecycle is not used as implicit
lock ownership. M5 will examine abnormal termination and session-loss behavior;
M4 establishes normal acquisition, mutual exclusion, and release.

## Behavior and Limitations

The M4 guarantee is deliberately narrow:

> Competing participating Worker instances cannot simultaneously enter the same
> protected job execution while the advisory lock is held.

This is not a claim of exactly-once execution. Advisory locks do not persist
through a PostgreSQL restart, cannot protect clients that ignore the locking
protocol, and do not replace database constraints or idempotent business logic.

The approach is useful when a small number of cooperating processes already use
PostgreSQL and need mutual exclusion around a short logical operation. It is a
poor fit when the database should not be part of coordination, when work must be
queued rather than skipped, or when correctness requires durable state beyond a
database session.

## Tests

The integration suite uses a real PostgreSQL container and verifies:

- the M3 unprotected operation can still execute twice;
- participant A acquires a lock on one PostgreSQL session;
- participant B fails immediately on another session;
- participant A executes the protected job and releases the lock;
- participant B can acquire the same lock after release;
- two orchestrated job runners persist one execution belonging to the lock owner.

The job-level test pauses Worker A at its first database save only after A has
entered the protected operation. Worker B then attempts the same logical job
before A is allowed to finish. Test synchronization uses explicit completion
signals, not delays or scheduler timing.

## Running the Example

From this directory, run:

```bash
docker compose down -v
docker compose up --build
```

This starts PostgreSQL, Worker A, and Worker B. Both Workers use `daily-report`
and execution key `2026-09-13`. During the competing attempt, the logs show one
Worker acquiring the distributed lock and the other skipping execution because
the lock is owned.

Worker A remains the non-production migration owner, while Compose no longer
waits for Worker A's job to finish before starting Worker B. No manual database
creation or migration command is required. Automatic migrations are disabled in
`Production`; production deployments require a controlled migration step.

While PostgreSQL is running, verify the durable result with the actual table and
column names:

```bash
docker compose exec postgres psql -U postgres -d distributed_lock -c 'SELECT "JobName", "ExecutionKey", "WorkerInstance" FROM "JobExecutions" ORDER BY "WorkerInstance";'
```

The competing logical job has one persisted execution:

```text
   JobName    | ExecutionKey | WorkerInstance
--------------+--------------+----------------
 daily-report | 2026-09-13   | worker-a or worker-b
```
