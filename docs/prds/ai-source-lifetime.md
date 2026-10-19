# Source protection during native work

A timed out provider may continue reading the source after the worker has rejected its result. A source lease tied only to the logical step can close too early, allowing the file to be changed while native work still uses its path.

## Requirements

Retain the source lease until both the logical step and its last actual provider invocation have ended. Late work must keep the same identity and revision protected against writes, deletion, and rename. Dispose the lease exactly once, including when the provider fails, cancellation occurs, or shutdown returns before native work finishes.

Deferred source disposal must be part of the worker's outstanding operation so another provider cannot start before ownership is released. Publication remains fenced by cancellation, authorization, request identity, source revision, and store commit guards.

## Acceptance criteria

- A real file cannot be renamed or opened for writing while a timed out provider remains active.
- After actual completion, the lease closes and the file becomes writable and renameable.
- The same protection holds across cancellation and worker shutdown, which remain bounded.
- A late successful or failed result cannot publish, and a queued provider cannot overlap the outstanding operation.

## Scope

Retain the existing file identity checks and guarded source handle. Do not copy all media, hold every queued file open, or introduce persisted session identity or migration logic.
