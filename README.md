# MinimalApiEntity – Pizza API

A small ASP.NET Core **Minimal API** on **.NET 10** that stores pizzas in **PostgreSQL** with **Entity Framework Core**, and puts a **Redis** cache in front of the reads.

## Features

- **Minimal API endpoints** grouped in an extension method (`MapPizza`), using `TypedResults` / `Results<...>` so the OpenAPI document shows each endpoint's real responses.
- **EF Core + PostgreSQL (Npgsql)** with code-first migrations. The migrations run when the app starts.
- **Cache-aside reads with Redis** (`IDistributedCache`):
  - Responses are cached for 60 seconds. Cache keys for pizza names are hashed with SHA-256.
  - The cache is cleared after a successful write.
  - If Redis is down, the API logs a warning and reads from PostgreSQL instead of failing.
- **Rate limiting**: read requests and write requests each have their own concurrency limit and queue. A full queue returns `429` with a `Retry-After` header.
- **Writes are serialized across instances** with a transaction-scoped PostgreSQL advisory lock (`pg_advisory_xact_lock`). If the lock isn't available within 5 seconds, the API returns `429`.
- **No secrets in source control.** The local database password is generated, stored outside the repo, and passed to the app through `dotnet user-secrets` and a Docker Compose secret. Containers are only reachable from `127.0.0.1`.

## Project structure

```
MinimalApiEntity.slnx
├── MinimalApiEntity/              # Web API
│   ├── Endpoints/                 # Minimal API route mappings
│   ├── Data/                      # DbContext, repository, EF migrations
│   ├── Models/                    # Entities and request/response records
│   ├── Services/                  # Redis-backed read service
│   ├── scripts/Start-Postgres.ps1 # Starts the local PostgreSQL and Redis containers
│   ├── compose.yaml
│   └── MinimalApiEntity.http      # Example requests
└── MinimalApiEntity.Tests/        # xUnit tests
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- PowerShell 7+ (for the setup script)

### Run locally

```powershell
# 1. Start PostgreSQL and Redis, then save the connection string to user-secrets
.\MinimalApiEntity\scripts\Start-Postgres.ps1

# 2. Run the API (migrations run when it starts)
dotnet run --project MinimalApiEntity
```

The API listens on `http://localhost:5295`. In Development, the OpenAPI document is served at `/openapi/v1.json`. Try the requests in [`MinimalApiEntity.http`](MinimalApiEntity/MinimalApiEntity.http).

### Run tests

```powershell
dotnet test
```

## Endpoints

| Method | Route           | Description                               | Responses            |
|--------|-----------------|-------------------------------------------|----------------------|
| GET    | `/pizza`        | List all pizzas (cached)                  | 200, 429             |
| GET    | `/pizza/{name}` | Get a pizza by name (cached)              | 200, 404, 429        |
| POST   | `/pizza`        | Create a pizza and clear related cache    | 201, 400, 429        |

## Configuration

| Key                        | Description                                                         |
|----------------------------|---------------------------------------------------------------------|
| `ConnectionStrings:Pizzas` | PostgreSQL connection string (set in user-secrets by the script)    |
| `ConnectionStrings:Redis`  | Redis connection string (`appsettings.Development.json` for local)  |
| `Redis:InstanceName`       | Optional cache key prefix (default `MinimalApiEntity:v1:`)          |
