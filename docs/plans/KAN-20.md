# KAN-20 Roles: Admin and User

**Goal:** Server-enforced global roles. Anonymous gets 401 everywhere except sign up/in/refresh/sign-out/health; `/admin/*` API is Admin only (403 for Member); the client hides the Admin area from Members; Admins get read-only access to boards they are not members of.

**Spec:** Jira KAN-20 (subtasks KAN-71 API policies, KAN-72 Angular role guard, KAN-73 admin read-only board access), `CLAUDE.md`.

**Branch:** `feature/KAN-20-roles` off `main`. KAN-21 and KAN-22 stack on this branch (PR bases chain 20 -> 21 -> 22).

## Gap analysis (ticket vs. current code)

| Rule / AC | Current state | Action |
|-----------|---------------|--------|
| Anonymous -> 401 | `Program.cs` has `UseAuthorization()` only. `JwtBearer` is installed (Infrastructure csproj) but **no authentication is wired**, no `[Authorize]` anywhere | Wire JWT bearer + fallback policy (Task 1) |
| Role checked on server | Token already carries `role` claim (`TokenService`) | Map `role` claim, add `Admin` policy (Task 1) |
| `/api/admin/*` -> 403 for User | No admin controller | `AdminControllerBase` (`[Route("admin")]`, Admin policy) (Task 2) |
| Role change effective at next session renewal | Role is read from DB on every refresh (`RotateAsync` -> `IssueAsync(user)`) | Test only (Task 2) |
| Admin read-only on non-member boards | No board access service | `IBoardAccess` returning None / ReadOnly / Full (Task 3) |
| User visits `/admin/users` -> "My boards"; no Admin link | `/admin` route is only `authGuard`; no link | `adminGuard`, link shown to Admin only (Tasks 4-5) |

## Decisions (my calls, flag if wrong)

1. **"User" in the ticket = `UserRole.Member`** in code (CLAUDE.md: roles Admin, Member). No rename, no migration.
2. **No `/api` prefix:** the API has no prefix today (`auth/...`), so the admin area is `admin/...`; the client's `apiBaseUrl` supplies any prefix.
3. **Admin-only by convention:** abstract `AdminControllerBase` carries `[Authorize(Policy = "Admin")]`; all admin controllers (KAN-21) inherit it, so a new admin endpoint is protected by default.
4. **No production admin endpoint in this ticket:** the 403 test uses a test-only controller in `Api.Tests` deriving from `AdminControllerBase`.
5. **401 vs 403 bodies** come from default ASP.NET (`UseStatusCodePages` already renders problem details).
6. **Access token keeps its role until it expires** (`AccessTokenMinutes`); a role change lands at next refresh, which satisfies the rule.
7. `IBoardAccess` here only answers "what level does this user have on this board". Pipeline behavior, child-resource resolution and 404 mapping are KAN-22.

## AC map

| AC | Task | Test |
|----|------|------|
| User opens `/admin/users` -> My boards, no Admin link | 4, 5 | `adminGuard redirects a Member to /boards`; `app.spec` `hides the Admin link for a Member` |
| User calls admin API -> 403 | 1, 2 | `Admin_route_returns_403_for_member` |
| Admin views non-member board read only | 3 | `Admin_who_is_not_a_member_gets_read_only`; `Member_gets_full`; `Non_member_gets_none` |
| Anonymous -> 401 except sign up/in/refresh/health | 1 | `Protected_endpoint_returns_401_without_token`; `Public_endpoints_stay_open` (register, sign-in, refresh, sign-out, health) |
| Rule: role change applies at next renewal | 2 | `Role_change_shows_in_next_refresh_token` |

## Files

- API: `Infrastructure/DependencyInjection.cs` (AddAuthentication/JwtBearer + policies), `Api/Program.cs` (`UseAuthentication`), `Api/Controllers/AdminControllerBase.cs`, `Application/Auth/ICurrentUser.cs` + `Infrastructure/Auth/CurrentUser.cs`, `Application/Boards/IBoardAccess.cs` + `Infrastructure/Boards/BoardAccess.cs`, `[AllowAnonymous]` on `AuthController` actions
- Tests: `Api.Tests/Auth/AuthorizationTests.cs`, `Api.Tests/Boards/BoardAccessTests.cs`, `Api.Tests/TestSupport` (test-only admin controller, helper to sign in as role)
- Client: `core/auth.guard.ts` (+ spec), `app.routes.ts`, `app.html` / `app.ts` (+ spec), `features/admin/admin.routes.ts`

## Tasks

### Task 1: Authentication and fallback policy (KAN-71)
- [ ] Tests: unauthenticated GET to a test-only protected route -> 401; register, sign-in, refresh, sign-out, `/health` reachable without a token; valid Bearer token -> 200
- [ ] Impl: `AddAuthentication().AddJwtBearer` with `JwtOptions` issuer/audience/key, `MapInboundClaims = false`, `RoleClaimType = "role"`; `FallbackPolicy = RequireAuthenticatedUser`; `AddPolicy("Admin", RequireRole("Admin"))`; `[AllowAnonymous]` on `AuthController`; `UseAuthentication()` before `UseAuthorization()`
- [ ] Commit `KAN-20: require authentication on every endpoint except auth and health`

### Task 2: Admin policy (KAN-71)
- [ ] Tests: Member -> 403, Admin -> 200 on test-only `AdminControllerBase` controller; role promoted in DB -> next `refresh` returns `role: Admin`
- [ ] Impl: `AdminControllerBase`
- [ ] Commit `KAN-20: protect admin routes with the Admin policy`

### Task 3: Read-only board access for admins (KAN-73)
- [ ] Tests against Testcontainers Postgres: owner/member -> Full, Admin non-member -> ReadOnly, Admin member -> Full, other Member -> None, unknown board -> None
- [ ] Impl: `ICurrentUser`, `IBoardAccess.GetAsync(boardId, ct)` -> `BoardAccessLevel`
- [ ] Commit `KAN-20: add board access levels with admin read only`

### Task 4: Role guard (KAN-72)
- [ ] Spec: `adminGuard` returns `/boards` UrlTree for Member, true for Admin; router test `/admin/users` as Member lands on `/boards`
- [ ] Impl: `adminGuard` in `core/auth.guard.ts`; `app.routes.ts` admin route `canActivate: [authGuard, adminGuard]`; `admin.routes.ts` add `users` path and redirect `''` -> `users`
- [ ] Commit `KAN-20: send non admins away from the admin area`

### Task 5: Admin navigation (KAN-72)
- [ ] Spec: Admin sees `Admin` link in toolbar, Member does not
- [ ] Impl: `isAdmin` computed in `App`, `@if` link to `/admin/users` in `app.html`
- [ ] Commit `KAN-20: show the Admin link to admins only`

## Delegation
API Tasks 1-3 -> `api-cqrs-implementer`; client Tasks 4-5 -> `angular-feature-builder`. Independent, run in parallel worktrees, then merge.
