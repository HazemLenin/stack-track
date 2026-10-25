---
description: Implement a Jira ticket end to end using the Superpowers workflow
argument-hint: KAN-<number>
---

Implement Jira ticket $ARGUMENTS following this workflow exactly. Use the Superpowers skills named in each step.

## 1. Understand
- Fetch $ARGUMENTS from Jira via the Atlassian MCP: summary, description, acceptance criteria, comments, linked/blocking tickets.
- If a blocking ticket isn't Done, stop and tell me.
- Read CLAUDE.md and any related plans in `docs/plans/`.

## 2. Clarify (brainstorming skill)
- Treat the ticket as the spec. Use brainstorming only to find gaps, contradictions or missing edge cases.
- Small gaps: ask me. Real gaps in the requirements: post a comment on the ticket listing the questions, then stop.

## 3. Plan (writing-plans skill)
- Write the plan to `docs/plans/$ARGUMENTS.md`.
- Map every acceptance criterion to at least one task and one test.
- **Stop and wait for my approval of the plan.**

## 4. Setup
- Create a worktree and branch `$ARGUMENTS-short-slug` (using-git-worktrees skill).
- Transition the ticket to In Progress.

## 5. Build (executing-plans / subagent-driven-development + test-driven-development)
- Red, green, refactor for every task. No production code without a failing test first.
- Commit per task: `$ARGUMENTS: message`.
- Delegate by layer: backend tasks to the `api-cqrs-implementer` agent, frontend tasks to `angular-feature-builder`. When the plan has independent API and client tasks, run both in parallel (each in its own worktree via `isolation: "worktree"`), then merge. Do small or cross-layer tasks inline.

## 6. Verify (verification-before-completion + requesting-code-review)
- Run full build, all API tests, client tests and lint. Paste the real results.
- Spawn the `stacktrack-reviewer` agent with the ticket key (fresh context, read-only). Fix every blocker and major finding, then re-run it until it says READY.
- Re-check each acceptance criterion and state how it's satisfied.

## 7. Finish (finishing-a-development-branch)
- Spawn the `notion-doc-writer` agent with the ticket key to update the Notion Feature log before the PR is opened.
- Push and open a PR titled `$ARGUMENTS: <ticket summary>` with: what changed, how each AC is met, how to test.
- Comment the PR link and a short summary on the Jira ticket.
- Transition the ticket to In Review. Never to Done.