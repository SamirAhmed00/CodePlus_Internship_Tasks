# OrderFlow

Educational e-commerce backend demonstrating the evolution from plain CRUD into
**CQRS + Redis caching + a Materialized read table + background processing**.

Spec source: `OrderFlow_Requirements.pdf` (FR-01 … FR-10).

## Stack

- .NET 10 (intentional deviation from the PDF's ".NET 8" — .NET 10 is what this machine runs)
- EF Core 10 / SQL Server (transactional source of truth)
- Redis via `IDistributedCache` (cache only — never authoritative)
- MediatR 12.5.0 (CQRS dispatching)
- Clean Architecture: Domain → Application → Infrastructure → API

## Structure

```
src/
├── OrderFlow.Domain/            entities, enum, domain rules, read model (no dependencies)
├── OrderFlow.Application/       vertical slices (CQRS via MediatR), abstractions only
├── OrderFlow.Infrastructure/    EF Core (SQL Server), Redis cache, BackgroundService
└── OrderFlow.Api/               thin controllers + Swagger
```

## Endpoints

| Method | Route | CQRS kind | Notes |
|---|---|---|---|
| POST | `/api/orders` | Command | 201 Created + Location; 400 on rule violation |
| GET | `/api/orders/{id}` | Query + **Redis cache-aside** | 200 / 404 |
| GET | `/api/orders` | Query | newest first, cap 100 |
| GET | `/api/dashboard/orders` | Query | reads **only** the `OrderDashboard` read table |

## The "Materialized View" (FR-10)

Implemented as an ordinary EF Core-mapped SQL **table** (`OrderDashboard`) — exactly as the
spec defines it: *"a SQL read table populated from transactional data. It is a read model,
not the source of truth."* No native `CREATE VIEW`, no SQL Agent.

The single `OrderProcessingWorker : BackgroundService`:
1. creates a DI scope per cycle,
2. completes Pending orders through the domain (`Order.Complete()` — FR-08),
3. invalidates the Redis cache of those orders,
4. rebuilds `OrderDashboard` transactionally from `Orders` + `OrderItems` (FR-10),
5. waits for the configured interval and repeats.

## Configuration (`appsettings.json`, overridable by environment variables)

```json
"ConnectionStrings": {
  "OrderFlowDb": "Server=localhost;Database=OrderFlow;Trusted_Connection=True;TrustServerCertificate=True",
  "Redis": "localhost:6379"
},
"Redis":  { "InstanceName": "OrderFlow:" },
"Cache":  { "OrderDetailsDurationSeconds": 300 },
"Background": { "RefreshIntervalSeconds": 30 }
```

## Running

```bash
dotnet ef database update --project src/OrderFlow.Infrastructure --startup-project src/OrderFlow.Api
dotnet run --project src/OrderFlow.Api
```

Migrations are also applied automatically at startup. Swagger UI: `/swagger` (development).

**Redis requirement:** the app expects Redis at `localhost:6379`. When Redis is down the
`RedisOrderCache` implementation logs a warning and degrades gracefully to direct SQL reads
(Redis stays the only cache; there is deliberately no in-memory fallback). Cache keys:
`OrderFlow:order:{id}` with a 300 s absolute expiration by default.

## Classroom walkthrough notes

- Business rules live in the domain (`Order.Create`, `Order.AddItem`, `Order.Complete`) — controllers are 3-line adapters.
- Domain rule violations surface as HTTP 400 via `OrderDomainExceptionHandler` (translation only).
- The dashboard endpoint never touches the `Orders` table — that is the point of the read model.
- Cache invalidation after `Pending → Completed` happens inside the worker (the only state change that affects cached details).
- SQL Server survives a Redis restart (and vice versa) — proves which store is the source of truth.

## Out of scope (per spec §7)

Auth, payments, message brokers, microservices, event sourcing, sharding, inventory,
generic repositories, unit-of-work abstractions, CQRS frameworks.
