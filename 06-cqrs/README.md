# CQRS — Command Query Responsibility Segregation

**Status: Completed.** M1–M8 are complete; see [PLAN.md](PLAN.md).

## Overview

CQRS separates the responsibility for changing system state (commands) from the responsibility for retrieving information (queries). Different code models can serve these responsibilities while sharing a physical database.

This Product Catalog & Inventory Dashboard uses .NET 10, ASP.NET Core Controllers, EF Core, PostgreSQL, and Docker Compose. Concrete command handlers call Product domain behavior; concrete query handlers project dedicated read DTOs. One API and one PostgreSQL database serve both sides.

## The problem

The original catalog used the same `Product` model for reads and writes. This was a reasonable starting point for a small application. Each product has an ID, name, price, and available quantity.

Writes must update prices and adjust inventory, enforcing nonnegative prices and preventing negative inventory. The warehouse dashboard needs product counts, low-stock and out-of-stock counts, total inventory value, and sorted attention lists. These are retrieval and presentation requirements rather than new Product business behavior.

A shared model can still support both. Separation becomes useful when specialized read calculations and write rules make that shared code harder to maintain.

## Example scenario

A fresh database contains:

| ID | Name | Price | Available quantity |
|---|---|---:|---:|
| `11111111-1111-1111-1111-111111111111` | Mechanical Keyboard | 299.00 | 15 |
| `22222222-2222-2222-2222-222222222222` | Wireless Mouse | 129.00 | 30 |
| `33333333-3333-3333-3333-333333333333` | USB-C Dock | 449.00 | 8 |

Initially, none need attention and total inventory value is `11947.00`. Changing the dock price to `459.00` and adjusting its quantity by `-3` leaves five units. The next dashboard query includes the dock as `LowStock`, with inventory value `2295.00`, and returns total value `10650.00`. Executable requests and responses appear below.

## Evolution: shared model to CQRS

These stages explain this PoC's progression; they are not a required migration path for every application.

### Stage 1 — Shared model (M2)

```text
HTTP Controller → Product + DbContext → PostgreSQL
```

Reads and writes shared the Product model. M2 temporarily returned Product directly from reads as a milestone-specific exception. The final API returns DTOs.

### Stage 2 — Dashboard requirements (M3)

```text
Product business state → Dashboard calculations → Presentation response
```

The dashboard loaded Product entities and calculated its response in the controller. It worked, but whole-entity reads and presentation calculations gave the shared model and controller more responsibilities.

### Stage 3 — Command Side (M4)

```text
HTTP PUT → Command Handler → Product Domain → EF Core / PostgreSQL
```

Explicit commands made state-changing intent visible. Handlers coordinate loading and saving, Product enforces invariants, and controllers map requests and HTTP outcomes.

### Stage 4 — Query Side (M5–M7)

```text
HTTP GET → Query Handler → EF Core Projection → Read DTO
```

Read handlers project consumer-specific DTOs without materializing full Product entities. M6 made the separation explicit; M7 verified persistence, read behavior, and boundaries using real PostgreSQL. M8 completes the documentation.

## Architecture

See the [architecture diagram](diagrams/architecture.md) and [command-to-query sequence](diagrams/command-to-query.md).

| Project | Responsibility | Project dependencies |
|---|---|---|
| `EngineeringPlayground.Cqrs.Api` | Controllers, request/response DTOs, commands, query handlers, read models, dependency registration | Domain, Infrastructure |
| `EngineeringPlayground.Cqrs.Domain` | Product and business invariants | None |
| `EngineeringPlayground.Cqrs.Infrastructure` | `CqrsDbContext`, entity configuration, migrations | Domain |
| `EngineeringPlayground.Cqrs.IntegrationTests` | xUnit domain, startup, and PostgreSQL-backed HTTP verification | Api, Infrastructure |

Command and query handlers both live in the API project. Both use the same scoped `CqrsDbContext` type and persistence mapping from Infrastructure. There is one `Products` table, no separately persisted read model, and no Application project or Worker. Domain depends on no other project.

Both sides run in the same API process and deployment. There is no message broker or asynchronous projection synchronization.

## Command Side

| Command | Handler | Domain behavior |
|---|---|---|
| `UpdateProductPriceCommand(ProductId, Price)` | `UpdateProductPriceHandler` | `Product.UpdatePrice(price)` |
| `AdjustInventoryCommand(ProductId, Delta)` | `AdjustInventoryHandler` | `Product.AdjustInventory(delta)` |

Commands are plain records representing intent. For example, `PUT /products/{id}/inventory` supplies a signed `delta`. `ProductsController` constructs `AdjustInventoryCommand`; the handler loads a tracked Product, calls `AdjustInventory`, awaits `SaveChangesAsync`, logs the change, and returns `ProductResponse`. Positive deltas increase stock; negative deltas decrease it.

Product protects the rules before mutation:

- Names cannot be null, empty, or whitespace; construction trims them.
- Prices cannot be negative; zero is valid.
- Quantities cannot be negative; adjustments also reject results greater than `Int32.MaxValue`.

Handlers return null for unknown IDs, which the controller maps to 404. Domain range exceptions become 400 Problem Details. Required nullable request properties distinguish missing values from valid zeros; ASP.NET Core validates them before handler execution. Rejected operations do not save changes.

## Query Side

| Handler | Input | Read contract |
|---|---|---|
| `GetProductsHandler` | Cancellation token | List of `ProductListItem` |
| `GetProductDetailsHandler` | `GetProductDetailsQuery(ProductId)` | `ProductDetails`, or null for an unknown ID |
| `GetInventoryDashboardHandler` | Cancellation token | `InventoryDashboardResponse`, containing `InventoryAttentionItemResponse` items |

Only details needs a query record. Parameterless reads call concrete handlers directly; no generic bus or mediator is involved.

For example, `GetProductsHandler` executes:

```csharp
return _database.Products
    .AsNoTracking()
    .OrderBy(product => product.Name)
    .ThenBy(product => product.Id)
    .Select(product => new ProductListItem(
        product.Id, product.Name, product.Price, product.AvailableQuantity))
    .ToListAsync(cancellationToken);
```

`Select` shapes the database result into a read DTO without materializing Product entities. `AsNoTracking` expresses read-only intent. Product contains behavior and invariants; a read DTO contains consumer-specific data without domain behavior. Product list and details currently expose the same fields through separate contracts, allowing their code to evolve separately. Queries never call `SaveChanges` or modify business state.

### Dashboard calculations

| Stock classification | Available quantity |
|---|---|
| `OutOfStock` | `quantity == 0` |
| `LowStock` | `quantity >= 1 && quantity <= 5` |
| `InStock` | `quantity > 5` |

The handler executes five SQL queries sequentially on one scoped DbContext: total count, out-of-stock count, low-stock count, total value, and the attention projection. Counts and `SUM(Price * AvailableQuantity)` run in PostgreSQL. Each product's inventory value is `Price * AvailableQuantity`, using decimal arithmetic.

The attention query filters quantities at or below five in SQL and projects `id`, `name`, `availableQuantity`, `stockStatus`, and `inventoryValue`. It sorts these DTOs in memory by out-of-stock first, then name with `StringComparer.Ordinal`, then Product ID. This preserves deterministic UTF-16 ordinal ordering, including case and Unicode differences. The product list instead orders by name and ID in PostgreSQL, using database name ordering.

In-stock products contribute to totals but do not appear in the attention list. Empty catalogs return zero counts/value and an empty list. Classifications and calculated values are derived on reads, not added to Product or persisted. No measured performance improvement is claimed.

## Data flow and consistency

The [sequence diagram](diagrams/command-to-query.md) traces an update through domain validation and commit, followed by a GET projecting the saved data.

One PostgreSQL database, synchronous awaited EF Core writes, and direct database queries provide read-after-write behavior: after a successful committed write, a subsequent ordinary query observes the committed state under normal database transaction semantics. There is no asynchronous read-model synchronization, polling, or projection delay. CQRS does not inherently imply eventual consistency.

This does not provide a universal snapshot guarantee. The dashboard's five statements are not wrapped in a shared snapshot transaction; concurrent writes between statements can produce totals and attention items from different committed states. There is also no optimistic concurrency protection: concurrent read-modify-save inventory operations can overwrite each other's changes.

## Running locally

Requirements: Docker with Linux containers and Docker Compose. From `06-cqrs/`, run:

```bash
docker compose up --build
```

Compose starts PostgreSQL 17 (`postgres:17-alpine`) and the .NET 10 API. PostgreSQL creates the database; its healthcheck gates API startup. The API applies pending EF Core migrations and the initial deterministic seed before accepting requests. No manual database creation, migrations, seeding, or service ordering is required.

The default API address is [http://localhost:8086/products](http://localhost:8086/products). Compose maps host port **8086** to API container port **8080**. PostgreSQL uses port **5432** internally and is not published on the host. The API's standalone launch profile uses port 5164 and needs its own connection string; the examples here use Compose.

Compose includes working local defaults. Optionally copy [.env.example](.env.example) to `.env` to override them:

| Variable | Default | Purpose |
|---|---|---|
| `API_PORT` | `8086` | Published API port |
| `POSTGRES_DB` | `cqrs` | Database created by PostgreSQL |
| `POSTGRES_USER` | `cqrs` | Local database user |
| `POSTGRES_PASSWORD` | Local example value in `.env.example` | Local database password |

The example credentials are development-only values. Compose sets `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_HTTP_PORTS=8080`, and `ConnectionStrings__Cqrs`. The local connection disables GSS encryption negotiation because this password-authenticated setup does not use Kerberos. PostgreSQL data persists in the `postgres-data` named volume.

```bash
docker compose down
# Delete this PoC's local database and reproduce the fresh-seed examples:
docker compose down -v
docker compose up --build
```

### Persistence and migrations

Infrastructure contains `ProductConfiguration : IEntityTypeConfiguration<Product>` and the generated `InitialCreate` migration. Mapping defines a UUID primary key, required text name, `numeric(18,2)` price, and integer quantity. Use prices with at most two decimal places; PostgreSQL stores them at that scale.

The initial migration inserts the three products above through EF Core `HasData`. Migration history prevents subsequent starts from duplicating seeds or resetting edits. `EnsureCreated()` is not used.

Only the API applies migrations, and only outside **Production**. Production requires reviewed, controlled migration execution before API startup and a `ConnectionStrings__Cqrs` value for its database. There is no production migration runner in this PoC.

## API examples

| Method | Route | Successful response |
|---|---|---|
| GET | `/products` | 200, array of `ProductListItem`, ordered by name then ID |
| GET | `/products/{id}` | 200, `ProductDetails` |
| GET | `/inventory/dashboard` | 200, `InventoryDashboardResponse` |
| PUT | `/products/{id}/price` | 200, `ProductResponse` |
| PUT | `/products/{id}/inventory` | 200, `ProductResponse` |

Unknown product IDs return 404 for details and both writes. Routes constrain IDs to GUIDs. Missing or invalid request input returns 400; domain rejections use Problem Details. Writes return the updated product rather than 204.

The examples use Bash quoting and default Compose ports. In Windows PowerShell, use `curl.exe` and shell-appropriate JSON quoting, or the API project's [HTTP request file](src/EngineeringPlayground.Cqrs.Api/EngineeringPlayground.Cqrs.Api.http).

### List products and choose an ID

```bash
curl http://localhost:8086/products
```

A fresh database returns:

```json
[
  { "id": "11111111-1111-1111-1111-111111111111", "name": "Mechanical Keyboard", "price": 299.00, "availableQuantity": 15 },
  { "id": "33333333-3333-3333-3333-333333333333", "name": "USB-C Dock", "price": 449.00, "availableQuantity": 8 },
  { "id": "22222222-2222-2222-2222-222222222222", "name": "Wireless Mouse", "price": 129.00, "availableQuantity": 30 }
]
```

Copy an `id` from this response for details and writes. Remaining examples use the dock's seeded ID and assume a fresh database, executed in order.

```bash
curl http://localhost:8086/products/33333333-3333-3333-3333-333333333333
```

Details return an object with `id`, `name`, `price`, and `availableQuantity`, matching the corresponding list item. These are also the fields in both write responses.

### Read the initial dashboard

```bash
curl http://localhost:8086/inventory/dashboard
```

```json
{
  "totalProducts": 3,
  "outOfStockCount": 0,
  "lowStockCount": 0,
  "totalInventoryValue": 11947.00,
  "productsNeedingAttention": []
}
```

### Update price and inventory

```bash
curl -X PUT http://localhost:8086/products/33333333-3333-3333-3333-333333333333/price \
  -H "Content-Type: application/json" -d '{"price":459.00}'
```

```json
{
  "id": "33333333-3333-3333-3333-333333333333",
  "name": "USB-C Dock",
  "price": 459.00,
  "availableQuantity": 8
}
```

```bash
curl -X PUT http://localhost:8086/products/33333333-3333-3333-3333-333333333333/inventory \
  -H "Content-Type: application/json" -d '{"delta":-3}'
```

This returns the same product shape with `price: 459.00` and `availableQuantity: 5`. A subsequent details GET returns those persisted values. Then query the dashboard:

```bash
curl http://localhost:8086/inventory/dashboard
```

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

The total is `299.00 * 15 + 129.00 * 30 + 459.00 * 5 = 10650.00`. JSON numbers may omit trailing zeros.

### Reject an invalid adjustment

With five docks available, subtracting six would make inventory negative:

```bash
curl -i -X PUT http://localhost:8086/products/33333333-3333-3333-3333-333333333333/inventory \
  -H "Content-Type: application/json" -d '{"delta":-6}'
```

The response is **400 Bad Request**, `application/problem+json`. Stable Problem Details fields include:

```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "The resulting inventory quantity must be between zero and Int32.MaxValue. (Parameter 'delta')"
}
```

The response also includes framework metadata such as `type` and a request-specific `traceId`. A subsequent details GET still returns quantity 5 and price 459.00. Sending `{}` to either write route instead triggers required-field validation and a 400 validation response.

## Integration tests

Install the .NET 10 SDK and start Docker. From `06-cqrs/`:

```bash
dotnet test
```

xUnit tests use `WebApplicationFactory` and Testcontainers with real PostgreSQL 17, without an EF Core InMemory provider. Compose need not be running. The shared `ProductsTests` fixture starts its own container; each test creates a unique database, and non-Production API startup migrates and seeds it. Tests dispose HTTP hosts, clear the relevant connection pool, drop their databases, and dispose the fixture container. Tests do not depend on order or alter the Compose database.

Coverage includes:

- Command persistence, full write DTOs, positive/negative adjustments, zero boundaries, rejected negative values and overflow, missing fields, and unknown IDs.
- Domain construction invariants, repeatable migrations without resetting seeds, and Production startup without a migration connection.
- Product list/details contracts, name/ID ordering, dashboard totals, classification boundaries at 0, 1, 5, and 6, zero-price items, and empty catalogs.
- Attention membership, calculated values, and ordering with same-name ID ties, case differences, and Unicode names.
- Details and dashboard reads immediately after price and inventory writes, plus fresh-context persistence verification.
- Query boundaries: test-only EF interceptors reject Product entity materialization and synchronous/asynchronous `SaveChanges` during all three query paths.

For the full validation workflow:

```bash
dotnet format
dotnet build
dotnet test
docker compose config
```

## Trade-offs

Commands make state-changing intent explicit, Product protects write invariants, and read DTOs reflect consumer needs. Read and write code can evolve separately within the application. Concrete handlers and direct DbContext access keep the example understandable without repositories or a dispatch framework.

The cost is additional commands, handlers, queries, and DTOs. IDs, names, prices, and quantities appear in several representations; changes can require coordinated edits. Developers must follow more classes, and projections still depend on persistence mappings and provider translation. For a small CRUD application, these maintenance costs may outweigh the benefits. Code separation here does not provide independent deployment or scaling.

### CQRS versus query optimization

Efficient EF Core projections do not require CQRS. A traditional CRUD application can use `AsNoTracking()`, `Select()`, SQL filtering, and database-side aggregation too. Query design provides these optimizations; CQRS separates responsibilities. This PoC replaces the M3 whole-entity dashboard calculation with aggregates and filtered DTO projections, without claiming benchmark-based speedups.

Five straightforward dashboard queries keep the code simple at the cost of round trips and the snapshot limitation described above. Exact ordinal attention ordering requires an in-memory sort of the filtered DTOs.

## When to use CQRS

Consider it when write-side invariants grow complex, read consumers need specialized models, read and write requirements evolve differently, or a shared model becomes hard to maintain. In this catalog, price and inventory rules belong to Product, while an operator needs classifications, totals, and attention lists. Explicit boundaries can reduce coupling when that distinction matters enough to justify extra code.

## When not to use CQRS

Simple administrative CRUD applications, small models, nearly identical read/write representations, and limited business rules often benefit more from a shared approach. Dedicated handlers and DTOs should earn their maintenance cost. CQRS is not a universal default, and query projections remain useful without it.

## What CQRS does not require

MediatR, Event Sourcing, microservices, separate databases, message brokers, eventual consistency, asynchronous projections, and separate deployments are independent architectural choices. None is required for CQRS, and none is used here.

## Limitations

- One database and DbContext type; no separate read/write stores or independently deployed sides.
- No asynchronous read-model projections, Event Sourcing, message broker, background worker, or caching.
- No optimistic concurrency handling or protection against lost concurrent inventory updates.
- No single-snapshot dashboard under concurrent writes; product and attention lists are unbounded.
- No benchmark-based performance comparison.

These are intentional scope limits. This example is unsuitable as-is for concurrent stock allocation or a dashboard requiring a single consistent snapshot.

## Key takeaways

1. CQRS separates read and write responsibilities; separate databases are optional.
2. Domain behavior protects write-side invariants, while read DTOs serve consumer-specific needs.
3. Query optimization works with or without CQRS.
4. Introduce the separation when its clarity and maintainability justify the added code.
