# KAN-19 Sign out

> **For agentic workers:** use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** A signed-in user signs out from a user menu; the current session's refresh token is revoked on the server, other devices stay signed in, and the browser keeps no tokens. Sign out works even when the API is down.

**Architecture:** KAN-16/17 already built `POST /auth/sign-out` (`SignOutCommand` -> `TokenService.RevokeAsync`, cookie cleared), `AuthService.signOut()` (clears session in `finalize`, so it works on API failure) and a toolbar "Sign out" button in `app.html`. Gaps vs. KAN-19: sign-out is a plain button, not in a user menu; the "other devices" and "API down" and "back button" scenarios have no test. No new packages (`MatMenuModule` ships with the installed Angular Material), no migration.

**Spec:** Jira KAN-19 (subtasks KAN-69 sign out API, KAN-70 sign out from the user menu), `CLAUDE.md`, `docs/plans/KAN-17.md`, `docs/plans/KAN-18.md`.

## Gap analysis (ticket vs. current code)

| Rule / AC | Current state | Action |
|-----------|---------------|--------|
| Sign out revokes current refresh token | `RevokeAsync` + test `SignOut_revokes_token_and_clears_cookie` | Existing; re-run |
| Old refresh token -> 401 | `Refresh_after_sign_out_returns_401` | Existing; re-run |
| Other devices stay signed in | `RevokeAsync` revokes one row only, but no test | **Test gap** (Task 1) |
| Click "Sign out" from the **user menu** | Plain toolbar button | **Gap** (Task 2) |
| Back button shows no boards | `authGuard` on `/boards` returns the sign-in URL tree once `user()` is null; untested | **Test gap** (Task 3) |
| Browser keeps no tokens | Access token in memory only, cookie cleared by API, `signed-in` flag removed in `finalize` | Assert in Task 3 |
| API down: still signed out locally, taken to sign in | `finalize` clears session; `App.signOut` navigates in `error` | Test gap (Task 2) |

## Decisions (my calls, plus your answers)

1. **User menu (your answer):** replace the avatar/name span and the "Sign out" button with one button (avatar + display name) opening a `MatMenu` whose item "Sign out" calls `signOut()`. No new package.
2. **History (your answer):** keep `navigateByUrl('/auth/sign-in')` (push). Back goes to `/boards`, where `authGuard` redirects to sign-in. Proven by a router test, not a code change.
3. **No API change expected (client service changed after review, see Files):** if Task 1 tests pass on first run they are regression guards, which is the point. Fix `TokenService` only if one fails.
4. **Sign-out does not set `sessionExpired`:** unchanged, so the redirect shows no expired message.

## AC map

| AC | Task | Test |
|----|------|------|
| Sign out from user menu -> sign-in page | 2 | `opens the user menu and signs out through the API, then goes to sign in` |
| Back button shows no boards | 3 | `after sign out, navigating back to /boards redirects to sign in` (real router + `authGuard`) |
| Old refresh token -> 401 | existing | `Refresh_after_sign_out_returns_401` (re-run only) |
| Other devices still signed in | 1 | `SignOut_leaves_another_session_signed_in` |
| API down: signed out locally, sign-in page | 2 | `still signs out and goes to sign in when the API is unreachable` |
| Rule: revokes current session token | existing | `SignOut_revokes_token_and_clears_cookie` |
| Rule: browser keeps no tokens | 3 | `signOut clears session and the signed-in flag` (extends existing service spec; asserts `localStorage` key removed) |

## Files

- Tests: `Api.Tests/Auth/RefreshEndpointTests.cs`
- Client: `app.html`, `app.ts`, `app.spec.ts`, `app.routes.spec.ts`, `features/auth/auth.service.ts` and its spec
- Added after review: `AuthService.signOut()` clears the session at once, waits (max 5s) for an in-flight refresh so the rotated cookie is the one revoked, and refreshes from before a sign out never touch a later session (generation counter)

## Tasks

### Task 1: Other sessions survive sign out (API, KAN-69)

- [ ] Test in `RefreshEndpointTests`: `SignOut_leaves_another_session_signed_in`. Register, sign in again as the same user (second token), sign out with the first token, assert `Refresh(second)` is 200 and `Refresh(first)` is 401
- [ ] Run `dotnet test src/api --filter SignOut_leaves`. Expect PASS (regression guard). If it fails, fix `TokenService.RevokeAsync` so it touches one row only
- [ ] Commit `KAN-19: cover sign out leaving other sessions alone`

### Task 2: Sign out from the user menu (client, KAN-70)

- [ ] Specs in `app.spec.ts`: replace the "sign out button" and "signs out through the API" tests with: menu trigger shows display name; click trigger, click `Sign out` menu item, flush `204`, expect `user()` null and `navigateByUrl('/auth/sign-in')`; second test flushes `status: 0` (API down) and expects the same result
- [ ] Run `cd src/client && npm test`, expect FAIL
- [ ] `App`: import `MatMenuModule`; `app.html` trigger `<button mat-button [matMenuTriggerFor]="menu" class="user">` with avatar and name, `<mat-menu #menu>` with `<button mat-menu-item class="sign-out" (click)="signOut()">Sign out</button>`; adjust `app.scss` only for the trigger
- [ ] Run `npm test && npm run lint`, expect PASS
- [ ] Commit `KAN-19: move sign out into the user menu`

### Task 3: Back button and no leftover state (client, KAN-70)

- [ ] Spec (real router with `authGuard` on a `boards` route and `guestGuard` sign-in route): sign in, go to `/boards`, sign out, `router.navigateByUrl('/boards')` (what back does) lands on `/auth/sign-in?returnUrl=%2Fboards` with no `reason`
- [ ] Extend `signOut posts to the API and clears the session` in `auth.service.spec.ts`: after sign out, `localStorage.getItem('stacktrack.signed-in')` is null
- [ ] Run `npm test`. Expect PASS (guards). Fix only if red
- [ ] Commit `KAN-19: cover back navigation after sign out`

### Task 4: Verify and finish

- [ ] `dotnet build src/api`, `dotnet test src/api`, `npm test`, `npm run lint`; paste real output
- [ ] `stacktrack-reviewer` until READY; re-check each AC
- [ ] `notion-doc-writer` Feature log entry, then PR `KAN-19: As a user, I want to sign out so that nobody else can use my account on this device`, Jira comment, move to In Review

## Delegation

Task 1 (API) is small; Tasks 2-3 (client) to `angular-feature-builder`. Disjoint folders, so Task 1 can run inline while the agent works on the client in its worktree.

## Review Focus

- Sign-out item reachable only through the menu, keyboard accessible (MatMenu default)
- No change to cookie or revoke behavior
- No new dependency
