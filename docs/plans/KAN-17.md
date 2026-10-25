# KAN-17 Sign in

> **For agentic workers:** use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** A registered user signs in with email and password, gets a 15 minute access token and a 7 day refresh token, is protected from brute force, and is bounced to sign-in (then back) when opening protected pages.

**Architecture:** Commands `SignIn` and `RefreshToken` in `Application/Auth`, reusing `IUserRepository`, `IPasswordHasher` and `ITokenService` from KAN-16. Lockout state lives on `User`. Client adds a sign-in page, an auth guard with a return URL, and silent refresh in the interceptor.

**Tech Stack:** same as KAN-16. No new packages beyond the JWT decision made there.

**Spec:** Jira KAN-17 (+ subtasks KAN-63 API, KAN-64 page and session, KAN-65 guard), `CLAUDE.md`, approved mockup option A. **Depends on KAN-16**: this branch is stacked on the KAN-16 branch and its PR targets that branch until KAN-16 merges.

## Global Constraints

- Email + password sign-in. Access token 15 min, refresh token 7 days
- Email matching is case-insensitive
- Error messages must not reveal whether an email is registered
- Deactivated accounts cannot sign in
- After 5 failed attempts in a row the account is locked for 15 minutes
- Access token contains user id, email, display name and role
- Do not edit existing migrations (add a new one). No new packages. Tests use real Postgres
- Commits: `KAN-17: imperative message`

## Decisions (my calls)

1. **Lockout scope (your answer to Q3):** the locked message appears only for real accounts. Unknown emails always get "Email or password is incorrect" and no state is stored for them. Accepted small leak: a locked message confirms the account exists.
2. **Lockout columns:** `User.FailedLoginCount` (int, default 0) and `User.LockedUntil` (`DateTimeOffset?`), new migration `AddUserLockout`. The 5th wrong password sets `LockedUntil = now + 15 min`; a successful sign-in resets the count. While locked, even the correct password is rejected and the count does not grow. After the lock expires the next wrong attempt counts from 0.
3. **Check order:** unknown email -> generic 401. Known email: locked -> 423; wrong password -> count and generic 401 (the 5th returns the locked response); correct password but inactive -> 403 "Your account has been deactivated. Please contact an administrator."; else success. A wrong password on a deactivated account stays generic.
4. **Status codes:** 401 `Email or password is incorrect`, 403 deactivated, 423 locked with ProblemDetails extension `lockedUntil` (ISO UTC) and `detail` "This account is temporarily locked. Try again after {local time}" built client side from `lockedUntil`. New exceptions `InvalidCredentialsException`, `AccountDeactivatedException`, `AccountLockedException(DateTimeOffset until)` mapped in `GlobalExceptionHandler`.
5. **Timing:** unknown email still runs one password hash verify against a dummy hash, so response time does not reveal registration.
6. **Refresh (your answer to Q5):** `POST /api/auth/refresh` takes no body; it reads the `HttpOnly` `refresh_token` cookie set in KAN-16 and rotates: old token revoked with `ReplacedByTokenId` set, new access token in the body and a new cookie set. Sign-in also sets the cookie and returns no refresh token in the body. Expired, unknown or already revoked tokens return 401. A revoked token being replayed also revokes the whole chain of its replacements (reuse detection). User inactive at refresh time returns 401.
7. **Lockout race:** `ponytail:` count increment is read-modify-write on the user row; two parallel wrong guesses can undercount by one. Upgrade to an atomic `ExecuteUpdate` if it matters.
8. **Client refresh:** the access token is held in memory only (KAN-16 Decision 7). On a 401 from a non-auth request, the interceptor calls refresh once (cookie) and replays; on failure it clears the session and goes to sign-in with the return URL. Concurrent 401s share one refresh call. The startup `restore()` runs before the auth guard, so a reload on a protected page stays on it when the cookie is valid.

9. **Carried over from KAN-16:** `AuthService.restore()` (app initializer calling refresh), the refresh endpoint at `POST /auth/refresh` (unprefixed, see KAN-16 Decision 11), and the "Already have an account? Sign in" link on the sign-up page.

## AC map

| AC | Task | Test |
|----|------|------|
| Successful sign in: lands on My boards, display name in top bar | 2, 5 | `SignIn_returns_tokens_with_required_claims`; `SignInPage navigates to return URL or /boards`; toolbar spec from KAN-16 |
| Wrong password or unknown email: same message | 2, 5 | `SignIn_wrong_password_and_unknown_email_return_identical_401_body`; `SignInPage shows Email or password is incorrect` |
| Deactivated account: cannot sign in, specific message | 2, 5 | `SignIn_deactivated_with_correct_password_returns_403`; `SignInPage shows deactivated message` |
| Too many attempts: locked, shows when to retry | 1, 2, 5 | `Fifth_wrong_attempt_locks_for_15_minutes`; `Locked_account_rejects_correct_password`; `SignInPage shows locked message with time` |
| Protected pages redirect to sign-in and back after | 6 | `authGuard redirects to /auth/sign-in with returnUrl`; `SignInPage returns to returnUrl after sign in` |
| Access token has id, email, display name, role; 15 min | 2 | `SignIn_returns_tokens_with_required_claims` (also checks `exp`) |
| Refresh token 7 days | 3 | `Refresh_rotates_and_revokes_old_token` |
| Email case-insensitive | 2 | `SignIn_email_is_case_insensitive` |

## Review Focus

- Unknown and wrong-password responses are indistinguishable in status, body and (roughly) timing
- Lock expires after 15 minutes; a success before the 5th failure resets the counter
- Wrong passwords during a lock do not extend it
- Replayed refresh token (reuse) is rejected and kills the chain
- A user deactivated after sign-in cannot refresh
- Return URL must be same-origin and relative (no open redirect to `//evil.com`)

## Files

- Create `Domain`: modify `User.cs` (`FailedLoginCount`, `LockedUntil`)
- Infrastructure: modify `UserConfiguration.cs`, new migration `AddUserLockout`, extend `UserRepository` and add `RefreshTokenRepository`
- Application `Auth/SignIn/SignInCommand.cs`, `Auth/Refresh/RefreshTokenCommand.cs`, exceptions in `Application/Errors`
- Api: `AuthController` (`sign-in`, `refresh`), `GlobalExceptionHandler`
- Tests `Api.Tests/Auth/SignInEndpointTests.cs`, `RefreshEndpointTests.cs`; `Persistence/` lockout migration test
- Client: `sign-in-page.ts`, `auth.guard.ts`, `auth.service.ts` (`signIn`, `refresh`), `auth.interceptor.ts`, `app.routes.ts`, `auth.routes.ts`

## Tasks

### Task 1: Lockout columns and migration

**Interfaces:** Produces `User.FailedLoginCount: int`, `User.LockedUntil: DateTimeOffset?`.

- [ ] Write a persistence test: new migration applies on an empty DB, new users default to `FailedLoginCount = 0` and `LockedUntil = null`, `LockedUntil` round-trips a `+03:00` value as the same instant
- [ ] Run, expect FAIL. Add properties, configuration, then `dotnet ef migrations add AddUserLockout -p src/api/StackTrack.Infrastructure -s src/api/StackTrack.Api`
- [ ] Run `dotnet test src/api`, expect PASS
- [ ] Commit `KAN-17: add user lockout columns`

### Task 2: Sign-in endpoint

**Interfaces:** Consumes KAN-16 `IUserRepository`, `IPasswordHasher`, `ITokenService`. Produces `SignInCommand(string Email, string Password) : IRequest<AuthResponse>`; repository gains `FindByEmailAsync(string email, CT)` and `SaveAsync(CT)`; `TimeProvider` is injected for lock timing.

- [ ] Write `SignInEndpointTests` (Testcontainers, fake `TimeProvider`): every row of the AC map above for this task, plus `SignIn_success_resets_failed_count`, `Wrong_passwords_while_locked_do_not_extend_lock`, `Lock_expires_after_15_minutes`, `SignIn_validation_empty_fields_return_400`
- [ ] Run, expect FAIL
- [ ] Implement handler in the order from Decision 3, dummy-hash verify for unknown email, controller `POST /api/auth/sign-in`, exception mapping with `lockedUntil` extension
- [ ] Run `dotnet test src/api`, expect PASS
- [ ] Commit `KAN-17: add sign-in endpoint with lockout`

### Task 3: Refresh endpoint

**Interfaces:** Produces `RefreshTokenCommand(string RefreshToken) : IRequest<IssuedTokens>` (the controller reads the value from the cookie); `IRefreshTokenRepository { FindByHashAsync; RevokeChainAsync }`. Tests read and send the cookie through the `HttpClient` cookie container.

- [ ] Write `RefreshEndpointTests`: `Refresh_rotates_and_revokes_old_token` (old row `IsRevoked`, `ReplacedByTokenId` set, new token expires in 7 days), `Refresh_unknown_expired_or_revoked_returns_401`, `Refresh_reuse_revokes_chain`, `Refresh_inactive_user_returns_401`, `Refresh_concurrent_same_token_only_one_succeeds`
- [ ] Run, expect FAIL. Implement handler, repository, controller `POST /api/auth/refresh`
- [ ] Run `dotnet test src/api`, expect PASS
- [ ] Commit `KAN-17: add refresh token endpoint`

### Task 4: Client sign-in and refresh in AuthService

**Interfaces:** Produces `AuthService.signIn(email, password)`, `AuthService.refresh(): Observable<AuthResponse>` (single in-flight).

- [ ] Write specs: signIn stores session; refresh replaces tokens; two concurrent refresh calls hit the API once; failed refresh clears the session
- [ ] Run, expect FAIL. Implement. Run, expect PASS
- [ ] Commit `KAN-17: add sign-in and refresh to auth service`

### Task 5: Sign-in page

**Interfaces:** Produces `SignInPage` at `/auth/sign-in`, matching mockup A, banners for 401, 403 and 423.

- [ ] Write `sign-in-page.spec.ts`: success navigates to a safe `returnUrl` query param or `/boards`; unsafe return URLs (`//evil.com`, `https://evil.com`, `javascript:`) fall back to `/boards`; 401 shows "Email or password is incorrect"; 403 shows the deactivated text; 423 shows the lock message with the retry time from `lockedUntil`; submit disabled while pending
- [ ] Run, expect FAIL. Implement. Run, expect PASS
- [ ] Commit `KAN-17: add sign-in page`

### Task 6: Auth guard and redirect rules

**Interfaces:** Produces `authGuard: CanActivateFn` redirecting to `/auth/sign-in?returnUrl=<url>`; `guestGuard` sending signed-in users away from sign-in/sign-up to `/boards`.

- [ ] Write specs: guard allows with a session, redirects without one and keeps the full URL with query; interceptor refreshes on 401 once and replays, signs out and redirects when refresh fails, never retries `/api/auth/*` calls; route table applies `authGuard` to `boards`, `admin`, `my-cards` (not the 404 route or `auth`)
- [ ] Run, expect FAIL. Implement guards, interceptor refresh, route changes (`''` redirects to `boards`)
- [ ] Run `cd src/client && npm test && npm run lint`, expect PASS
- [ ] Commit `KAN-17: guard protected routes with return URL`

### Task 7: Verify and docs

- [ ] `dotnet build src/api`, `dotnet test src/api`, `npm test`, `npm run lint`; paste real output
- [ ] Notion "Feature log" entry for KAN-17 (sign-in, refresh, lockout, guard; how to try); update conventions section with the exception-to-status mapping
- [ ] Code review against this plan and CLAUDE.md; fix findings
- [ ] Push, PR `KAN-17: As a user, I want to sign in with my email and password so that I can access my boards` (base: KAN-16 branch until it merges), comment on Jira, transition to In Review (already In Progress)
