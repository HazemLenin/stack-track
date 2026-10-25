# Stack Track client

Angular 21 client for the Stack Track task tracker.

- `npm start` serves http://localhost:4200 and proxies `/api/*` to http://localhost:5009 with the `/api` prefix stripped, so run the API first.
- `npm run build` builds for production.
- `npm test` runs unit tests.
- `npm run lint` lints the code.

Features live in `src/app/features/<feature>`; cross-cutting code lives in `src/app/core`.
