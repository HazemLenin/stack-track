# KAN-13 Local development environment with Docker Compose

**Goal:** One command (`docker compose up -d`) brings up Postgres the API can use. Most of this exists from KAN-11 (`docker-compose.yml`, `appsettings.Development.json`, `/health` + `HealthTests`). This ticket closes the gaps.

**Spec:** Jira KAN-13 (+ subtasks KAN-54, KAN-55). Conventions: CLAUDE.md. No new packages.

## Decisions (accepted by user)

- Bump Postgres `17-alpine` -> `18-alpine` ("latest stable major"). PG18 data dir is `/var/lib/postgresql`, so the volume mounts there. Existing local volumes must be recreated (`docker compose down -v`); noted in README.
- Compose reads `.env` with inline defaults (`${POSTGRES_PASSWORD:-stacktrack}`); works with no `.env`. `.env` is gitignored.
- Secret-history AC verified by manual `git log -p` grep, no new tooling.

## Existing state (do not redo)

- `docker-compose.yml`: `db` service, named volume `stacktrack-db`, port bound to 127.0.0.1.
- `appsettings.Development.json`: local default connection string.
- `/health` with `database` check; `HealthTests` cover 200/healthy and 503/unhealthy.
- DB is created by `POSTGRES_DB`; no migrations/entities yet, so no manual creation step.

## AC map

| AC | Task | Check |
|----|------|-------|
| `docker compose up -d` starts Postgres, reachable from API | 1 | `docker compose up -d`, healthcheck reports healthy, `dotnet run` + `curl /health` 200 |
| Stop/start keeps data | 1 | Manual: create table + row, `docker compose stop` / `start`, row still there (output pasted) |
| API starts with no manual DB creation | 1, 3 | Fresh volume (`down -v`), `up -d`, API starts, `/health` Healthy |
| `/health` healthy up / unhealthy down | 3 | Existing `HealthTests` (Testcontainers) + manual `docker compose stop db` -> 503 |
| No real secrets in git history | 2 | `.env` ignored (check-repo.sh), history grep pasted in PR |

## Tasks

### Task 1: Compose with PG18, env vars, healthcheck (KAN-54)

**Files:** Modify `docker-compose.yml`.

- [ ] Failing check first: extend `scripts/check-repo.sh` (Task 2) to assert compose uses `postgres:18`, a named volume, and `${POSTGRES_PASSWORD` interpolation. Run -> FAIL.
- [ ] Change image to `postgres:18-alpine`; volume target `/var/lib/postgresql`; env via `${POSTGRES_USER:-stacktrack}`, `${POSTGRES_PASSWORD:-stacktrack}`, `${POSTGRES_DB:-stacktrack}`; port `127.0.0.1:${POSTGRES_PORT:-5432}:5432`; add `pg_isready` healthcheck and `restart: unless-stopped`.
- [ ] `docker compose down -v && docker compose up -d --wait`; run persistence check (stop/start keeps row).
- [ ] Commit `KAN-13: pin Postgres 18 and read compose settings from env`

### Task 2: `.env.example`, gitignore, repo check (KAN-55)

**Files:** Create `.env.example`; modify `.gitignore`, `scripts/check-repo.sh`.

- [ ] Add failing checks to `check-repo.sh`: `.env` is git-ignored, `.env.example` not ignored and documents `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT`, `ConnectionStrings__Default`; compose checks from Task 1. Run -> FAIL.
- [ ] Create `.env.example` (local defaults, comments; note that real/non-local values go in user secrets or env vars, never committed). Add `.env` to `.gitignore`.
- [ ] Run `bash scripts/check-repo.sh` -> `OK`.
- [ ] Verify config override: `ConnectionStrings__Default=... dotnet run` is used over appsettings (ASP.NET default config order; confirm via `/health`).
- [ ] Commit `KAN-13: add .env.example and ignore .env`

### Task 3: README "Getting started" and verification

**Files:** Modify `README.md`.

- [ ] Add "Getting started" (prereqs, `cp .env.example .env` optional, `docker compose up -d`, run API, `/health`, stop/start keeps data, `docker compose down -v` wipes, PG18 volume note). Keep the `## How to run` heading (check-repo requires it).
- [ ] Verify end to end: fresh volume -> API start -> `/health` 200 -> `docker compose stop db` -> 503 -> `start` -> 200.
- [ ] Secret history scan: `git log -p --all` grep for private keys, tokens, non-default passwords; paste result.
- [ ] Full gate: `dotnet build src/api`, `dotnet test src/api`, client `npm test` and `npm run lint`; paste output.
- [ ] Commit `KAN-13: document local Docker Compose setup`

### Task 4: Docs, review, PR

- [ ] Code review against this plan + CLAUDE.md; fix findings.
- [ ] Add KAN-13 entry to Notion "Feature log"; update commands/layout if changed.
- [ ] Push `feature/KAN-13-docker-compose-dev-env`, PR `KAN-13: Local development environment with Docker Compose`, comment on Jira, transition to In Review (never Done).
