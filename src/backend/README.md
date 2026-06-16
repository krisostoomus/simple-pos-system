# Backend

ASP.NET Core (.NET 10) REST API + SignalR hub over PostgreSQL, in lightweight Clean Architecture.

## Projects

| Project | Responsibility | Depends on |
|---|---|---|
| `Pos.Domain` | Entities (`Product`, `Order`, `OrderLine`), value objects, `ChangeCalculator`, domain exceptions. Pure — no external dependencies. | — |
| `Pos.Application` | Use-case services (`CheckoutService`, `CatalogService`, `ReportingService`), port interfaces, DTOs, application exceptions. | Domain |
| `Pos.Infrastructure` | EF Core 10 + Npgsql (DbContext, configs, repositories, `UnitOfWork`), `CashPaymentService`, `ReportQueries`, config seeder, SignalR notifier, JWT `TokenService`, DI. | Application, Domain |
| `Pos.Api` | Minimal API endpoints, API versioning (`/api/v1`), JWT auth, OpenAPI/Swagger, `ProblemDetails` exception handler, CORS, health, landing page, startup migrate+seed. | Infrastructure, Application, Domain |

Dependencies point inward (Api → Application → Domain); Infrastructure implements Application's
ports and is wired only at the composition root (`AddInfrastructure`).

## Key decisions

- **Money is integer cents** end to end. `ChangeCalculator` is a pure greedy function over euro denominations.
- **Optimistic concurrency** via the Postgres `xmin` system column; `UnitOfWork` translates EF's
  `DbUpdateConcurrencyException` to a domain-neutral `ConcurrencyConflictException`, and a unique
  `IdempotencyKey` violation to `DuplicateIdempotencyKeyException` (→ idempotent replay).
- **Errors** are RFC 9457 `ProblemDetails` carrying a language-neutral `errorCode`
  (`out_of_stock`, `insufficient_payment`, `empty_cart`, `invalid_quantity`, `unknown_product`,
  `concurrency_conflict`, `not_found`) so the client localizes messages itself.
- **Order lines snapshot** product name + unit price for historical accuracy.

## Endpoints

`GET /api/v1/products` · `GET /api/v1/products/{id}` · `PUT /api/v1/products/{id}/stock` (staff) ·
`POST /api/v1/orders` (checkout) · `GET /api/v1/orders/{id}` · `GET /api/v1/reports/summary` (staff) ·
`POST /api/v1/auth/token` · SignalR `/hubs/stock` · `GET /health` · `GET /` (landing).

See the live contract at `/swagger` when running.

## Run & test

```bash
# Unit tests (no infrastructure)
dotnet test tests/Pos.Domain.Tests tests/Pos.Application.Tests

# API integration tests (spins a throwaway Postgres via Testcontainers — Docker required)
dotnet test tests/Pos.Api.Tests

# Run the API alone (needs a Postgres at the configured connection string)
dotnet run --project src/backend/Pos.Api
```

Configuration keys: `ConnectionStrings:Postgres`, `Jwt:*`, `StaffCredential:*`, `Seed:FilePath`,
`Cors:Origins`. See [../../DEPLOY.md](../../DEPLOY.md).
