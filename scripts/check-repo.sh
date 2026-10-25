#!/usr/bin/env bash
# Repo convention checks for KAN-10. Run from repo root: bash scripts/check-repo.sh
cd "$(dirname "$0")/.." || exit 1
failures=0
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }

for d in src/api src/client docs/plans; do
  [ -d "$d" ] || fail "missing dir $d"
done

for p in src/api/StackTrack.Api/bin/x.dll src/api/StackTrack.Domain/obj/x src/client/node_modules/x \
  src/client/dist/x src/client/.angular/x .idea/x .vs/x .vscode/x foo.user .DS_Store Thumbs.db \
  .claude/worktrees/x; do
  git check-ignore -q "$p" || fail "should be ignored: $p"
done
for p in CLAUDE.md docs/plans/KAN-10.md src/api/.gitkeep scripts/check-repo.sh; do
  git check-ignore -q "$p" && fail "must not be ignored: $p"
done

grep -q '^root = true' .editorconfig 2>/dev/null || fail ".editorconfig missing or no root = true"
grep -q 'eol=lf' .gitattributes 2>/dev/null || fail ".gitattributes missing eol=lf"

for h in "Stack Track" "## Prerequisites" "## How to run"; do
  grep -q "$h" README.md 2>/dev/null || fail "README.md missing: $h"
done
for h in "feature/KAN-" "fix/KAN-" "KAN-<n>:" "one ticket = one branch = one PR"; do
  grep -qi -- "$h" CONTRIBUTING.md 2>/dev/null || fail "CONTRIBUTING.md missing: $h"
done

# KAN-13: local dev environment
grep -q 'image: postgres:18' docker-compose.yml 2>/dev/null || fail "docker-compose.yml must use postgres:18"
grep -q 'stacktrack-db:/var/lib/postgresql$' docker-compose.yml 2>/dev/null || fail "docker-compose.yml needs named volume on /var/lib/postgresql"
grep -q 'POSTGRES_PASSWORD:-' docker-compose.yml 2>/dev/null || fail "docker-compose.yml must read POSTGRES_PASSWORD from env with default"
git check-ignore -q .env || fail ".env must be ignored"
git check-ignore -q .env.example && fail ".env.example must not be ignored"
for v in POSTGRES_USER POSTGRES_PASSWORD POSTGRES_DB POSTGRES_PORT ConnectionStrings__Default; do
  grep -q "^#\? *$v=" .env.example 2>/dev/null || fail ".env.example missing $v"
done

[ "$failures" -eq 0 ] && echo "OK" || exit 1
