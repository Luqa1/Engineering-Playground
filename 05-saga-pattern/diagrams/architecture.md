# Architecture

One API application, one database, and independent local commits. Arrows show runtime calls, not project dependencies.

```mermaid
flowchart TD
    Client --> Controller[ASP.NET Core OrdersController]
    Controller --> Saga[OrderSaga]
    Saga --> Operations[OrderOperations: order, inventory, payment]
    Saga --> State[OrderSagaStateService]
    Operations --> DB[(PostgreSQL: independent business transactions)]
    State --> DB
    Client --> Reads[OrdersController / InventoryController: read DTOs]
    Reads --> DB
```

Domain owns entities and business rules. Infrastructure owns the orchestrator, operations, EF Core context, mappings, and migrations. API owns HTTP DTOs/controllers and applies migrations only outside Production. No broker or background Worker exists.
