# Outbox Pattern

## Goal

Demonstrate how the Outbox Pattern supports reliable integration event publishing.

---

## M1 – Repository Skeleton

**Status: Completed**

### Goal

Prepare the repository structure and initial documentation.

### Expected outcome

- Repository structure exists
- Documentation placeholders exist

---

## M2 – Running Application

**Status: Completed**

### Goal

Create a minimal runnable application.

### Expected outcome

- .NET solution
- API
- Background Worker
- PostgreSQL
- RabbitMQ
- Docker Compose

---

## M3 – Business Domain

**Status: Completed**

### Goal

Implement a minimal business scenario.

### Expected outcome

- Sample domain
- Entity Framework Core
- Initial database migration

---

## M4 – Transactional Outbox

**Status: Completed**

### Goal

Persist business data and integration events atomically.

### Expected outcome

- Outbox table
- Transactional save
- Integration event model

---

## M5 – Outbox Processor

**Status: Completed**

### Goal

Publish pending integration events asynchronously.

### Expected outcome

- Background Worker
- Event publishing
- Processed message tracking

---

## M6 – Failure Scenarios

**Status: Completed**

### Goal

Demonstrate why the Outbox Pattern exists.

### Expected outcome

- Broker unavailable scenario
- Worker restart scenario
- Duplicate delivery scenario
- Retry scenario

---

## M7 – Documentation

**Status: Completed**

### Goal

Complete the documentation.

### Expected outcome

- Architecture diagram
- Sequence diagram
- Final README
