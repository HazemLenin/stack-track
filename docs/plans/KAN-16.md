# KAN-16 Sign up

> **For agentic workers:** use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** A visitor can register with display name, email and password, is signed in automatically, and lands on "My boards".

**Architecture:** CQRS command `Register` in `Application/Auth` with a FluentValidation validator. Handler talks to small Application-layer interfaces (`IUserRepository`, `IPasswordHasher`, `ITokenService`) implemented in Infrastructure, because Application may only reference Domain (enforced by `ArchitectureTests`). The token service issues the access JWT and a refresh token; KAN-17 reuses it. `AuthController` only calls `Send()`. Client: standalone sign-up page, `AuthService` with a session signal, placeholder "My boards" page, toolbar shows the user.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core/Npgsql (installed), Angular 21 + Material (installed), xUnit + FluentAssertions + Testcontainers, vitest.

**Spec:** Jira KAN-16 (+ subtasks KAN-61 API, KAN-62 page), `CLAUDE.md`, `docs/erd.md`, approved mockup option A (violet, pill buttons, filled inputs, floating card). KAN-15 (persistence) is a prerequisite and is not merged to `main` yet, so this branch is cut from `feature/KAN-15-ef-core-persistence`. KAN-17 builds on this branch.

## Global Constraints

- Display name 2 to 50 chars, trimmed. Password at least 8 chars with one uppercase, one lowercase, one digit
- New accounts are always role `Member`. Any `role` in the request body is ignored
- Email matched case-insensitively. Stored lowercased and trimmed (DB already has a unique index on `lower(email)`)
- Out of scope: email verification, CAPTCHA
- Controllers hold no logic. No new packages without approval. Do not edit existing migrations
- Tests: real Postgres via Testcontainers, no EF InMemory
- Commits: `KAN-16: imperative message`

## Decisions (my calls)

1. **JWT library needs a package.** Creating JWTs needs `Microsoft.IdentityModel.JsonWebTokens` (or `Microsoft.AspNetCore.Authentication.JwtBearer`); neither is in the shared framework. **Needs your OK.** Recommended: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.13 in `StackTrack.Api`/`Infrastructure`, which also lets later tickets add `[Authorize]`. Fallback: hand-rolled HS256 in about 25 lines.
2. **Password hashing:** `PasswordHasher<User>` from `Microsoft.Extensions.Identity.Core` (in the ASP.NET shared framework, so a `FrameworkReference` in Infrastructure, no package), wrapped by `IPasswordHasher`.
3. **Response shape** for register and later sign-in: `AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserDto User)`, `UserDto(Guid Id, string Email, string DisplayName, string Role)`. The refresh token is never in the body; it is set as a cookie (Decision 6).
4. **Duplicate email** returns `409` ProblemDetails with `detail` "An account with this email already exists". New `ConflictException` in Application, mapped in `GlobalExceptionHandler`. A unique-index race (23505) maps to the same result.
5. **Access token:** 15 min, claims `sub`, `email`, `name`, `role`. Config under `Jwt` (`Secret`, `Issuer`, `Audience`, `AccessTokenMinutes`, `RefreshTokenDays`). Dev secret in `appsettings.Development.json`; prod from environment.
6. **Refresh token:** 32 random bytes, base64url; only the SHA-256 hash is stored in `refresh_token` (7 days). Delivered as cookie `refresh_token`: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`, 7 day expiry. Script on the page can never read it. `ITokenService.IssueAsync` returns the raw token separately so the controller sets the cookie (`IssuedTokens(AuthResponse Response, string RefreshToken)`).
7. **Client token storage:** nothing in `localStorage`/`sessionStorage`. The access token lives only in memory (a signal). On page load the app calls `POST /api/auth/refresh` (cookie sent automatically) to restore the session before routing. The client is served same-site as the API (dev proxy `proxy.conf.json`), which `SameSite=Strict` requires; no CORS is added.
10. **Sign out:** `POST /api/auth/sign-out` revokes the presented refresh token and clears the cookie; the toolbar calls it, then drops the in-memory session. Included here because without it a reload would restore the session.
8. **Routes:** `/auth/sign-up`, `/auth/sign-in` (KAN-17), `/boards` is "My boards". Replaces the `AuthPage` stub.
9. **Confirm password** is checked on the server too (`ConfirmPassword` must equal `Password`).

## AC map

| AC | Task | Test |
|----|------|------|
| Successful registration: account created, signed in, redirected to "My boards" | 2, 5, 6 | `Register_creates_member_and_returns_tokens` (API); `SignUpPage navigates to /boards on success` (client) |
| Email already used, case-insensitive: message shown, no second account | 2, 5 | `Register_duplicate_email_ignoring_case_returns_409_and_creates_no_row`; `SignUpPage shows duplicate-email message` |
| Weak password: which rule is unmet shown next to the field before submit | 1, 5 | `PasswordRules_*` validator theories; `SignUpPage marks unmet rules as the user types` |
| `role: "Admin"` in body is ignored | 2 | `Register_ignores_role_field_in_body` |
| Display name 2 to 50, trimmed | 1, 2 | `DisplayName_rules` theory, `Register_trims_display_name` |
| Auto sign-in lands on My boards | 6 | `BoardsPage shows My boards and display name in toolbar` |

## Review Focus

- Email with surrounding spaces or mixed case is stored trimmed and lowercased (`Register_normalizes_email`)
- Password and confirm mismatch returns 400 on `confirmPassword`
- Password stored hashed, never plain text (`Register_stores_hashed_password`)
- Two simultaneous registrations of the same email: one 201, one 409, no 500
- Response and logs never contain the password or hash
- Refresh cookie is `HttpOnly; Secure; SameSite=Strict; Path=/api/auth` (`Register_sets_refresh_cookie_with_security_flags`), and no refresh token appears in the JSON body
- After sign-out a reload does not restore the session (`SignOut_revokes_token_and_clears_cookie`)

## Files

- Create `Domain`: nothing (entities exist)
- Create `Application/Auth/`: `Register/RegisterCommand.cs` (command, validator, handler), `AuthResponse.cs`, `Abstractions.cs` (`IUserRepository`, `IPasswordHasher`, `ITokenService`), `Application/Errors/ConflictException.cs`
- Create `Infrastructure/Auth/`: `UserRepository.cs`, `PasswordHasher.cs`, `TokenService.cs`, `JwtOptions.cs`; modify `Infrastructure/DependencyInjection.cs`, `Infrastructure.csproj`
- Create `Api/Controllers/AuthController.cs`; modify `Api/Errors/GlobalExceptionHandler.cs`, `appsettings*.json`
- Tests `Api.Tests/Auth/`: `RegisterValidatorTests.cs`, `RegisterEndpointTests.cs`, `TokenServiceTests.cs`
- Client `features/auth/`: `auth.service.ts`, `sign-up-page.ts`, `password-rules.ts`, `auth.routes.ts` (modify); `features/boards/boards-page.ts` (modify); `app.ts`/`app.html` (toolbar user); `core/auth.interceptor.ts` (attach bearer)

## Tasks

### Task 1: Register validation

**Interfaces:** Produces `record RegisterCommand(string DisplayName, string Email, string Password, string ConfirmPassword) : IRequest<AuthResponse>` and `RegisterCommandValidator`. No `Role` property exists, so a `role` field in JSON is dropped by the binder.

- [ ] Write `RegisterValidatorTests`: theories for display name ("a" fails, 51 chars fails, " ab " passes after trim, 2 and 50 pass), email (empty, no `@`, over 320 fail), password ("Short1", "alllower1", "ALLUPPER1", "NoDigitsHere" each fail with a message naming the rule; "Valid1234" passes), confirm mismatch fails on `ConfirmPassword`
- [ ] Run `dotnet test src/api --filter RegisterValidatorTests`, expect FAIL (types missing)
- [ ] Implement command + validator in `Application/Auth/Register/RegisterCommand.cs`. Validate on the trimmed display name; messages: "At least 8 characters", "One uppercase letter", "One lowercase letter", "One digit" (same text as the UI)
- [ ] Run the tests, expect PASS
- [ ] Commit `KAN-16: validate register command`

### Task 2: Register handler, persistence adapters, endpoint

**Files:** as listed under Files. **Needs Decision 1 approved first.**

**Interfaces:**
- Produces `IUserRepository { Task<bool> EmailExistsAsync(string email, CT); Task<bool> TryAddAsync(User user, CT); }` (`TryAddAsync` returns false on unique violation), `IPasswordHasher { string Hash(string); bool Verify(string hash, string password); }`, `ITokenService { Task<AuthResponse> IssueAsync(User user, CT); }` (creates and stores the refresh token)
- Produces `ConflictException(string message)` mapped to 409

- [ ] Write `RegisterEndpointTests` (Testcontainers, `ApiFactory` extended to take a connection string and `Jwt:*` settings; migrate DB first): `Register_creates_member_and_returns_tokens` (201, user row has `Role = Member`, `IsActive`, access token decodes to claims `sub`/`email`/`name`/`role`, `exp` about 15 min), `Register_ignores_role_field_in_body` (post `role: "Admin"`), `Register_duplicate_email_ignoring_case_returns_409_and_creates_no_row` (`Sara@Example.com` after `sara@example.com`, detail text as in AC, user count stays 1), `Register_normalizes_email`, `Register_trims_display_name`, `Register_stores_hashed_password`, `Register_concurrent_same_email_yields_one_201_and_one_409`, `Register_invalid_returns_400_per_field`
- [ ] Run, expect FAIL
- [ ] Implement `RegisterCommandHandler`: normalize, `EmailExistsAsync` then `TryAddAsync`, either path false throws `ConflictException`; issue tokens. Infrastructure adapters, `JwtOptions` bound from config, DI registration, `AuthController.Register` (`POST /api/auth/register`, `[AllowAnonymous]`, returns `201`), handler mapping in `GlobalExceptionHandler`
- [ ] Add `TokenServiceTests`: refresh token stored as hash only, expiry 7 days
- [ ] Add `SignOutCommand` + `POST /api/auth/sign-out` (reads the cookie, revokes, deletes cookie; idempotent 204 even with no cookie) with the tests named in Review Focus
- [ ] Run `dotnet build src/api` and `dotnet test src/api`, expect all PASS
- [ ] Commit `KAN-16: add register endpoint`

### Task 3: Client auth service and session

**Interfaces:** Produces `AuthService` (`register(req): Observable<AuthResponse>`, `session: Signal<Session | null>` in memory only, `user: Signal<UserDto | null>`, `restore(): Promise<void>` called by an app initializer, `signOut(): Observable<void>`); auth interceptor attaches `Authorization: Bearer` to requests for the API base URL only and uses `withCredentials` for `/api/auth/*`.

- [ ] Write `auth.service.spec.ts`: register stores session in memory and sets `user`; `restore()` calls refresh and sets the session, and a 401 leaves it signed out without an error toast; `signOut` posts to sign-out and clears state; nothing is ever written to `localStorage` or `sessionStorage`
- [ ] Run `cd src/client && npm test`, expect FAIL
- [ ] Implement `auth.service.ts`, update `auth.interceptor.ts` (+ spec: header added for API url, not for other hosts)
- [ ] Run tests, expect PASS
- [ ] Commit `KAN-16: add client auth service`

### Task 4: Password rules helper

**Interfaces:** Produces `passwordRules(password: string): { id: 'len'|'up'|'lo'|'di'; label: string; met: boolean }[]` in `password-rules.ts` (same labels as the server).

- [ ] Write spec: each rule flips independently; empty string has all unmet
- [ ] Run, expect FAIL. Implement. Run, expect PASS
- [ ] Commit `KAN-16: add password rules helper`

### Task 5: Sign-up page

**Interfaces:** Consumes `AuthService.register`, `passwordRules`. Produces `SignUpPage` at `/auth/sign-up`. Matches mockup A (card, filled inputs, pill button, live rules list with ok/fail states).

- [ ] Write `sign-up-page.spec.ts`: rules show unmet state while typing a weak password and submit is blocked; confirm mismatch error; valid submit calls `register` with trimmed name and navigates to `/boards`; 409 response shows "An account with this email already exists" under the email field; submit button disabled while pending
- [ ] Run, expect FAIL. Implement standalone component with reactive forms and Material form fields, signals for state. Update `auth.routes.ts` (`sign-up`, redirect `''` to `sign-in` once KAN-17 lands)
- [ ] Run tests, expect PASS
- [ ] Commit `KAN-16: add sign-up page`

### Task 6: My boards placeholder and toolbar

- [ ] Write specs: `BoardsPage` renders heading "My boards" and an empty state; `App` toolbar shows the display name and a Sign out action when signed in, "Sign in" link when not
- [ ] Run, expect FAIL. Implement in `boards-page.ts`, `app.html`, `app.ts` (replace the "coming soon" menu)
- [ ] Run tests, expect PASS
- [ ] Commit `KAN-16: add My boards placeholder and toolbar user`

### Task 7: Verify and docs

- [ ] `dotnet build src/api`, `dotnet test src/api`, `cd src/client && npm test && npm run lint`; paste real output
- [ ] Add a "KAN-16 Sign up" entry to the Notion "Feature log" (ticket, what, where, how to try); update commands/layout sections if changed
- [ ] Run a code review against this plan and CLAUDE.md, fix findings
- [ ] Push, open PR `KAN-16: As a visitor, I want to create an account so that I can start organizing my work`, comment on Jira, transition to In Review (already In Progress)
