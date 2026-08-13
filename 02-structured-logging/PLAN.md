# Structured Logging & Observability

## Goal

Demonstrate how structured, contextual and centralized logs make production troubleshooting significantly easier than plain-text logging.

The PoC should focus on the engineering problem and diagnostic workflow, not merely on configuring a logging library.

---

## M1 – Repository Skeleton

Goal:

Prepare the PoC structure and implementation plan.

Expected outcome:

- directory structure;
- initial README;
- PLAN;
- configuration placeholders.

---

## M2 – Running Application

Status: Completed

Goal:

Create a small .NET 10 application that provides a realistic flow suitable for demonstrating logging.

Expected outcome:

- .NET solution;
- ASP.NET Core API using Controllers;
- small domain;
- PostgreSQL if required by the scenario;
- Docker Compose;
- one-command local startup according to `AGENTS.md`.

Keep the business scenario intentionally small.

---

## M3 – Structured Logging

Status: Completed

Goal:

Replace unstructured diagnostic output with meaningful structured application logs.

Expected outcome:

- Serilog;
- structured properties;
- appropriate log levels;
- meaningful event messages;
- console JSON output suitable for centralized ingestion.

Logs should capture useful domain and technical context without logging sensitive data.

---

## M4 – Correlation & Context

Status: Completed

Goal:

Make all logs belonging to the same request easy to identify and query.

Expected outcome:

- Correlation ID;
- request context enrichment;
- consistent contextual properties;
- correlation propagated through the application flow;
- HTTP response exposes or preserves the correlation identifier where appropriate.

Avoid unnecessary tracing infrastructure.

---

## M5 – Centralized Logging

Status: Completed

Goal:

Make application logs searchable outside individual containers.

Expected outcome:

- Loki;
- Grafana;
- Docker Compose integration;
- log ingestion;
- searchable structured properties;
- useful example queries.

The entire environment must remain runnable with one Docker Compose command.

---

## M6 – Diagnostic Scenario

Status: Completed

Goal:

Demonstrate the practical difference between having logs and having useful structured logs.

Create a realistic failure scenario where an engineer needs to determine:

- which request failed;
- which operation was being performed;
- which relevant identifiers were involved;
- where the failure occurred;
- what other logs belong to the same request.

The scenario should demonstrate querying logs by structured properties and Correlation ID.

Do not introduce artificial complexity solely to generate more logs.

---

## M7 – Documentation

Goal:

Turn the completed PoC into a concise engineering knowledge-base example.

Expected outcome:

- final README;
- architecture diagram;
- sequence diagram if useful;
- screenshots from Grafana;
- example log queries;
- documented diagnostic walkthrough;
- trade-offs;
- when to use;
- when not to use;
- production considerations.

---

## Final Review

Perform a focused engineering review after M7.

Review:

- simplicity;
- correctness;
- logging quality;
- consistency;
- educational value;
- documentation accuracy.

Do not add functionality during Final Review.

## Technology direction

- .NET 10;
- ASP.NET Core Controllers;
- Serilog;
- Loki;
- Grafana;
- Docker Compose.

Do not add OpenTelemetry in this PoC.

OpenTelemetry, distributed tracing and metrics are related observability topics but are outside the primary scope. They may be mentioned as possible extensions.
