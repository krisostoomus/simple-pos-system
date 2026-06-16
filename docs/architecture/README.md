# Architecture

Diagrams for the Charity Bake Sale POS. The system is a separated frontend (Blazor WebAssembly SPA)
and backend (ASP.NET Core REST API + SignalR) over PostgreSQL, packaged with Docker Compose.

- [C4 Level 1 — System Context](#c4-level-1--system-context)
- [C4 Level 2 — Containers](#c4-level-2--containers)
- [C4 Level 3 — API Components](#c4-level-3--api-components)
- [Checkout sequence](#checkout-sequence)
- [Data model (ER)](#data-model-er)

---

## C4 Level 1 — System Context

```mermaid
flowchart TB
    seller["Salesperson<br/>(tablet / phone)"]
    staff["Staff member<br/>(stock & reports)"]
    pos["Charity Bake Sale POS<br/>(web SPA + REST API + DB)"]

    seller -->|"selects items, checks out, gets change"| pos
    staff -->|"logs in, sets second-hand stock, views funds raised"| pos
```

The POS lets multiple salespeople ring up bake-sale and second-hand purchases concurrently on their
own devices, calculates change, and records sales so the charity can see the funds raised. Staff
authenticate to set second-hand stock and view reports.

---

## C4 Level 2 — Containers

```mermaid
flowchart TB
    subgraph client["Browser (tablet / phone)"]
        web["Web SPA<br/>Blazor WebAssembly + MudBlazor<br/>served by nginx"]
    end

    subgraph server["Docker Compose"]
        api["REST API + SignalR hub<br/>ASP.NET Core minimal API (.NET 10)<br/>JWT auth, OpenAPI/Swagger"]
        db[("PostgreSQL<br/>EF Core 10, xmin concurrency")]
    end

    web -->|"HTTPS/JSON<br/>/api/v1/* (Accept-Language, Idempotency-Key, Bearer)"| api
    web -->|"WebSocket<br/>/hubs/stock (live stock)"| api
    api -->|"EF Core / Npgsql"| db
```

| Container | Tech | Responsibility | Port (compose) |
|---|---|---|---|
| Web SPA | Blazor WASM + MudBlazor, nginx | Product grid, cart, checkout, admin, localization | host `8080` → `80` |
| API | ASP.NET Core (.NET 10) | Catalog, checkout, reports, auth, stock hub | host `8081` → `8080` |
| Database | PostgreSQL 16 | Products, translations, orders, order lines | internal only |

---

## C4 Level 3 — API Components

The API follows lightweight Clean Architecture: dependencies point inward
(Api → Application → Domain); Infrastructure implements Application's ports.

```mermaid
flowchart TB
    subgraph apiproj["Pos.Api"]
        endpoints["Endpoints<br/>products · orders · reports · auth"]
        problem["ProblemDetails handler<br/>(exception → errorCode)"]
        authz["JWT bearer + staff policy"]
        hub["StockHub (SignalR)"]
    end

    subgraph appproj["Pos.Application"]
        checkout["CheckoutService"]
        catalog["CatalogService"]
        reporting["ReportingService"]
        ports["Ports (interfaces):<br/>IProductRepository · IOrderRepository<br/>IUnitOfWork · IPaymentService<br/>IStockNotifier · IReportQueries"]
    end

    subgraph domproj["Pos.Domain"]
        entities["Product · Order · OrderLine<br/>ChangeCalculator (greedy)"]
    end

    subgraph infraproj["Pos.Infrastructure"]
        repos["EF Core repositories + UnitOfWork"]
        payment["CashPaymentService"]
        notifier["SignalRStockNotifier"]
        seeder["Config seeder (seed.json)"]
        token["TokenService (JWT)"]
    end

    endpoints --> checkout & catalog & reporting
    checkout & catalog & reporting --> ports
    checkout & catalog & reporting --> entities
    repos & payment & notifier & seeder -.implements.-> ports
    repos --> entities
    notifier --> hub
```

---

## Checkout sequence

Shows the happy path plus the optimistic-concurrency guard and the live stock broadcast.

```mermaid
sequenceDiagram
    autonumber
    participant U as Seller (SPA)
    participant A as API (CheckoutService)
    participant P as PaymentService
    participant DB as PostgreSQL
    participant H as StockHub
    participant O as Other tablets

    U->>A: POST /api/v1/orders (lines, cashPaidCents, Idempotency-Key)
    A->>DB: load products by id
    alt any line exceeds stock
        A-->>U: 409 out_of_stock
    else cash < total
        A->>P: AcceptCash(total, cash)
        P-->>A: InsufficientPayment
        A-->>U: 422 insufficient_payment
    else ok
        A->>P: AcceptCash(total, cash) → change
        A->>DB: decrement stock + insert order (1 transaction, xmin check)
        alt concurrency conflict
            A->>DB: reload + retry once
        end
        DB-->>A: committed
        A->>H: StockChanged(productId, newQty)
        H-->>O: live update (gray out if 0)
        A-->>U: 201 Created (order, change breakdown)
    end
```

---

## Data model (ER)

```mermaid
erDiagram
    PRODUCT ||--o{ PRODUCT_TRANSLATION : "has"
    ORDER ||--|{ ORDER_LINE : "contains"

    PRODUCT {
        int Id PK
        string Name "canonical (en)"
        string Category "Edible | SecondHand"
        int PriceCents
        int StockQuantity
        string ImageKey
        bool IsActive
        uint xmin "concurrency token"
    }
    PRODUCT_TRANSLATION {
        int Id PK
        int ProductId FK
        string CultureCode "e.g. et"
        string Name "localized"
    }
    ORDER {
        int Id PK
        datetime CreatedAtUtc
        int TotalCents
        int CashPaidCents
        int ChangeCents
        guid IdempotencyKey UK
    }
    ORDER_LINE {
        int Id PK
        int OrderId FK
        int ProductId
        string ProductName "snapshot"
        int UnitPriceCents "snapshot"
        int Quantity
    }
```

Order lines snapshot the product's canonical name and unit price so historical orders and the
funds-raised report stay correct even if a product is later renamed or repriced. Localized product
names live in `PRODUCT_TRANSLATION` with fallback to the canonical `Name`; money is stored as integer
cents throughout.
