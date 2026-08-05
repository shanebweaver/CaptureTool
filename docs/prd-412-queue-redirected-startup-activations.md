# PRD: Queue redirected startup activations

- Issue: [#412](https://github.com/shanebweaver/CaptureTool/issues/412)
- Finding: `ARCH-07`
- Severity: Medium
- Status: Implemented
- Affected features: `APP-01`

## Summary

Redirected activation arguments must be materialized in the `AppInstance.Activated` callback and retained until the WinUI application and its dispatcher are ready. Startup will use an ordered queue that accepts redirects before `App` exists, then attaches the application consumer after the primary launch activation completes.

This removes the startup-time dependency on `App.Current`, preserves the lifetime-sensitive activation data before the redirecting process exits, and guarantees that the primary activation is handled before immediately redirected work.

## Problem

The primary `AppInstance` subscribes to redirected activation before `Microsoft.UI.Xaml.Application.Start`, but its event handler calls `App.Current.Activate(e)`. During that interval `Application.Current` has not been initialized because `new App()` has not run. A secondary process that redirects quickly enough can therefore dereference a nonexistent application or lose the activation.

Redirected `AppActivationArguments` may also expose data through a COM proxy owned by the redirecting process. Retaining the raw argument object until later is unsafe; the durable activation kind and protocol URI must be copied before the event callback returns.

## Goals

1. Accept redirected activations before the WinUI `App` instance exists.
2. Materialize all lifetime-sensitive activation data inside the lifecycle callback.
3. Preserve redirected activations in FIFO order until the UI dispatcher is initialized.
4. Process the primary launch activation before draining startup redirects.
5. Keep malformed activation failures explicit and log them once application logging is available.

## Non-goals

- Do not change single-instance registration or secondary-process foreground behavior.
- Do not change protocol routing, navigation destinations, or activation telemetry.
- Do not persist queued activations across process termination.
- Do not introduce retries for activation-handler failures.

## Functional requirements

### Lifecycle callback

- Register the primary instance's `Activated` event before WinUI startup.
- Materialize the activation kind and protocol URI before the callback returns.
- Represent invalid data or materialization exceptions as a queued failure rather than dereferencing application services that do not exist yet.
- Enqueue the materialized result without reading `App.Current`.

### Ordered startup queue

- Accept items before or after a consumer is attached.
- Drain pre-attachment items in FIFO order when the consumer attaches.
- Preserve ordering when items arrive while a drain is in progress.
- Reject a second consumer attachment explicitly.
- Retain the current item if the consumer throws so later work cannot silently overtake it.

### Application startup

- Construct `App` with the redirected-activation queue.
- Handle the primary activation through the existing awaited activation path.
- Attach the queue consumer only after primary handling completes.
- Dispatch drained and future redirects through the app's UI dispatcher.
- Log queued materialization warnings or exceptions through the normal application log service.

## Reliability requirements

- No redirected activation path may dereference `Application.Current` before `new App()` completes.
- Returning from the lifecycle callback must not leave required data backed only by the redirecting process.
- An activation arriving concurrently with consumer attachment must be handled exactly once and in FIFO order.
- A dispatcher enqueue failure must remain visible through the existing warning log.

## Test plan

- Queue a redirected protocol activation before attaching the application consumer, process the primary launch, attach, and verify primary-then-protocol order.
- Verify multiple pre-start redirects drain in FIFO order.
- Verify an activation enqueued reentrantly during drain cannot overtake pending work.
- Verify duplicate consumer attachment fails explicitly.
- Run every non-UI test project.
- Build the WinUI x64 Debug project.

## Acceptance criteria

- [x] An immediate redirected activation cannot access a nonexistent `App.Current`.
- [x] Redirected arguments are materialized before the lifecycle callback returns.
- [x] The primary activation completes before queued redirects are dispatched.
- [x] Queued and concurrent redirects retain FIFO order without loss.
- [x] Materialization and dispatcher failures remain observable through application logging.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.
