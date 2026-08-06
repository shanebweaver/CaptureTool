# PRD: Coalesce Home recent-capture refresh notifications

- Issue: [#425](https://github.com/shanebweaver/CaptureTool/issues/425)
- Architecture finding: `ARCH-20`
- Severity: Low
- Status: Implemented
- Affected features: `APP-03`, `APP-06`

## Summary

Home must not lose a recent-capture invalidation that arrives while its recent-captures query is already running. Refresh requests that overlap an active initial load, refresh, or load-more query will mark the collection dirty. The active loader will drain that pending invalidation with a reset query before it exits.

Multiple invalidations during one query are coalesced into one follow-up query. If another invalidation arrives during the follow-up query, the loader runs again so the final collection represents the newest catalog state it was notified about.

## Problem

Capture-state and catalog-change events fire-and-forget `RefreshRecentCapturesAsync`. `LoadRecentCapturesPageAsync` currently returns immediately whenever `IsLoadingRecentCaptures` is true.

If the active query took its catalog snapshot before a new capture was recorded, a notification arriving before that query completes is discarded. The stale response then replaces the Home collection, and the new capture remains invisible until navigation or another catalog event causes a later refresh.

The same race can occur while Home is appending a load-more page: a catalog invalidation is ignored because the pagination query owns the loading flag.

## Goals

1. Preserve refresh invalidations received during any recent-captures query.
2. Rerun a reset query after the active query completes.
3. Coalesce repeated invalidations during one query into a single follow-up request.
4. Continue draining when an invalidation arrives during the follow-up request.
5. Keep pagination, loading indicators, command availability, and cancellation behavior intact.
6. Avoid concurrent mutations of the observable collection.

## Non-goals

- Do not change recent-capture catalog ordering or pagination contracts.
- Do not make capture/catalog event handlers await refresh completion.
- Do not add polling or debounce delays.
- Do not redesign the notifier or capture-state event sources.
- Do not preserve intermediate catalog snapshots when a newer reset response supersedes them.

## Functional requirements

### Pending refresh state

- When a reset request arrives during an active load, set a pending-refresh flag instead of discarding the request.
- A blocked load-more request remains a no-op because load-more commands are already disabled while loading.
- Keep one loader responsible for collection mutation at a time.

### Drain loop

- After each completed query, inspect and clear the pending-refresh flag.
- If the flag was set, issue another query with `Skip = 0`, clear the displayed collection, and rebuild it from that response.
- Keep the loading state active until no pending refresh remains.
- Allow an invalidation during the follow-up query to request another pass.
- Preserve the caller's cancellation token across the drain loop.

## Reliability requirements

- A notification between query start and completion must be reflected before the active load task completes.
- Multiple notifications during the same query must not create concurrent or redundant queries.
- A refresh queued during load-more must replace the paged collection with a fresh first page.
- Existing query failure behavior remains bounded by the use-case response contract and the loader's `finally` state cleanup.

## Test plan

- Start Home loading with a controlled in-flight first-page query.
- Raise multiple catalog notifications before completing that query.
- Return a stale first response and a newer follow-up response; verify only the newer collection remains and exactly two queries run.
- Start a load-more query, notify while it is active, then verify a reset query runs after pagination and replaces the collection.
- Run all non-UI tests and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] A notification during an initial or refresh query cannot be lost.
- [x] A notification during load-more triggers a subsequent reset query.
- [x] Repeated overlapping notifications coalesce without concurrent collection mutation.
- [x] Home finishes with the latest notified catalog snapshot.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.
