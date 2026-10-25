#!/usr/bin/env bash
# Repo convention checks for KAN-10. Run from repo root: bash scripts/check-repo.sh
cd "$(dirname "$0")/.." || exit 1
failures=0
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }

for d in src/api src/client docs/plans; do
  [ -d "$d" ] || fail "missing dir $d"
done

[ "$failures" -eq 0 ] && echo "OK" || exit 1
