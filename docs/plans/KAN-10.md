# KAN-10 Initialize monorepo and repository conventions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One repo with agreed layout, ignore/format rules, README and CONTRIBUTING, pushed to `main`.

**Architecture:** Config/docs only, no production code. "Tests" are shell checks (`git check-ignore`, `test -d`, `grep`) collected in `scripts/check-repo.sh`, run by hand; no CI added.

**Tech Stack:** git, GitHub (`origin` = https://github.com/HazemLenin/stack-track), bash.

**Spec:** Jira KAN-10 (+ subtasks KAN-44, KAN-45), overridden by CLAUDE.md where they conflict (see memory `kan-10-conventions`).

## Global Constraints

- Layout: `src/api`, `src/client`, `docs/plans` (NOT `/backend` `/frontend`).
- Commits: `KAN-10: imperative message`. No Conventional Commits.
- Branch: `feature/KAN-10-monorepo-conventions`. Default branch `main`.
- No new NuGet/npm packages. Don't edit files outside ticket scope.
- Ticket stays In Progress (no transition to In Review unless user says).
- `origin/main` already has one commit (`5c28774 Initial commit`, only `README.md`). Branch from it. Local `master` has no commits, ignore it.

## Review Focus

- Nested `bin/`/`obj/` deep under `src/api/**` must be ignored (not just root).
- `node_modules/` and `dist/` under `src/client` must be ignored; `.angular/` cache too.
- `.vscode/` ignored but shared settings is not needed; `.idea/`, `.vs/`, `*.user` ignored.
- Empty dirs are not tracked by git: `src/api`, `src/client` need `.gitkeep`.
- `docs/plans/` and `CLAUDE.md` must NOT be ignored.
- README existing remote content must be replaced, not duplicated.

## AC map

| AC | Task | Check |
|----|------|-------|
| Repo on GitHub, `main` default | 1 | `git ls-remote origin` shows `refs/heads/main`; PR targets `main` |
| Folder structure | 1 | `check-repo.sh` dirs |
| No build output committable | 2 | `check-repo.sh` ignore cases |
| README: description, prerequisites, how to run | 3 | `check-repo.sh` greps headings |
| Branch/commit conventions in CONTRIBUTING.md | 3 | `check-repo.sh` greps |
| First commit message convention | 1 | see note |

Note on last AC: ticket says Conventional Commits; user decided `KAN-10:` format wins. First commit is `KAN-10: add repo layout and check script`. Called out in PR.

---

### Task 1: Layout and check script (KAN-44 part 1)

**Files:**
- Create: `scripts/check-repo.sh`, `src/api/.gitkeep`, `src/client/.gitkeep`
- Existing: `docs/plans/KAN-10.md`, `CLAUDE.md` (commit both; CLAUDE.md has the branch-convention edit)

**Interfaces:**
- Produces: `scripts/check-repo.sh` exit 0 when all checks pass, prints `FAIL: <what>` per failure. Later tasks append checks.

- [ ] **Step 1: Write `scripts/check-repo.sh` with only the layout check** (`test -d src/api && test -d src/client && test -d docs/plans`; `fail()` helper counts failures; exit 1 if any).
- [ ] **Step 2: Run `bash scripts/check-repo.sh`.** Expected: FAIL for `src/api`, `src/client`.
- [ ] **Step 3: Create the two `.gitkeep` files.**
- [ ] **Step 4: Run again.** Expected: exit 0.
- [ ] **Step 5: Commit** `KAN-10: add repo layout and check script`

### Task 2: .gitignore and .editorconfig (KAN-44 part 2)

**Files:**
- Create: `.gitignore`, `.editorconfig`
- Modify: `scripts/check-repo.sh`

- [ ] **Step 1: Add checks** using `git check-ignore -q <path>` that must succeed for: `src/api/StackTrack.Api/bin/x.dll`, `src/api/StackTrack.Domain/obj/x`, `src/client/node_modules/x`, `src/client/dist/x`, `src/client/.angular/x`, `.idea/x`, `.vs/x`, `.vscode/x`, `foo.user`, `.DS_Store`, `Thumbs.db`; and must FAIL (not ignored) for `CLAUDE.md`, `docs/plans/KAN-10.md`, `src/api/.gitkeep`. Also check `.editorconfig` exists and contains `root = true`.
- [ ] **Step 2: Run script.** Expected: FAIL on every ignore case.
- [ ] **Step 3: Write `.gitignore`** covering .NET (`bin/`, `obj/`, `*.user`, `.vs/`, `TestResults/`), Node/Angular (`node_modules/`, `dist/`, `.angular/`, `*.log`), IDE (`.idea/`, `.vscode/`), OS (`.DS_Store`, `Thumbs.db`). Patterns without leading slash so nested dirs match.
- [ ] **Step 4: Write `.editorconfig`**: `root = true`; `*` utf-8, lf, final newline, trim trailing whitespace; `*.cs` 4 spaces; `*.{ts,html,scss,json,yml,yaml,md}` 2 spaces; `*.md` keep trailing whitespace.
- [ ] **Step 5: Run script.** Expected: exit 0.
- [ ] **Step 6: Commit** `KAN-10: add gitignore and editorconfig`

### Task 3: README and CONTRIBUTING (KAN-45)

**Files:**
- Modify: `README.md` (replace remote's stub), `scripts/check-repo.sh`
- Create: `CONTRIBUTING.md`

- [ ] **Step 1: Add checks:** README contains headings `## Prerequisites` and `## How to run` and word `Stack Track`; CONTRIBUTING contains `feature/KAN-`, `fix/KAN-`, `KAN-<n>:` commit format, and "one ticket = one branch = one PR".
- [ ] **Step 2: Run script.** Expected: FAIL on those.
- [ ] **Step 3: Write README.md:** description (Trello-style task tracker, .NET 10 API + Angular 21 client), layout section, Prerequisites (.NET 10 SDK, Node LTS, Docker for integration tests, PostgreSQL), How to run (commands from CLAUDE.md; mark "to be completed as setup tickets land"), link to CONTRIBUTING.
- [ ] **Step 4: Write CONTRIBUTING.md:** branch names, commit format with example, one ticket/branch/PR, PR title `KAN-<n>: <summary>`, no Done transitions note not needed (internal). Note why it differs from Conventional Commits.
- [ ] **Step 5: Run script.** Expected: exit 0.
- [ ] **Step 6: Commit** `KAN-10: add README and CONTRIBUTING`

### Task 4: Verify and PR

- [ ] `bash scripts/check-repo.sh` exit 0; `git status` clean; `git ls-files` shows no bin/obj/node_modules/dist.
- [ ] No API/client build or tests exist yet; state that in PR instead of pasting results.
- [ ] Push `feature/KAN-10-monorepo-conventions`, open PR to `main` titled `KAN-10: Initialize monorepo and repository conventions` with AC table, deviation note, how to test (`bash scripts/check-repo.sh`).
- [ ] Comment PR link on KAN-10. Leave status In Progress (per user).
