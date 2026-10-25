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
- PostgreSQL (when running the API outside tests)

## How to run

> The API and client are added by later setup tickets. This section will be completed as they land.

```bash
# API
dotnet build src/api
dotnet run --project src/api/StackTrack.Api
dotnet test

# Client
cd src/client
npm install
npm run start
npm test
npm run lint
```

Check repo conventions: `bash scripts/check-repo.sh`

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch and commit conventions.
