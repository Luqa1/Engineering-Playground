# Command-to-query flow

One successful update followed by a separate read request:

```mermaid
sequenceDiagram
    participant Client
    participant Controller as ASP.NET Core Controller
    participant Command as Concrete Command Handler
    participant Product as Product Domain
    participant EF as CqrsDbContext / EF Core
    participant DB as PostgreSQL
    participant Query as Concrete Query Handler

    Client->>Controller: PUT /products/{id}/price or /inventory
    Controller->>Command: UpdateProductPriceCommand or AdjustInventoryCommand
    Command->>EF: Load tracked Product by ID
    EF->>DB: SELECT Product
    DB-->>EF: Product row
    EF-->>Command: Product entity
    Command->>Product: UpdatePrice(price) or AdjustInventory(delta)
    Note over Product: Enforce business invariants before mutation
    Product-->>Command: Valid state change
    Command->>EF: await SaveChangesAsync
    EF->>DB: UPDATE and commit
    DB-->>EF: Write committed
    EF-->>Command: Save completed
    Command-->>Controller: ProductResponse
    Controller-->>Client: 200 + product DTO

    Client->>Controller: GET /products/{id} or /inventory/dashboard
    Controller->>Query: HandleAsync
    Query->>EF: AsNoTracking + DTO projection / aggregates
    EF->>DB: SELECT committed state
    DB-->>EF: Projected values
    EF-->>Query: DTOs / scalar totals
    Note over Query: Dashboard sorts filtered attention DTOs in memory
    Query-->>Controller: ProductDetails or InventoryDashboardResponse
    Controller-->>Client: 200 + read DTO
```

The command awaits the save before returning success. A subsequent ordinary query reads committed data from the same database under normal transaction semantics; no asynchronous projection update is needed. The dashboard executes five sequential SQL statements, so concurrent writes between statements can affect its internal consistency. Unknown products return 404; invalid command input returns 400 without saving.
