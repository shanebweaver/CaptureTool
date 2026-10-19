# Model cleanup and shutdown

An idle model can hang during native unload. An unbounded worker wait also stalls synchronous application shutdown. Closing the application must complete even if a provider never returns.

## Requirements

The worker will give cleanup at most two seconds during shutdown. After that grace period, it will return while continuing to observe cleanup in the background. Cancellation must fence publication before waiting for cleanup.

Native operations retain ownership of their model until they actually finish. Cleanup must wait for any live invocation, and restarting the worker must wait for outstanding cleanup before starting another provider. A timeout must never cause an unload underneath inference, an unobserved task failure, or overlapping model use.

## Acceptance criteria

- A resource whose release never completes cannot hold worker shutdown beyond the two second cleanup grace plus normal scheduling and store work.
- Cooperative cleanup still finishes before shutdown returns.
- A worker restarted while cleanup is pending starts no new provider until cleanup completes.
- Cleanup begins only after a cancellation resistant invocation ends; delayed cleanup failures are observed.

## Scope

Keep the existing execution store, authorization fences, and serialized model ownership. This requirement bounds native cleanup; it does not promise a deadline for arbitrary filesystem or database calls. No forced native disposal, migration, or second worker is required.
