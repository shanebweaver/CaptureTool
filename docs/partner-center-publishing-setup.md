# Microsoft Store publishing setup

This guide is for wiring the Capture Tool GitHub Actions publish workflow to
Microsoft Partner Center with a Microsoft Entra ID app registration.

The important mental model is:

- Your old Microsoft account can still own or manage the Partner Center account.
- The Microsoft Store Developer CLI should not sign in with that MSA.
- CI uses a Microsoft Entra tenant, an app registration, and a Partner Center
  Manager assignment for that app registration.

## What the workflow expects

The publish workflow uses the Microsoft Store Developer CLI through
`microsoft/microsoft-store-apppublisher`. It expects these GitHub repository
secrets:

- `PARTNER_CENTER_TENANT_ID`
- `PARTNER_CENTER_SELLER_ID`
- `PARTNER_CENTER_CLIENT_ID`
- `PARTNER_CENTER_CLIENT_SECRET`
- `STORE_PRODUCT_ID`

The first four values are passed to:

```powershell
msstore reconfigure `
  --tenantId $env:PARTNER_CENTER_TENANT_ID `
  --sellerId $env:PARTNER_CENTER_SELLER_ID `
  --clientId $env:PARTNER_CENTER_CLIENT_ID `
  --clientSecret $env:PARTNER_CENTER_CLIENT_SECRET
```

`STORE_PRODUCT_ID` is the Microsoft Store product ID for Capture Tool. The
public listing currently uses `9N7W6J13C4W0`; verify that against Partner
Center before saving it as a secret.

## Prerequisites

Before starting, confirm these are true:

1. The app is already published and live in the Microsoft Store. Microsoft says
   the GitHub Actions update flow is for updating already-published apps.
2. You can sign in to Partner Center as a user with the Manager role.
3. You either have an existing Microsoft Entra tenant to associate, or you are
   ready to create a new tenant from Partner Center.
4. You can create an app registration in that tenant, or an admin in the tenant
   can do it for you.

If your Partner Center account is old and tied to an MSA, do not try to use the
MSA as the CI identity. Use it only to get into Partner Center and associate an
Entra tenant.

## Step 1: Associate an Entra tenant with Partner Center

In Partner Center:

1. Sign in to https://partner.microsoft.com.
2. Select the gear icon.
3. Open `Account settings`.
4. Open `Tenants`.
5. Choose one path:
   - If you already have an organizational tenant, select
     `Associate Microsoft Entra ID with your Partner Center account`.
   - If you do not have one, select `Create Microsoft Entra ID`.
6. Complete the sign-in or creation flow for the tenant.
7. Confirm the tenant domain shown by Partner Center.

Notes:

- A Partner Center Manager can associate tenants.
- Creating users or changing the Entra tenant itself requires an account with
  the right Entra admin permissions, usually Global Administrator.
- Partner Center can have multiple associated tenants, so it is acceptable to
  create a small tenant dedicated to Store publishing if that is cleaner.

Record the tenant ID:

1. Open https://entra.microsoft.com.
2. Switch to the tenant you associated with Partner Center.
3. Go to `Entra ID` > `Overview`.
4. Copy `Tenant ID`.
5. Save it as GitHub secret `PARTNER_CENTER_TENANT_ID`.

## Step 2: Create the CI app registration

In the Entra admin center:

1. Go to `Entra ID` > `App registrations`.
2. Select `New registration`.
3. Use a clear name, for example `CaptureTool-GitHub-Store-Publish`.
4. For supported account types, choose single tenant.
5. Select `Register`.
6. On the app overview page, copy `Application (client) ID`.
7. Save it as GitHub secret `PARTNER_CENTER_CLIENT_ID`.

You do not need a redirect URI for this CI scenario.

## Step 3: Create the client secret

In the same app registration:

1. Open `Certificates & secrets`.
2. Open `Client secrets`.
3. Select `New client secret`.
4. Use a description such as `GitHub Actions Store publishing`.
5. Choose an expiration you can operationally rotate.
6. Select `Add`.
7. Immediately copy the secret `Value`, not the secret ID.
8. Save that value as GitHub secret `PARTNER_CENTER_CLIENT_SECRET`.

Microsoft recommends certificates over client secrets for production
confidential clients, but the current workflow is set up for the client-secret
path because that is what the Store CLI GitHub Actions examples use. Put a
calendar reminder in place before the secret expires. If this secret expires,
the publish workflow will fail at `msstore reconfigure` or the first Store API
call.

## Step 4: Add the Entra app to Partner Center

This is the step that is easiest to miss. Creating the app registration in
Entra is not enough. Partner Center also needs to know that this application is
allowed to manage submissions.

In Partner Center:

1. Sign in as a Partner Center Manager.
2. Go to the gear icon > `Account settings`.
3. Open `User management`.
4. Open the `Microsoft Entra applications` tab.
5. Add the app registration you created.
6. Assign it the `Manager` role.

If you do not see the app registration, check that:

- Partner Center is associated with the same tenant where you created the app.
- You are signed into Partner Center as a user with the Manager role.
- You are not accidentally creating the app registration in a different Entra
  directory.

## Step 5: Get Seller ID

In Partner Center:

1. Open `Account settings`.
2. Look under `Developer settings`, `Identifiers`, or equivalent account
   identity pages.
3. Copy the `Seller ID` or `Publisher ID` value used for Store API access.
4. Save it as GitHub secret `PARTNER_CENTER_SELLER_ID`.

Microsoft's docs use the wording `Seller ID`, while some Partner Center UI
surfaces may say `Publisher ID`. The value must be the identifier accepted by
`msstore reconfigure --sellerId`.

## Step 6: Get Store product ID

In Partner Center:

1. Open the Capture Tool product.
2. Find its Store product ID / Partner Center ID.
3. Save it as GitHub secret `STORE_PRODUCT_ID`.

For Capture Tool, verify whether this is `9N7W6J13C4W0` before saving it.

## Step 7: Add GitHub secrets

In GitHub:

1. Open the repository.
2. Go to `Settings` > `Secrets and variables` > `Actions`.
3. Add these repository secrets:

```text
PARTNER_CENTER_TENANT_ID
PARTNER_CENTER_SELLER_ID
PARTNER_CENTER_CLIENT_ID
PARTNER_CENTER_CLIENT_SECRET
STORE_PRODUCT_ID
```

Do not put these values in workflow YAML, commit history, logs, or issue
comments.

## Step 8: Test the credentials locally

Install the Microsoft Store Developer CLI, then run:

```powershell
msstore reconfigure `
  --tenantId "<tenant-id>" `
  --sellerId "<seller-id>" `
  --clientId "<client-id>" `
  --clientSecret "<client-secret>"

msstore apps list
msstore apps get "<store-product-id>"
```

Expected result:

- `msstore reconfigure` completes.
- `msstore apps list` can list your Partner Center apps.
- `msstore apps get` can read the Capture Tool app.

Common failures:

- Tenant not associated with Partner Center: associate the tenant in Partner
  Center first.
- App registration not added in Partner Center: add it under `User management`
  > `Microsoft Entra applications` and assign Manager.
- Wrong tenant: confirm the app registration tenant ID equals the Partner
  Center-associated tenant ID.
- Expired or copied-wrong client secret: create a new secret and copy the
  `Value`.
- MSA sign-in confusion: the CLI/API credentials are Entra app credentials, not
  your personal Microsoft account.

## Step 9: Test the GitHub workflow

After a successful `Build` workflow on `main`:

1. Open the successful Build workflow run in GitHub Actions.
2. Copy the numeric run ID from the URL.
3. Open `Publish to Microsoft Store`.
4. Select `Run workflow`.
5. Paste the Build run ID.
6. Run it.

The publish workflow downloads the `StorePackage-x64` and `StorePackage-arm64`
artifacts from that build run.

## CI review notes

The workflow shape is generally correct:

- `Build` runs on pushes to `main`.
- `Publish to Microsoft Store` runs after the `Build` workflow completes
  successfully on `main`.
- It uses the current Store CLI credential shape:
  tenant ID, seller ID, client ID, client secret.
- `actions: read` is present, which is required for downloading artifacts from
  another workflow run.

The risky parts are:

- Store submissions are stateful. The publish workflow currently has x64 and
  arm64 matrix jobs targeting the same Store product. If Partner Center rejects
  simultaneous submissions, change this to one sequential publish job or publish
  a single `.msixupload` package that contains everything Partner Center expects.
- The project has `AppxBundle` and resource scale packages. The workflow now
  prefers `.msixupload`, otherwise it chooses the architecture package matching
  the matrix platform and ignores `_scale-*.msix` files.
- The build artifact path is `output\<platform>\**\*.msix`, which depends on
  the app project's `AppxPackageDir`. If the build produces `.msixupload`
  instead, add that extension to the upload artifact path too.

## References

- Microsoft Store Developer CLI overview:
  https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/
- Microsoft Store Developer CLI commands:
  https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/commands
- Publish app updates with GitHub Actions:
  https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions
- Associate an existing Entra tenant with Partner Center:
  https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/associate-existing-azure-ad-tenant-with-partner-center-account
- Create a new Entra tenant from Partner Center:
  https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/create-new-azure-ad-tenant
- Register an app in Microsoft Entra ID:
  https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app
- Add application credentials in Microsoft Entra ID:
  https://learn.microsoft.com/en-us/entra/identity-platform/how-to-add-credentials
