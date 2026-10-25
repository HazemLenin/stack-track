# KAN-18 Stay signed in

> **For agentic workers:** use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** A signed-in user's session renews silently, refresh tokens rotate with reuse detection, the 7 day window is fixed from sign in, and an expired session lands on sign-in with a clear message.

**Architecture:** KAN-17 already built `POST /auth/refresh`, rotation in `TokenService.RotateAsync`, hashed storage, chain revocation, inactive-user rejection and a single-flight `AuthService.refresh()` used by `authInterceptor`. This ticket closes the three gaps between that code and the KAN-18 rules. No new packages, no migration.

**Spec:** Jira KAN-18 (subtasks KAN-66 refresh endpoint, KAN-67 interceptor, KAN-68 expired session UI), `CLAUDE.md`, `docs/plans/KAN-17.md`.

## Gap analysis (ticket vs. current code)

| Rule / AC | Current state | Action |
|-----------|---------------|--------|
| Rotation does not extend the 7 day window | `IssueAsync` sets `ExpiresAt = now + 7d` on every rotation, so a session can live forever | **Gap.** Carry the original `ExpiresAt` into the successor (Task 1) |
| Reused token: old rejected AND newer token stops working | 10s grace window returns 401 without revoking the chain (KAN-17 Decision 10) | **Conflict.** Decision: remove the grace (your answer). Any replay revokes the chain (Task 2) |
| Session too old: sent to sign-in with "Your session has expired, please sign in again" | Interceptor redirects with `returnUrl` only, no message | **Gap** (Task 3) |
| Silent renewal, single refresh for parallel calls, deactivated user, hashed storage, rotation | Implemented and tested in KAN-17 | Regression tests only where noted |

## Decisions (my calls)

1. **Absolute expiry:** `RotateAsync` passes `stored.ExpiresAt` to a private issue helper; `IssueAsync` (sign-in/register) keeps `now + RefreshTokenDays`. No schema change.
2. **Grace removed, concurrent claim race unchanged:** two requests that truly race on the same token still resolve in the atomic claim (loser gets 401, no chain revoke). Any request that arrives after the winner commits is a replay and revokes the chain. Multi-tab race can therefore force a re-sign-in; accepted per your answer. `RotationGrace` and its test are deleted.
3. **Message trigger:** the interceptor adds `reason=expired` to the sign-in redirect only when a refresh fails after the app had a session. A visitor who never signed in does not see the message. Sign-in page shows it as the form error banner and leaves it when the query param is absent.
4. **Deactivated user:** refresh already returns 401 for inactive users, so they can work until the current access token expires (15 min) and are then sent to sign-in with the expired-session message. No extra revoke on deactivation (out of scope).

## AC map

| AC | Task | Test |
|----|------|------|
| Silent renewal: action succeeds without sign-in page | existing + 4 | `refreshes once on 401 and replays the request with the new token` (exists); `parallel 401s share one refresh and all succeed` |
| Several requests at once: one refresh, all succeed | 4 | `parallel 401s share one refresh and all succeed` (real `AuthService`); `two concurrent refresh calls hit the API once` (exists) |
| Reused refresh token: rejected, newer token also dead | 2 | `Refresh_reuse_revokes_chain`; `Immediate_replay_revokes_the_new_token` (replaces grace test) |
| Session too old (7 days): sign-in with message | 1, 3 | `Refresh_does_not_extend_the_original_seven_day_window`; `Refresh_after_seven_days_returns_401`; `interceptor redirects with reason=expired when refresh fails`; `SignInPage shows the expired session message` |
| Deactivated while signed in: out after access token expires | existing + 3 | `Refresh_inactive_user_returns_401` (exists); `interceptor redirects with reason=expired when refresh fails` |
| Rule: hashed storage | existing | `TokenServiceTests` (exists); re-run only |
| Rule: rotation, old token stops working | existing | `Refresh_rotates_and_revokes_old_token` (exists) |

## Files

- Infrastructure: `Auth/TokenService.cs`
- Tests: `Api.Tests/Auth/RefreshEndpointTests.cs`
- Client: `core/auth.interceptor.ts` (+ spec), `features/auth/sign-in-page.ts` (+ spec)

## Tasks

### Task 1: Fixed 7 day window (API, KAN-66)

- [ ] Add tests in `RefreshEndpointTests`: `Refresh_does_not_extend_the_original_seven_day_window` (sign in at T, advance clock 3 days, refresh, new row `ExpiresAt == T+7d`); `Refresh_after_seven_days_returns_401` (advance 7d+1s from sign in, even with a chain of earlier rotations)
- [ ] Run `dotnet test src/api`, expect FAIL on the first
- [ ] `TokenService`: extract issue helper taking `refreshExpires`; `IssueAsync` passes `now + days`, `RotateAsync` passes `stored.ExpiresAt`
- [ ] Run `dotnet test src/api`, expect PASS
- [ ] Commit `KAN-18: keep refresh window fixed from sign in`

### Task 2: Reuse always revokes the chain (API, KAN-66)

- [ ] Replace `Replaying_a_just_rotated_token_inside_the_grace_window_does_not_kill_the_session` with `Immediate_replay_revokes_the_new_token` (rotate, replay old at the same instant, assert 401 and the newer token also gets 401)
- [ ] Run, expect FAIL
- [ ] Delete `RotationGrace` and the grace branch in `RotateAsync`
- [ ] Run `dotnet test src/api`, expect PASS (`Refresh_concurrent_same_token_only_one_succeeds` must still pass)
- [ ] Commit `KAN-18: revoke chain on any refresh token reuse`

### Task 3: Expired session message (client, KAN-68)

- [ ] Specs: interceptor test `redirects with reason=expired when refresh fails` (update the existing "signs out and goes to sign in" expectation to `{ returnUrl, reason: 'expired' }`); `SignInPage shows the expired session message` when `?reason=expired`, and no message without it
- [ ] Run `cd src/client && npm test`, expect FAIL
- [ ] Interceptor adds `reason: 'expired'`; `SignInPage` initialises `error` to "Your session has expired, please sign in again" when the param is present
- [ ] Run `npm test && npm run lint`, expect PASS
- [ ] Commit `KAN-18: show expired session message on sign in`

### Task 4: Parallel 401s share one refresh (client, KAN-67)

- [ ] Add interceptor spec using the real `AuthService`: three simultaneous requests get 401, exactly one `POST /auth/refresh` is sent, all three are replayed with the new token and succeed
- [ ] Run; if it passes it is a regression guard, otherwise fix `refresh()` / interceptor
- [ ] Run `npm test && npm run lint`
- [ ] Commit `KAN-18: cover parallel refresh in interceptor`

### Task 5: Verify and finish

- [ ] `dotnet build src/api`, `dotnet test src/api`, `npm test`, `npm run lint`; paste real output
- [ ] `stacktrack-reviewer` until READY; re-check each AC
- [ ] `notion-doc-writer` Feature log entry, then PR `KAN-18: As a user, I want to stay signed in so that I'm not interrupted while working`, Jira comment, move to In Review

## Delegation

Tasks 1-2 (API) go to `api-cqrs-implementer`; Tasks 3-4 (client) go to `angular-feature-builder`. They touch disjoint folders, so they can run in parallel worktrees.

## Review Focus

- Successor token never has a later `ExpiresAt` than its predecessor
- No leftover grace code or test
- Expired message only after a real session was lost
