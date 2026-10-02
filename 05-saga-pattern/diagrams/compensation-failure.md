# Compensation Failure

The deterministic refusal changes no stock and stops processing before order cancellation.

```mermaid
sequenceDiagram
    participant Client
    participant API as OrdersController
    participant Saga as OrderSaga
    participant DB as PostgreSQL
    Client->>API: POST /orders (both modes: Fail)
    API->>Saga: Process order
    Saga->>DB: Create Pending order / COMMIT
    Saga->>DB: Create Running Saga / COMMIT
    Saga->>DB: Reserve inventory / COMMIT
    Saga->>DB: Record Failed payment / COMMIT
    Saga->>DB: Save Compensating Saga / COMMIT
    Saga->>DB: Attempt release: load inventory
    Note over Saga,DB: Refused before mutation; no release commit
    Saga->>DB: Save CompensationFailed Saga / COMMIT
    Saga-->>API: Order ID
    API->>DB: Read persisted outcome
    API-->>Client: 422: Pending / Failed / CompensationFailed
    Note over Saga,DB: Unresolved: inventory reserved, order Pending, no automatic recovery
```
