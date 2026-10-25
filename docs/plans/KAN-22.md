# KAN-22 Boards are private to their members

**Goal:** One central place on the backend decides whether the current user may see a board or anything inside it. No access looks exactly like "does not exist" (404). New board-scoped requests get the rule by default.

**Spec:** Jira KAN-22 (subtasks KAN-77 central check, KAN-78 child resources, KAN-79 lost access in UI), `CLAUDE.md`.

**Branch:** `feature/KAN-22-board-privacy`, stacked on `feature/KAN-21-admin-users` (builds on KAN-20's `IBoardAccess`). PR base = KAN-21 branch.

## Scope (agreed)

**No board, list, card or comment endpoints exist yet** (only entities and the client placeholder `boards-page`). So this ticket builds the access layer and proves it with tests; the HTTP-level scenarios become real when the board CRUD tickets add endpoints. Those tickets just implement `IBoardScoped` / `IListScoped` / `ICardScoped` / `ICommentScoped` on their requests.

## Gap analysis

| Rule / AC | Current state | Action |
|-----------|---------------|--------|
| Central enforcement, on by default | `IBoardAccess` (KAN-20) answers a level, nothing calls it | MediatR `BoardAccessBehavior` (Task 1) |
| No access == not found | No `NotFoundException` | New exception + 404 mapping (Task 1) |
| Child items by id (list, card, label, comment) | No resolver | `IBoardAccess` resolves the owning board id from child ids, in one query per kind (Task 2) |
| Move card into inaccessible board | No move feature | Rule in the behavior: a request that references two scopes needs access to both; card stays (Task 3) |
| Lost access in UI | No board API calls yet | Interceptor on 404 from `boards/*` (Task 4) |

## Decisions (my calls, flag if wrong)

1. **Marker interfaces on requests**: `IBoardScoped { Guid BoardId }`, `IListScoped { Guid ListId }`, `ICardScoped { Guid CardId }`, `ICommentScoped { Guid CommentId }`, plus `IWriteRequest` (commands). The behavior resolves the board id, asks `IBoardAccess`, then: `None` -> `NotFoundException`; `ReadOnly` + `IWriteRequest` -> `403`. (Admin read-only: they can see the board, so a write is a plain 403, not 404.)
2. **Member vs owner:** both are Full, as in KAN-20. No per-board roles (CLAUDE.md).
3. **Unknown id and no-access are byte-identical:** same exception, same problem details, no id echoed in `detail`. Tested by comparing both responses.
4. **Move:** a request implementing both `ICardScoped` and `IListScoped` (the destination) has both checked, each independently; the first failure is 404 and nothing is saved because the handler never runs.
5. **Archived boards:** still accessible to members (archive is a separate feature).
6. **Client:** `404` from any `boards/...` request -> toast "This board is no longer available" and navigate to `/boards`. Matches the URL path only; other 404s are untouched.
7. **Verifying without endpoints:** access tests call MediatR with test-only requests/handlers declared in `Api.Tests`, over a real Postgres.

## AC map

| AC | Task | Test |
|----|------|------|
| Member sees lists and cards | 1 | `Member_request_reaches_handler` |
| Non member by URL/API -> 404, same as unknown id | 1 | `NonMember_gets_NotFound`; `NonMember_and_unknown_board_responses_are_identical` (HTTP, test-only controller) |
| Child items by id -> 404 | 2 | `NonMember_gets_NotFound_for_list_card_label_comment_ids` (theory over 4 kinds) |
| Move card into inaccessible board rejected, card stays | 3 | `Move_into_inaccessible_board_is_rejected_and_card_stays` |
| Lost access -> message + back to My boards | 4 | `404 on a board request shows the message and goes to /boards`; `other 404s are ignored` |
| Rule: central, new endpoints get it by default | 1, 3 | `ArchitectureTests`: every request implementing a scope marker is covered; a request with a board scope marker but no behavior registration fails the build test |
| Rule: Admin read only | 1 | `Admin_non_member_can_read_but_write_returns_403` |

## Files

- API: `Application/Boards/{IBoardScoped.cs (markers), BoardAccessBehavior.cs}`, `Application/Errors/NotFoundException.cs`, `IBoardAccess` additions (child resolvers), `Infrastructure/Boards/BoardAccess.cs`, `Application/DependencyInjection.cs` (register behavior after validation), `Api/Errors/GlobalExceptionHandler.cs` (404 + 403 cases)
- Tests: `Api.Tests/Boards/BoardAccessBehaviorTests.cs`, `Api.Tests/ArchitectureTests.cs` (extend), `ErrorHandlingTests.cs` (404 shape)
- Client: `core/board-access.interceptor.ts` (+ spec), `app.config.ts` (register), uses existing `ToastService`

## Tasks

### Task 1: Central behavior and 404 (KAN-77)
- [ ] Tests (Member passes, non-member 404, admin read vs write, identical responses), expect FAIL
- [ ] `NotFoundException`, handler mapping, markers, `BoardAccessBehavior`, DI registration
- [ ] Commit `KAN-22: enforce board access in one MediatR behavior`

### Task 2: Child resources (KAN-78)
- [ ] Theory test over list/card/label/comment ids, expect FAIL
- [ ] `IBoardAccess` resolves the owning board for each child kind (join to `board_id`)
- [ ] Commit `KAN-22: resolve board access for lists, cards, labels and comments`

### Task 3: Cross-board move (KAN-78)
- [ ] Test: member of A only, card in A, destination list in B -> NotFound, card unchanged in DB
- [ ] Behavior checks every scope marker a request implements
- [ ] Architecture test described in AC map
- [ ] Commit `KAN-22: check both boards when a request touches two`

### Task 4: Lost board access in the UI (KAN-79)
- [ ] Spec: 404 on `boards/{id}/...` toasts and navigates to `/boards`; 404 elsewhere ignored; non-404 ignored
- [ ] Impl: functional interceptor, registered in `app.config.ts`
- [ ] Commit `KAN-22: send users back to My boards when board access is lost`

## Delegation
Tasks 1-3 -> `api-cqrs-implementer`; Task 4 -> `angular-feature-builder`. Independent, parallel worktrees, then merge.
