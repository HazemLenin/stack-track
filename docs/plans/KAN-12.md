# KAN-12 Scaffold Angular 21 frontend app shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Empty-but-working Angular 21 client: lazy routed feature areas, 404 page, env-configurable API base URL with dev proxy, interceptor placeholder, global HTTP error toasts, Material app shell.

**Architecture:** Standalone components, signals, no NgModules. `app.routes.ts` lazy-loads four feature areas via `loadChildren`. Two functional interceptors (`authInterceptor` passthrough placeholder, `errorInterceptor` -> `ToastService` -> `MatSnackBar`). Shell is a root component with `mat-toolbar` + `router-outlet`.

**Tech Stack:** Angular 21 (`@angular/cli@21`), Angular Material 21 (M3 theme), Vitest (Angular 21 default test runner), angular-eslint.

**Spec:** Jira KAN-12 (+ subtasks KAN-50..53) and the decisions answered by the user in this session. Conventions: CLAUDE.md.

## Global Constraints

- Location: `src/client` (NOT `/frontend`; ticket text is outdated, user confirmed). Subtasks all ship in one PR.
- Angular 21: generate with `npx @angular/cli@21 new`, never the latest CLI (22.x). Standalone components, signals, no NgModules. Prefix `app`, style `scss`, routing on, SSR off, zoneless default.
- Commands must exist in `src/client/package.json`: `start`, `build`, `test`, `lint` (`npm start` serves `http://localhost:4200`).
- **New packages need approval (CLAUDE.md). User approved Angular Material, conditional: must NOT look dated/"2013 Android tablet".** => M3 theme, custom primary palette, rounded/flat toolbar, no default heavy-shadow Material 2 look. Approved list only: `@angular/material`, `@angular/cdk` (21.x), `angular-eslint` (+ its `eslint`/`typescript-eslint` deps, required for AC `npm run lint`). Nothing else.
- Feature areas (exact route paths): `auth`, `boards`, `admin`, `my-cards`. Each `src/client/src/app/features/<name>/<name>.routes.ts` + one placeholder standalone component.
- API base URL: `environment.apiBaseUrl`, value `/api` in dev and prod files (prod behind same-origin reverse proxy is a later ticket). Exposed via injection token `API_BASE_URL`.
- Dev proxy: `src/client/proxy.conf.json` maps `/api` -> `http://localhost:5009`, `pathRewrite {"^/api": ""}` (API has no `/api` prefix; API untouched).
- Error UX: toast only, page stays rendered. Status `0`/`502`/`503`/`504` -> "Can't reach the server. Please try again in a moment." ; other `>=500` -> "Something went wrong on our side. Please try again." ; other 4xx -> ProblemDetails `detail` ?? `title` if string, else "The request could not be completed."; 401 is left to the Authentication epic (no toast). Never show raw `HttpErrorResponse` text. Error is always re-thrown.
- Do not edit `src/api`, docs outside plan/Notion. Commits: `KAN-12: message`. Branch `feature/KAN-12-frontend-scaffold` in a new worktree.

## Review Focus

- API down (status 0) -> friendly toast, no unhandled error, app still rendered.
- Two failing calls at once -> two toasts do not stack unbounded (ToastService replaces the open one).
- Problem Details body with non-string `detail`/HTML body -> generic message, never raw JSON/HTML.
- Unknown nested route (`/boards/xyz/nope`, `/admin/zzz`) -> "Page not found", not blank.
- `/` redirects to `boards`; `authInterceptor` must not alter requests (placeholder).
- Prod build (`ng build`) uses environment file correctly (budget warnings are errors? keep default budgets, build must have 0 errors).

## AC map

| AC | Task | Test / check |
|----|------|--------------|
| `npm start` serves on :4200 | 1 | manual: `npm start`, `curl -sI http://localhost:4200` -> 200 |
| `/api/*` proxied to local API | 2 | manual with API running: `curl -i http://localhost:4200/api/health` -> 200 + `X-Correlation-Id`; unit: `proxy.conf.json` shape test |
| Unknown route -> "Page not found" | 3 | `routes.spec.ts` (router harness) |
| API down -> friendly message | 4 | `error.interceptor.spec.ts` status 0; manual: stop API, call -> toast |
| `npm run lint` and `npm run build` pass | 1-6 | run in task 6 and after every task |
| Lazy features, shell, interceptor placeholder, Material themed (requirements) | 3, 4, 5 | `routes.spec.ts`, `auth.interceptor.spec.ts`, `app.spec.ts` |

Subtask map: KAN-50 = tasks 1+3, KAN-51 = task 2, KAN-52 = task 5, KAN-53 = task 4.

---

### Task 1: Generate app, lint, runner (KAN-50)

**Files:** Create everything under `src/client` (remove `src/client/.gitkeep`).

- [ ] **Step 1:** From `src/client`'s parent run `npx @angular/cli@21 new client --directory src/client --routing --style=scss --ssr=false --skip-git --prefix app --defaults` (Vitest is the default runner; keep it). Confirm `package.json` has `@angular/core` `^21`.
- [ ] **Step 2:** `cd src/client && npx ng add angular-eslint --skip-confirmation`; confirm script `lint` exists in `package.json` (add `"lint": "ng lint"` if the schematic did not).
- [ ] **Step 3:** Verify: `npm run build`, `npm run lint`, `npm test -- --watch=false` -> all exit 0 (generated spec for `App` passes). Verify `npm start` serves 200 on :4200, then stop it.
- [ ] **Step 4: Commit** `KAN-12: generate Angular 21 app with eslint`

### Task 2: Environment config + dev proxy (KAN-51)

**Files:**
- Create: `src/client/src/environments/environment.ts`, `environment.development.ts`, `src/client/src/app/core/api-base-url.ts`, `src/client/src/app/core/api-base-url.spec.ts`, `src/client/proxy.conf.json`, `src/client/src/app/core/proxy-config.spec.ts`
- Modify: `src/client/angular.json` (`fileReplacements` for development config, `proxyConfig` in serve options), `src/client/src/app/app.config.ts`

**Interfaces:**
- Produces: `export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', { providedIn: 'root', factory: () => environment.apiBaseUrl })` in `core/api-base-url.ts`. `environment = { apiBaseUrl: '/api' }`.

- [ ] **Step 1: Failing tests.** `api-base-url.spec.ts`: `TestBed.inject(API_BASE_URL)` equals `'/api'`; overridable via `{provide: API_BASE_URL, useValue: 'http://x'}`. `proxy-config.spec.ts`: import `proxy.conf.json` (resolveJsonModule) and assert `['/api'].target === 'http://localhost:5009'`, `pathRewrite['^/api'] === ''`, `changeOrigin === true`.
- [ ] **Step 2:** Run `npm test -- --watch=false` -> FAIL.
- [ ] **Step 3:** Implement files above; wire `proxyConfig` under `serve.options` and `fileReplacements` (`environment.ts` -> `environment.development.ts`) in the `development` build configuration.
- [ ] **Step 4:** Tests PASS; `npm run build` ok. Manual AC check: `docker compose up -d db`, run API (`dotnet run --project src/api/StackTrack.Api --launch-profile http`), `npm start`, `curl -i http://localhost:4200/api/health` -> 200 with `X-Correlation-Id`. Paste output.
- [ ] **Step 5: Commit** `KAN-12: add API base URL token and dev proxy`

### Task 3: Routing, lazy features, Page not found (KAN-50)

**Files:**
- Create: `src/client/src/app/app.routes.ts` (replace generated), `src/client/src/app/features/{auth,boards,admin,my-cards}/<name>.routes.ts` + `<name>-page.ts` (placeholder, inline template with the feature name in an `<h1>`), `src/client/src/app/features/not-found/not-found-page.ts`, `src/client/src/app/app.routes.spec.ts`

**Interfaces:**
- Produces: `export const routes: Routes` — `''` redirect (`pathMatch: 'full'`) to `boards`; `auth|boards|admin|my-cards` each `loadChildren: () => import('./features/<n>/<n>.routes').then(m => m.routes)`; `**` -> `NotFoundPage` (`<h1>Page not found</h1>` + link to `/`). `NotFoundPage` is eagerly declared via `loadComponent` is also fine; choose `loadComponent`.

- [ ] **Step 1: Failing `app.routes.spec.ts`** using `RouterTestingHarness` with `provideRouter(routes)`:
  - `navigateByUrl('/')` -> `router.url === '/boards'`, heading text `boards`.
  - Each of `/auth`, `/boards`, `/admin`, `/my-cards` renders its heading.
  - `/does/not/exist`, `/boards/xyz/nope`, `/admin/zzz` render "Page not found".
- [ ] **Step 2:** Run -> FAIL.
- [ ] **Step 3:** Implement per Interfaces (each feature route file: `export const routes: Routes = [{ path: '', component: XPage }]`).
- [ ] **Step 4:** Tests PASS; `npm run build` shows separate lazy chunks for the four features (paste chunk list).
- [ ] **Step 5: Commit** `KAN-12: add lazy feature routes and page not found`

### Task 4: Interceptors + global error toasts (KAN-53)

**Files:**
- Create: `src/client/src/app/core/toast.service.ts`, `core/auth.interceptor.ts`, `core/error.interceptor.ts`, `core/error-message.ts`, specs `core/error-message.spec.ts`, `core/error.interceptor.spec.ts`, `core/auth.interceptor.spec.ts`, `core/toast.service.spec.ts`
- Modify: `src/client/package.json` + `angular.json` (Material install: `npx ng add @angular/material@21 --skip-confirmation` with M3 theme chosen in Task 5; install here only the packages), `app.config.ts` (`provideHttpClient(withInterceptors([authInterceptor, errorInterceptor]))`, `provideAnimationsAsync` only if the CLI still requires it)

**Interfaces:**
- Produces:
  - `export function toUserMessage(e: HttpErrorResponse): string | null` in `error-message.ts` — implements the Error UX rules in Global Constraints; returns `null` for 401.
  - `@Injectable({providedIn:'root'}) class ToastService { show(message: string): void }` — wraps `MatSnackBar.open(message, 'Dismiss', { duration: 6000 })`; dismisses any open snackbar first so toasts don't stack.
  - `export const authInterceptor: HttpInterceptorFn` — returns `next(req)` unchanged; comment `// placeholder: attach access token in Authentication epic`.
  - `export const errorInterceptor: HttpInterceptorFn` — `catchError` -> `toast.show(msg)` when `toUserMessage` non-null -> `throwError(() => err)`.

- [ ] **Step 1: Failing tests**
  - `error-message.spec.ts`: status 0 -> "Can't reach the server. Please try again in a moment."; 500 and 503 -> "Something went wrong on our side. Please try again."; 400 body `{title:'Validation failed', detail:'Name is required'}` -> `'Name is required'`; 400 with `{detail: {x:1}}` and with string HTML body -> "The request could not be completed."; 401 -> `null`.
  - `error.interceptor.spec.ts` (`provideHttpClient(withInterceptors([errorInterceptor]))` + `HttpTestingController`, spy `ToastService.show`): `error(new ProgressEvent('error'), {status:0})` -> `show` called once with the friendly message and the call's observable errors with the original `HttpErrorResponse`; 200 -> no toast; 401 -> no toast.
  - `toast.service.spec.ts`: two consecutive `show()` calls -> `MatSnackBar.dismiss` called before the second `open`.
  - `auth.interceptor.spec.ts`: request reaches the backend unchanged (same URL, no `Authorization` header).
- [ ] **Step 2:** Run -> FAIL.
- [ ] **Step 3:** Install Material packages (approved) and implement per Interfaces.
- [ ] **Step 4:** Tests PASS; build + lint clean.
- [ ] **Step 5: Commit** `KAN-12: add http interceptors and global error toasts`

### Task 5: App shell + Material M3 theme (KAN-52)

**Files:**
- Modify: `src/client/src/styles.scss` (M3 theme via `mat.theme`, custom non-default primary palette, system font stack, light surface background), `src/client/src/app/app.ts|html|scss` (replace generated content), `src/client/src/app/app.spec.ts`
- Create: none beyond the above (user menu lives in `app.html`)

**Interfaces:**
- Produces: shell template: `<mat-toolbar>` with app name "Stack Track" (link to `/`) and a user menu button (`mat-icon-button`, `account_circle` icon, `aria-label="User menu"`) opening a `mat-menu` with one disabled item "Sign in (coming soon)"; below, `<main><router-outlet /></main>`. No `NgModule`.

- [ ] **Step 1: Failing `app.spec.ts`:** renders toolbar containing text "Stack Track"; button with `aria-label="User menu"` exists; clicking it shows a menu item "Sign in (coming soon)"; contains a `router-outlet`.
- [ ] **Step 2:** Run -> FAIL.
- [ ] **Step 3:** Implement. Look: M3 flat toolbar (`surface` colour, 1px bottom border, no heavy elevation), 16px content padding, max-width container, rounded corners; avoid the default azure/rose preset look by choosing a custom palette (e.g. `mat.$violet-palette` or `mat.$cyan-palette`).
- [ ] **Step 4:** Tests PASS; `npm start` and take a screenshot (Chrome tools) at desktop + phone width, confirm it looks modern; show user.
- [ ] **Step 5: Commit** `KAN-12: add app shell and Material theme`

### Task 6: Verify, document, PR

- [ ] `cd src/client && npm run lint && npm run build && npm test -- --watch=false`; also `dotnet build src/api` and `dotnet test src/api` (Docker running): paste real output.
- [ ] Manual AC run: `npm start` on :4200; proxy curl; open `/nonsense` (Page not found); stop API and trigger a call (e.g. temporary `fetch('/api/health')` through devtools or a throwaway button NOT committed) -> toast appears.
- [ ] Code review against this plan + CLAUDE.md; fix findings.
- [ ] Update CLAUDE.md-mandated Notion page "Stack Track — Developer Docs": Feature log entry for KAN-12, layout (`src/client` structure), commands (`npm start/build/test/lint`), conventions (interceptors, `API_BASE_URL`, toast rule, proxy).
- [ ] Push `feature/KAN-12-frontend-scaffold`, open PR `KAN-12: Scaffold Angular 21 frontend app shell` (what changed, per-AC, how to test); comment PR link + summary on KAN-12; transition to In Review (never Done).
