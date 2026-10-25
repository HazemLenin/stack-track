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

[ "$failures" -eq 0 ] && echo "OK" || exit 1
