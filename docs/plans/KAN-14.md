# KAN-14 Design the database schema (ERD)

**Goal:** Commit `docs/erd.md`: Mermaid ERD + written rules every later feature ticket references. Docs only, no C#/Angular, no migrations, no packages.

**Spec:** Jira KAN-14 (+ subtasks KAN-56 ERD draft, KAN-57 ordering + delete behaviors). Conventions: CLAUDE.md. No blocking tickets, no comments.

## Decisions (my calls, flagged for approval)

1. **Ordering:** `Position` is `double precision` (fractional indexing). Insert between A and B = `(A+B)/2`; first = `first/2` (or 1000 on empty); last = `last+1000`. Moving a card writes one row. When gap between neighbours < 1e-9, rebalance that one list to 1000, 2000, ... in a single transaction. No unique constraint on `(ListId, Position)` (avoids transient conflicts); non-unique index on `(ListId, Position)`. Same scheme for `List.Position` within a board. Chosen over LexoRank strings: simpler, native sort, rebalance is rare.
2. **Board owner** is also inserted as a `BoardMember` on board creation (so "members of a board" is one query). Owner FK is required.
3. **All FKs to `User` are RESTRICT.** Users never deleted (deactivate via `IsActive`), so nothing cascades from a user. This also guarantees board deletion can never delete users.
4. **Case-insensitive uniqueness:** `User.Email` stored lowercased, unique; `Label.Name` unique per board on `lower(Name)`.
5. **RefreshToken:** `TokenHash` (unique), `ExpiresAt`, `IsRevoked`, `ReplacedByTokenId` self-FK, SET NULL.
6. **Not DB-enforced (documented as app rules):** assignee must be a board member; card label must belong to the card's board.
7. **Audit columns:** every table has `Id` (GUID), `CreatedAt`, `UpdatedAt` (UTC, `timestamptz`). Join tables (`BoardMember`, `CardAssignee`, `CardLabel`) get their own GUID `Id` plus a unique constraint on the pair, so the rule "every table has CreatedAt/UpdatedAt and GUID PK" holds uniformly. `BoardMember.JoinedAt` is the requested joined date.

## Delete behaviors (to document)

| Relationship | Cardinality | On delete of parent |
|---|---|---|
| User -> Board (owner) | 1:N | RESTRICT |
| Board -> BoardMember | 1:N | CASCADE |
| User -> BoardMember | 1:N | RESTRICT |
| Board -> List | 1:N | CASCADE |
| List -> Card | 1:N | CASCADE |
| Board -> Label | 1:N | CASCADE |
| Card -> CardAssignee | 1:N | CASCADE |
| User -> CardAssignee | 1:N | RESTRICT |
| Card -> CardLabel | 1:N | CASCADE |
| Label -> CardLabel | 1:N | CASCADE |
| Card -> Comment | 1:N | CASCADE |
| User -> Comment (author) | 1:N | RESTRICT |
| User -> RefreshToken | 1:N | RESTRICT |
| RefreshToken -> RefreshToken (replaced by) | 1:0..1 | SET NULL |

Board delete chain: Board -> Lists -> Cards -> Comments/Assignees/CardLabels, Board -> Labels, Board -> Members. Users untouched.

## AC map

| AC | Task | Check |
|----|------|-------|
| `docs/erd.md` renders on GitHub | 1 | `mermaid-cli` not added (no packages); validate by pasting into the Mermaid live parser / GitHub preview after push; `check-erd.sh` asserts a fenced ` ```mermaid ` block with `erDiagram` |
| Every relationship has cardinality + delete behavior | 1, 2 | `check-erd.sh`: all 14 relationships from table above appear in the doc's delete table with CASCADE/RESTRICT/SET NULL |
| Board delete removes lists, cards, labels, comments, memberships, never users | 2 | `check-erd.sh` asserts the cascade chain section and "never delete users" wording; no User row is CASCADE target |
| User deletion unsupported, deactivate instead | 2 | `check-erd.sh` greps "deactivat" and `IsActive` |
| Unique constraints documented | 2 | `check-erd.sh` greps all four: user email, (BoardId,UserId), label name per board, (CardId,UserId) |
| Ordering strategy + worked example of moving a card between two others | 3 | `check-erd.sh` greps "Ordering" section and an example with three numeric positions; example arithmetic verified by hand in the PR |

## Tasks

### Task 1: ERD diagram (KAN-56)
**Files:** Create `docs/erd.md`, `scripts/check-erd.sh`.
- [ ] RED: write `scripts/check-erd.sh` (bash, `grep -q` assertions per AC above, exits non-zero with the failing assertion). Run with no `docs/erd.md` -> FAIL.
- [ ] GREEN: `docs/erd.md` with `erDiagram` for the 10 entities, attributes (type, PK/FK/UK), relationships with crow's-foot cardinality. Conventions section: GUID PKs, `CreatedAt`/`UpdatedAt`.
- [ ] Run check (diagram assertions pass; later sections still FAIL, expected).
- [ ] Commit `KAN-14: add ERD diagram`

### Task 2: Relationships, delete behavior, constraints (KAN-57 part 1)
- [ ] RED: check-erd.sh already asserts delete table, board cascade, deactivation, four unique constraints -> FAIL.
- [ ] GREEN: add sections: Relationships and delete behavior (table above), Board deletion, Users (no delete, `IsActive`), Unique constraints, App-level rules.
- [ ] Run check -> passes except ordering.
- [ ] Commit `KAN-14: document delete behavior and unique constraints`

### Task 3: Ordering strategy (KAN-57 part 2)
- [ ] RED: ordering assertions fail.
- [ ] GREEN: "Ordering" section: scheme, why one-row moves, insert rules, rebalance, example (cards at 1000, 2000, 3000; move C between A and B -> 1500; only C's row changes; list move same).
- [ ] Run check -> `OK`.
- [ ] Commit `KAN-14: document ordering strategy`

### Task 4: Verify, review, Notion, PR
- [ ] `bash scripts/check-erd.sh` and `bash scripts/check-repo.sh` real output; `dotnet build src/api`, `dotnet test src/api`, `npm test`, `npm run lint` (sanity, unchanged code) pasted.
- [ ] Mermaid validity: confirm renders on GitHub preview of pushed branch.
- [ ] Review vs plan + CLAUDE.md; fix findings.
- [ ] Notion "Feature log" entry (docs-only: link `docs/erd.md`, how to read it); add `docs/erd.md` to layout section.
- [ ] Push `feature/KAN-14-database-erd`, PR `KAN-14: Design the database schema (ERD)`, Jira comment, transition to In Review.

## Out of scope
Entities, EF config, migrations, seed data (later tickets implement this doc).
