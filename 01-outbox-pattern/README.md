# Outbox Pattern

A Proof of Concept demonstrating the Outbox Pattern.

**Work in Progress**

## Problem

## Why this pattern exists

## Architecture

## How it works

## Trade-offs

## When to use

## When not to use

## Production retry scheduling

The current Worker polls at a fixed interval. During each polling cycle, any unprocessed Outbox message below `MaxRetriesCount` is eligible for publication. A message that reaches the limit remains unprocessed so it can be investigated and reprocessed manually.

Production systems commonly delay subsequent attempts instead of selecting every failed message on every polling cycle. One extension is to add a nullable `NextAttemptAtUtc` column to `OutboxMessage` and select a message only when that value is `NULL` or less than or equal to the current UTC time. After a failure, the Worker would calculate the next attempt from configurable delays—for example, 1, 2, 5, 8, and 13 minutes—or another configurable backoff policy. Automatic processing would still stop at `MaxRetriesCount`, requiring manual intervention.

This scheduling mechanism is intentionally outside this PoC. The example focuses on the Outbox Pattern itself; retry scheduling adds configuration, persistence, and time-based logic that would distract from that educational objective. More advanced implementations may also add exponential backoff, jitter, dead-letter handling, and operational tooling for manual replay.

## Running the example
