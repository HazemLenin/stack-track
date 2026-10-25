# Contributing

Work is tracked in Jira (project key `KAN`). One ticket = one branch = one PR.

## Branches

- Features: `feature/KAN-<n>-short-slug`, e.g. `feature/KAN-12-board-crud`
- Bug fixes: `fix/KAN-<n>-short-slug`
- Default branch is `main`. Open PRs against it.

## Commits

Format: `KAN-<n>: imperative message`

```
KAN-12: add create board command
```

We use the Jira key as the prefix instead of Conventional Commits (`feat:`, `fix:`), so every commit links back to its ticket.

## Pull requests

- Title: `KAN-<n>: <ticket summary>`
- Body: what changed, how each acceptance criterion is met, how to test.
- Build, all tests and lint must pass before review.

## Other rules

- Don't add NuGet/npm packages without discussing first.
- Don't edit existing EF migrations, add a new one.
- Keep changes inside the ticket's scope.
- Formatting is enforced by `.editorconfig`; line endings are LF (`.gitattributes`).
