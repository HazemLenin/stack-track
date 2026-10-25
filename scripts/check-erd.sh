#!/usr/bin/env bash
# KAN-14: checks that docs/erd.md covers the acceptance criteria. Run from repo root: bash scripts/check-erd.sh
cd "$(dirname "$0")/.." || exit 1
f=docs/erd.md
failures=0
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }
has() { grep -qiE -- "$2" "$f" 2>/dev/null || fail "$1"; }

[ -f "$f" ] || { echo "FAIL: $f missing"; exit 1; }

# diagram: fenced mermaid block with every entity
grep -q '^```mermaid' "$f" || fail "no mermaid block"
has "no erDiagram" '^erDiagram'
for e in User Board BoardMember List Card CardAssignee Label CardLabel Comment RefreshToken; do
  grep -qE "^\s+$e \{" "$f" || fail "entity not in diagram: $e"
done
has "GUID primary keys not stated" 'GUID'
has "CreatedAt/UpdatedAt not stated" 'CreatedAt.*UpdatedAt'

# every relationship: cardinality + delete behavior in the delete table
for r in "User -> Board" "Board -> BoardMember" "User -> BoardMember" "Board -> List" "List -> Card" \
  "Board -> Label" "Card -> CardAssignee" "User -> CardAssignee" "Card -> CardLabel" "Label -> CardLabel" \
  "Card -> Comment" "User -> Comment" "User -> RefreshToken" "RefreshToken -> RefreshToken"; do
  grep -qE "^\|.*$r.*\| *(1:N|1:0\.\.1) *\| *(CASCADE|RESTRICT|SET NULL)" "$f" || fail "relationship row missing cardinality/delete: $r"
done

# board delete + users
has "board delete chain missing" 'deleting a board.*(lists|cards)'
has "never delete users not stated" 'never.*delete.*users?'
has "user deletion unsupported / deactivate not documented" 'deactivat'
has "IsActive missing" 'IsActive'

# unique constraints
has "unique: user email" 'unique.*email|email.*unique'
has "unique: (BoardId, UserId) membership" 'BoardId, *UserId'
has "unique: label name per board" 'lower\(Name\)|label name.*per board'
has "unique: (CardId, UserId) assignment" 'CardId, *UserId'

# ordering
has "Ordering section missing" '^#+ .*ordering'
has "ordering example missing" 'example'
has "ordering example positions missing" '1000.*2000.*3000'
has "ordering: single-row move not explained" 'one row|single row'

if [ "$failures" -eq 0 ]; then echo OK; else echo "$failures check(s) failed"; exit 1; fi
