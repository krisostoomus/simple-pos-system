# Deployment Guide

How to run the Charity Bake Sale POS end to end with Docker Compose.

## Prerequisites

- **Docker** with the Compose plugin (`docker compose version`).
- Ports **8080** (web) and **8081** (api) free on the host.
- That's all for running. (For local development without containers you also need the **.NET 10 SDK**, and Docker is still required for the API integration tests, which use Testcontainers.)

## Run the whole stack

From the repository root:

```bash
docker compose up --build
```

This builds and starts three services:

| Service | Image / build | Host port → container | Notes |
|---|---|---|---|
| `db` | `postgres:16-alpine` | internal only | Named volume `pos-db`; healthchecked. |
| `api` | `src/backend/Pos.Api/Dockerfile` | `8081` → `8080` | Applies EF migrations + seeds on startup; waits for a healthy `db`. |
| `web` | `src/frontend/Pos.Web/Dockerfile` | `8080` → `80` | nginx serving the Blazor WASM bundle. |

Run detached and wait for health:

```bash
docker compose up --build -d --wait
```

### URLs

| What | URL |
|---|---|
| POS app | http://localhost:8080 |
| Swagger UI | http://localhost:8081/swagger |
| OpenAPI document | http://localhost:8081/openapi/v1.json |
| Health check | http://localhost:8081/health |
| API landing page | http://localhost:8081/ |

### Staff login (demo)

`/admin` and `GET /api/v1/reports/summary` require a staff JWT. Compose dev credentials:

- **Username:** `staff`
- **Password:** `staff-password`

Obtain a token directly if needed:

```bash
curl -s -X POST http://localhost:8081/api/v1/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"staff","password":"staff-password"}'
```

### Teardown

```bash
docker compose down          # stop & remove containers
docker compose down -v       # also delete the database volume (fresh seed next start)
```

## ⚠️ Origin / CORS note (important)

The **browser** calls the API directly at `http://localhost:8081`. The browser therefore sends an
`Origin` header of `http://localhost:8080`, which the API's CORS policy must allow. The default
configuration allowlists **both** `http://localhost:8080` and `http://127.0.0.1:8080`, so browse to
the app at **`http://localhost:8080`** (or `http://127.0.0.1:8080`) — but be consistent: if you open
the app via one spelling, the API must allow that exact origin. If you change the web port or
hostname, update the API's `Cors:Origins` (see below) to match.

## Configuration

The API reads configuration from `appsettings.json`, environment variables, and (in compose) the
`api` service `environment:` block. Environment variables use `__` for nesting.

| Setting | Env var | Default (dev) | Purpose |
|---|---|---|---|
| Connection string | `ConnectionStrings__Postgres` | `Host=db;...` (compose) | PostgreSQL connection. |
| JWT signing key | `Jwt__SigningKey` | dev placeholder | **Symmetric key — set a strong secret in production.** |
| JWT issuer/audience | `Jwt__Issuer` / `Jwt__Audience` | `pos` / `pos` | Token validation. |
| Staff username | `StaffCredential__Username` | `staff` | Admin login. |
| Staff password | `StaffCredential__Password` | `staff-password` | **Set a real secret in production.** |
| Seed file | `Seed__FilePath` | `seed.json` | Catalog seeded on first start (resolved against the app base dir). |
| CORS origins | `Cors__Origins__0`, `Cors__Origins__1`, … | `localhost:8080`, `127.0.0.1:8080` | Allowed browser origins. |

The web SPA reads `ApiBaseUrl` from `src/frontend/Pos.Web/wwwroot/appsettings.json` (baked into the
published bundle). For the composed stack it is `http://localhost:8081/`. To point the SPA at a
different API host/port, change that value and rebuild the `web` image.

## Database migrations & seeding

The API runs `Database.MigrateAsync()` and then seeds the catalog from `seed.json` **if the catalog
is empty**, on startup. This assumes a single API instance (true for this setup). To re-seed from
scratch, recreate the volume: `docker compose down -v && docker compose up --build`.

The seed file (`src/backend/Pos.Infrastructure/Seeding/seed.json`) holds the edible and second-hand
catalog with prices, starting quantities, and Estonian translations. Second-hand items start at 0
stock; staff set them on the day via `/admin`.

## Verify the deployment

```bash
curl -s http://localhost:8081/health                 # -> Healthy
curl -s http://localhost:8081/api/v1/products | jq length   # -> 9
# then open http://localhost:8080, add items, check out, observe the change breakdown
```

> **Startup timing:** the `api` container can report *healthy* a moment before Kestrel is serving on
> the published host port, so the very first request right after `up --wait` may come back empty.
> Just retry, or poll until ready: `until curl -fs http://localhost:8081/health >/dev/null; do sleep 1; done`.

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| App loads but product calls fail (CORS error in console) | Browser origin not allowlisted — browse via an origin in `Cors:Origins`, or add yours and rebuild the `api`. |
| `ERR_CONNECTION_RESET` on API calls from the browser | IPv4/IPv6 mismatch — use `http://localhost:8080` consistently, or `127.0.0.1` for both app and `ApiBaseUrl`. |
| First request after `up --wait` is empty / connection refused | Startup race — the container is healthy a beat before the port serves. Retry, or poll `/health` until 200 (see "Startup timing" above). |
| API exits / unhealthy on start | `db` not healthy yet, or a bad `ConnectionStrings__Postgres`. Check `docker compose logs api`. |
| Port already in use | Another process on 8080/8081 — stop it or remap ports in `docker-compose.yml`. |
| Stock didn't update on another tablet | SignalR/WebSocket blocked — ensure the API origin is reachable and CORS allows credentials. |
| Want a clean catalog | `docker compose down -v` to drop the DB volume, then `up` again. |
