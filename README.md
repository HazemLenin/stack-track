# Stack Track

Trello-style task tracker. Monorepo with a .NET 10 API and an Angular 21 client.

## Layout

| Path | Contents |
|------|----------|
| `src/api` | .NET solution (Domain, Application, Infrastructure, Api) |
| `src/client` | Angular app |
| `docs/plans` | One implementation plan per Jira ticket (`KAN-12.md`) |
| `scripts` | Repo helper scripts |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (current LTS) and npm
- [Docker](https://www.docker.com/) (API integration tests use Testcontainers with real PostgreSQL)

## Getting started

No manual PostgreSQL install needed. Docker Compose runs it.

```bash
docker compose up -d --wait        # Postgres 18 on 127.0.0.1:5432, waits until healthy
dotnet run --project src/api/StackTrack.Api --launch-profile http
curl -i http://localhost:5009/health   # 200 {"status":"Healthy","checks":{"database":"Healthy"}}
```

- The database (`stacktrack`) is created by the container on first start. No manual DB step.
- Data lives in the named volume `stacktrack-db`. `docker compose stop` / `start` keeps it; `docker compose down -v` wipes it.
- `/health` returns `503` with `database: Unhealthy` while Postgres is stopped.
- Defaults (`stacktrack` / `stacktrack`) are local-only. To override, copy `.env.example` to `.env` (gitignored). The API reads `ConnectionStrings:Default` from `appsettings.Development.json`; override it with the `ConnectionStrings__Default` environment variable or `dotnet user-secrets`. Never commit real credentials.
- Upgrading from the old Postgres 17 volume: run `docker compose down -v` once, which destroys local data (Postgres 18 uses a different data layout).

## How to run

> The API is scaffolded (KAN-11). The client is added by a later setup ticket.

```bash
# API
docker compose up -d db                       # local PostgreSQL (stacktrack/stacktrack)
dotnet build src/api
dotnet run --project src/api/StackTrack.Api --launch-profile http
curl -i http://localhost:5009/health          # 200 + {"status":"Healthy",...}
dotnet test src/api                           # needs Docker running

# Client (not added yet)
cd src/client
npm install
npm run start
npm test
npm run lint
```

Check repo conventions: `bash scripts/check-repo.sh`

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch and commit conventions.
