# Successful Flow

Each database arrow marked COMMIT is an independent transaction.

```mermaid
sequenceDiagram
    participant Client
    participant API as OrdersController
    participant Saga as OrderSaga
    participant DB as PostgreSQL
    Client->>API: POST /orders (default modes)
    API->>Saga: Process order
    Saga->>DB: Create Pending order / COMMIT
    Saga->>DB: Create Running Saga / COMMIT
    Saga->>DB: Reserve inventory / COMMIT
    Saga->>DB: Record Succeeded payment / COMMIT
    Saga->>DB: Complete order / COMMIT
    Saga->>DB: Save Completed Saga / COMMIT
    Saga-->>API: Order ID
    API->>DB: Read persisted outcome
    API-->>Client: 201: Completed / Succeeded / Completed
    Note over Saga,DB: Reservation remains; no compensation or fulfillment step
```
