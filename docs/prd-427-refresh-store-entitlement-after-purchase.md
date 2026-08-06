# PRD: Refresh Store entitlement after add-on purchase

- Issue: [#427](https://github.com/shanebweaver/CaptureTool/issues/427)
- Finding: `ARCH-22`
- Severity: Low
- Status: Implemented
- Affected features: `IMG-08`, `PLT-09`

## Summary

The Store page must own the add-on purchase workflow rather than binding its button directly to the purchase use case. While a purchase is running, duplicate attempts must be disabled. When the Store reports success—including its already-purchased result—the page must refresh product and entitlement state immediately. A failed or canceled attempt must leave the product retryable and show bounded feedback.

## Problem

`StorePageViewModel` assigns add-on ownership, price, logo, and availability only during `LoadAsync`. Its purchase command delegates directly to `PurchaseChromaKeyAddOnUseCase`, discarding `PurchaseChromaKeyAddOnResponse`. The page therefore never reloads Store product state after a successful purchase.

The Windows Store adapter maps both `Succeeded` and `AlreadyPurchased` to a successful purchase response, so both paths currently leave stale UI. The purchase button can continue to appear enabled and the add-on can continue to appear unowned until the page is reopened.

## Goals

1. Consume the purchase response in `StorePageViewModel`.
2. Disable duplicate purchase attempts while a request is in flight.
3. Refresh Store product and entitlement state after successful or already-owned outcomes.
4. Prevent a confirmed purchase from returning to an actionable unowned state if Store propagation is delayed.
5. Show bounded failure/cancellation feedback while keeping retry available.
6. Keep Store-specific platform statuses inside infrastructure.

## Non-goals

- Do not enable the currently disabled Store feature.
- Do not redesign the Store page or purchase use case.
- Do not add receipt validation, refund handling, or license-change push notifications.
- Do not expose Store error text, product identifiers, or account information in UI or telemetry.
- Do not alter the image editor’s entitlement lookup.

## State model

The Store page maintains these independent concerns:

- **Product state:** available/unavailable, owned/unowned, price, and logo.
- **Purchase state:** idle or purchasing.
- **Purchase feedback:** none or a bounded unsuccessful-attempt message.

The purchase command is available only when the product is available, is not owned, and no purchase is in flight.

## Functional requirements

### Initial product load

- Query the add-on through `IGetChromaKeyAddOnUseCase` using the linked application cancellation token.
- Apply ownership, availability, price, and logo through one shared state-update path.
- Preserve the existing unavailable state when no product is returned.

### Purchase workflow

- Clear stale purchase feedback before starting an attempt.
- Set purchasing state before invoking `IPurchaseChromaKeyAddOnUseCase`.
- Disable the purchase command for the full in-flight interval.
- Restore idle state in `finally`, including exception and cancellation paths.
- Treat only `PurchaseChromaKeyAddOnResponse.Purchased == true` as confirmation.

### Successful or already-owned outcome

- Re-query product information after confirmation.
- Apply refreshed price, logo, and entitlement state.
- Treat the confirmed purchase response as authoritative if the refreshed product temporarily reports unowned or cannot be returned.
- Show the localized owned label and keep purchase disabled.

### Failure or cancellation

- Do not re-query product state.
- Preserve the pre-purchase product state.
- Show a generic localized message that the purchase was not completed.
- Keep the purchase action available for retry.

## User experience

- Replace the price label with an in-button progress indicator during purchase.
- Disable the purchase button while purchasing and after ownership is confirmed.
- Show failure feedback adjacent to the purchase action.
- Do not display raw Store statuses or exception content.

## Reliability requirements

- A command invoked twice while the first attempt is running must issue only one purchase request.
- Purchase state must return to idle after every completed attempt.
- A confirmed purchase must not leave `IsChromaKeyAddOnAvailable` true.
- Failure feedback must clear when a retry begins or ownership is confirmed.
- Linked cancellation sources must be disposed after load and purchase operations.

## Test plan

- Load an unowned add-on and verify purchase is available.
- Complete a successful purchase and verify product state is queried again and becomes owned.
- Return an unowned or missing product after confirmed purchase and verify purchase remains disabled and ownership is shown.
- Hold a purchase in flight and verify duplicate execution is disabled.
- Return unsuccessful and canceled responses and verify feedback plus retry availability.
- Build WinUI to validate the Store-page bindings.
- Run every non-UI test project.

## Acceptance criteria

- [x] Successful and already-owned purchase outcomes refresh entitlement UI immediately.
- [x] Duplicate purchase attempts are disabled while Store UI is active.
- [x] Confirmed ownership disables further purchase attempts.
- [x] Failure and cancellation produce bounded, retryable feedback.
- [x] Existing non-UI tests remain green.
- [x] The WinUI x64 Debug project builds successfully.
