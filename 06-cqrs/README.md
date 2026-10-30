# CQRS — Product Catalog & Inventory

**Status: Work in Progress.** M1 through M4 are complete. **CQRS separation is not complete yet.** Writes now use explicit commands and handlers; reads still use the shared Product model. M5 and later milestones remain planned. See [PLAN.md](PLAN.md).

## The Problem

A small catalog tracks each product's ID, name, price, and available quantity. Users can list products, inspect one product, change its price, and adjust its inventory by a signed delta.

The warehouse operator also needs to know how many products exist, how many are out of stock or low on stock, the total inventory value, and which products need attention. These questions introduce counts, aggregations, calculated values, classifications, and sorting. They do not add write-side business behavior.

The write model protects business invariants, while the dashboard needs a read-optimized representation. CQRS allows these responsibilities to evolve independently when the complexity justifies the separation. It does not require separate databases or automatically improve performance: performance depends on the actual query design. The shared model still works correctly for this small catalog.

## Shared Model

Both reads and writes use the same `Product` entity and the same `CqrsDbContext`. Read actions still load entities directly through EF Core. Write request DTOs contain `price` or `delta`; writes return a `ProductResponse` DTO with the existing `id`, `name`, `price`, and `availableQuantity` JSON fields. Product reads still return the shared Product model as the intentional M2 exception to the repository's usual response DTO convention. The dashboard retains its small aggregate HTTP response contracts. These contracts are not a dedicated CQRS query model. There are no query handlers, query services, read repositories, or separate read contexts.

Domain methods enforce these rules:

- Names cannot be null, empty, or whitespace.
- Prices cannot be negative; zero is allowed.
- Inventory cannot be negative; zero is allowed. Adjustments also reject integer overflow.

`UpdatePrice` and `AdjustInventory` enforce the rules before mutation. Controllers handle HTTP concerns; command handlers coordinate writes and saving; business rules live in Domain. Handlers retain structured write logging through `ILogger<T>`.

`InventoryController` makes one `AsNoTracking().ToListAsync(cancellationToken)` call to load persisted Product entities, then calculates the dashboard in application memory:

```text
Load Product entities
      ↓
Calculate dashboard values
      ↓
Return dashboard response
```

The fixed dashboard classifications are mutually exclusive for valid product quantities:

| StockStatus | AvailableQuantity |
|---|---|
| `OutOfStock` | 0 |
| `LowStock` | 1 through 5 inclusive |
| `InStock` | Greater than 5 |

Each inventory value is `Price * AvailableQuantity`, using decimal arithmetic. The total sums these values across all products, including those in stock. Attention items include only out-of-stock and low-stock products, ordered by status (out of stock first), name ascending using ordinal comparison, then Product ID ascending for ties. `InStock` products contribute to the totals but do not appear in the attention list. Empty catalogs return zero counts/value and an empty list. Stock status, inventory value, and attention flags are not added to Product or persisted; no migration is needed.

## Command Side

A command represents an intention to change business state. `UpdateProductPriceCommand` carries a Product ID and the new price; `AdjustInventoryCommand` carries a Product ID and a signed quantity delta. Both are plain records independent of HTTP and EF Core types.

`ProductsController` constructs a command and calls its concrete `UpdateProductPriceHandler` or `AdjustInventoryHandler`, registered with scoped constructor injection. Each handler loads the Product through `CqrsDbContext`, invokes its domain method, calls `SaveChangesAsync`, and returns a small response DTO. A null result means the Product was not found. Cancellation flows from the controller through the handler to EF Core.

Controllers no longer coordinate write persistence. They map the handler result to the existing 200 response or 404, and domain validation exceptions to the existing 400 Problem Details. Missing request fields still receive 400 through request validation. Invariants remain in Product so every caller uses the same business rules; handlers do not duplicate them. Invalid domain operations fail before mutation or saving.

The current paths are deliberately asymmetric:

```text
Commands:
HTTP → Controller → Handler → Product → EF Core / PostgreSQL

Queries:
HTTP → Controller → Product/DbContext → Response
```

M4 separates write coordination only. Read actions and dashboard calculations remain unchanged so this refactoring can be assessed independently. The Query Side will be separated in M5; CQRS is not fully implemented. There is no dispatcher, handler interface, repository, or additional project.

## Solution and architecture

```text
EngineeringPlayground.Cqrs.sln
src/
  EngineeringPlayground.Cqrs.Api/             Controllers, commands/handlers, HTTP DTOs, startup
  EngineeringPlayground.Cqrs.Domain/          Product and its invariants
  EngineeringPlayground.Cqrs.Infrastructure/  DbContext, EF mapping, migrations
tests/
  EngineeringPlayground.Cqrs.IntegrationTests/ PostgreSQL HTTP tests and domain checks
```

Project references: API → Domain + Infrastructure; Infrastructure → Domain; IntegrationTests → API + Infrastructure. Domain has no project dependencies. There is no Application project or Worker.

```mermaid
flowchart LR
    Client --> API[API: ProductsController and InventoryController]
    API -->|Writes| Handlers[API: concrete command handlers]
    Handlers --> Domain[Domain: shared Product model]
    Handlers --> Infrastructure[Infrastructure: CqrsDbContext and mappings]
    API -->|Reads unchanged| Infrastructure
    Infrastructure --> Domain
    Infrastructure --> PostgreSQL[(PostgreSQL)]
```

```mermaid
sequenceDiagram
    participant Client
    participant Controller as ProductsController
    participant Handler as AdjustInventoryHandler
    participant Context as CqrsDbContext
    participant Product
    participant DB as PostgreSQL
    Client->>Controller: PUT /products/{id}/inventory {delta: -3}
    Controller->>Handler: HandleAsync(AdjustInventoryCommand, cancellationToken)
    Handler->>Context: Load Product
    Context->>DB: SELECT product
    DB-->>Context: Product row
    Context-->>Handler: Shared Product entity
    Handler->>Product: AdjustInventory(-3)
    alt Resulting quantity is valid
        Product-->>Handler: Updated quantity
        Handler->>Context: SaveChangesAsync
        Context->>DB: UPDATE product
        DB-->>Context: Saved
        Context-->>Handler: Changes saved
        Handler-->>Controller: ProductResponse
        Controller-->>Client: 200 + unchanged product JSON
    else Negative quantity or integer overflow
        Product-->>Handler: Reject adjustment
        Handler-->>Controller: ArgumentOutOfRangeException
        Controller-->>Client: 400 Problem Details
    end
```

For an unknown product the handler returns null, and the controller returns 404 before any domain operation. Product reads use the same context and entity, returning Product directly.

The dashboard uses the same persistence path with an aggregate HTTP response:

```mermaid
sequenceDiagram
    participant Client
    participant Controller as InventoryController
    participant Context as CqrsDbContext
    participant DB as PostgreSQL
    Client->>Controller: GET /inventory/dashboard
    Controller->>Context: Products.AsNoTracking().ToListAsync()
    Context->>DB: SELECT products
    DB-->>Context: Product rows
    Context-->>Controller: Shared Product entities
    Note over Controller: Calculate counts, decimal values, classifications; sort attention items
    Controller-->>Client: 200 + InventoryDashboardResponse
```

## Run locally

Requirements: Docker with Linux containers and Docker Compose. From `06-cqrs/`:

```bash
docker compose up --build
```

This starts PostgreSQL 17 and the .NET 10 API at [http://localhost:8086/products](http://localhost:8086/products). PostgreSQL creates the database, its healthcheck gates API startup, and the API applies pending EF Core migrations before accepting requests. No manual database creation, migration command, or service sequencing is required.

Compose includes working local defaults. Optionally copy `.env.example` to `.env` to change `API_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, or `POSTGRES_PASSWORD`. Credentials in the example are for local development. The database is internal to Compose; its data persists in a named volume. The local connection disables GSS encryption negotiation because this password-authenticated setup does not use Kerberos.

```bash
docker compose down
# To delete this PoC's local data and start fresh:
docker compose down -v
docker compose up --build
```

## Persistence, migrations, and seed

Infrastructure contains `CqrsDbContext`, `ProductConfiguration : IEntityTypeConfiguration<Product>`, and the generated `InitialCreate` migration. Mapping defines the primary key, required name, `numeric(18,2)` price, and integer quantity. Use currency values with at most two decimal places; PostgreSQL stores prices at that scale.

The initial migration inserts deterministic data through EF Core `HasData`:

| ID | Name | Price | AvailableQuantity |
|---|---|---:|---:|
| `11111111-1111-1111-1111-111111111111` | Mechanical Keyboard | 299.00 | 15 |
| `22222222-2222-2222-2222-222222222222` | Wireless Mouse | 129.00 | 30 |
| `33333333-3333-3333-3333-333333333333` | USB-C Dock | 449.00 | 8 |

Migration history prevents repeated startup from inserting duplicates or resetting edited values. `EnsureCreated()` is not used.

Only the API runs migrations, and only when the environment is **not Production**. Compose sets `ASPNETCORE_ENVIRONMENT=Development`. **Production never migrates automatically**: apply reviewed EF Core migrations through a controlled deployment process before starting the API. Configure `ConnectionStrings__Cqrs` for the target database. This PoC does not add a production migration runner.

## API

| Method | Endpoint | Behavior |
|---|---|---|
| GET | `/products` | 200, all products ordered by name |
| GET | `/products/{id}` | 200, one product; 404 if unknown |
| PUT | `/products/{id}/price` | `{ "price": 319.00 }`; 200 with updated product JSON (`ProductResponse`) |
| PUT | `/products/{id}/inventory` | `{ "delta": -3 }`; 200 with updated product JSON (`ProductResponse`) |
| GET | `/inventory/dashboard` | 200, counts, total inventory value, sorted attention items |

Both writes return 404 for an unknown product. Invalid or missing input returns 400; a rejected domain operation leaves persisted state unchanged. Positive deltas increase inventory and negative deltas decrease it.

```bash
curl http://localhost:8086/products
curl http://localhost:8086/products/11111111-1111-1111-1111-111111111111
curl -X PUT http://localhost:8086/products/11111111-1111-1111-1111-111111111111/price \
  -H "Content-Type: application/json" -d '{"price":319.00}'
curl -X PUT http://localhost:8086/products/11111111-1111-1111-1111-111111111111/inventory \
  -H "Content-Type: application/json" -d '{"delta":-3}'
```

The API project's `.http` file contains equivalent requests for an HTTP editor.

## Example Scenario

With a fresh database, the seeded products have quantities 15, 30, and 8, so none need attention. This request:

```bash
curl http://localhost:8086/inventory/dashboard
```

returns:

```json
{
  "totalProducts": 3,
  "outOfStockCount": 0,
  "lowStockCount": 0,
  "totalInventoryValue": 11947.00,
  "productsNeedingAttention": []
}
```

Starting from that seed, update the USB-C Dock price and reduce its inventory to the low-stock boundary:

```bash
curl -X PUT http://localhost:8086/products/33333333-3333-3333-3333-333333333333/price \
  -H "Content-Type: application/json" -d '{"price":459.00}'
curl -X PUT http://localhost:8086/products/33333333-3333-3333-3333-333333333333/inventory \
  -H "Content-Type: application/json" -d '{"delta":-3}'
curl http://localhost:8086/inventory/dashboard
```

The dashboard reads the persisted changes immediately:

```json
{
  "totalProducts": 3,
  "outOfStockCount": 0,
  "lowStockCount": 1,
  "totalInventoryValue": 10650.00,
  "productsNeedingAttention": [
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "name": "USB-C Dock",
      "availableQuantity": 5,
      "stockStatus": "LowStock",
      "inventoryValue": 2295.00
    }
  ]
}
```

The total is `299.00 * 15 + 129.00 * 30 + 459.00 * 5 = 10650.00`. A further inventory adjustment of `-5` makes the dock `OutOfStock` with value zero. The existing domain methods protect valid price and quantity; the dashboard derives its presentation from that same state. JSON numbers may omit trailing zeros.

## Tests and validation

Install the .NET 10 SDK and start Docker. From `06-cqrs/`:

```bash
dotnet format
dotnet build
dotnet test
docker compose config
```

xUnit integration tests use `WebApplicationFactory` and Testcontainers with real PostgreSQL 17. Testcontainers creates and removes its own PostgreSQL container; Compose need not be running. Each test gets a new database, automatically migrated and seeded by API startup. Test data is deterministic and tests do not depend on order. Tests drop their databases, and the fixture disposes its container.

Coverage includes seeded reads, a single-product read, price persistence, positive/negative inventory adjustments, zero boundaries, rejected negative prices/inventory, overflow, missing request fields, unknown products, and repeatable migrations. The existing HTTP write tests now exercise command handlers and verify all product response fields and validation Problem Details. Persistence assertions use a fresh DbContext. Additional checks cover invalid initial names, prices, and quantities, and Production startup without a database connection for migrations. No EF Core InMemory provider is used.

Dashboard tests replace the seeded products inside each test's own database. Actual HTTP requests verify explicit decimal totals at quantities 0, 1, 5, and 6; a zero-price item; attention membership, classifications, values, and ordering including same-name ID ties; price/inventory writes followed by fresh dashboard reads in all three stock categories; and an empty catalog. Existing M2 tests retain their seeded setup and assertions.

## Trade-offs and limits

The shared model keeps this application small and easy to trace, and loading Product entities for this dashboard is acceptable for a small catalog. Concrete commands and handlers add a little code in exchange for clear write intent and a place to coordinate persistence independently of HTTP. Direct DbContext access still couples read actions to persistence, and returning Product from product reads couples their response shapes to the domain entity. Write handlers also use the context directly because EF Core already provides the persistence coordination these single-save operations need.

The dashboard makes a concrete read/write tension visible as the catalog grows:

- Full Product entities are loaded even though the dashboard does not need write behavior or every field for every result.
- Aggregation work and entity materialization consume application memory across the entire catalog.
- Read-specific calculations and sorting live alongside HTTP/application concerns.
- The read path remains coupled to the shared Product persistence model.

A dedicated query model with database projections could retrieve and aggregate just the required data, and separate those read concerns from writes. It is future work in M5. The current implementation remains correct; CQRS is justified only when the complexity warrants the additional code, and query design determines performance.

Concurrency control is outside this PoC. Concurrent read-modify-save inventory adjustments can overwrite one another; this implementation is not safe against all concurrent inventory changes. There are no concurrency tokens, locks, reservations, retries, caching, background workers, messaging, or separate databases.

## When to use this baseline

Use a shared model for a small catalog with straightforward reads and writes, where separating responsibilities would add code without clear benefit.

Use this command-side step when write intentions and coordination benefit from being explicit while reads can still use the existing model.

## When not to use this baseline

Reconsider the shared response/entity shape when presentation queries diverge from business behavior or whole-catalog materialization becomes costly. Separate handlers may add little value for trivial CRUD with no coordination needs. Do not use this baseline as a stock reservation system requiring correct concurrent allocation. Query-side separation and read projections remain future work in the plan.
