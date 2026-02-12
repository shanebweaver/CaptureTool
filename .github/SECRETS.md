# GitHub Secrets Configuration

This document describes the GitHub secrets required for CI/CD workflows.

## Microsoft Store Publishing

The `publish.yml` workflow uses the Microsoft Store CLI to publish app packages to the Microsoft Store. The following secrets must be configured in the repository settings:

### Required Secrets

| Secret Name | Description |
|------------|-------------|
| `PARTNER_CENTER_TENANT_ID` | The Azure AD Tenant ID for your Microsoft Partner Center account |
| `PARTNER_CENTER_SELLER_ID` | Your Microsoft Partner Center Seller ID |
| `PARTNER_CENTER_CLIENT_ID` | The Client ID of your Azure AD application registered for Partner Center API access |
| `PARTNER_CENTER_CLIENT_SECRET` | The Client Secret for your Azure AD application |
| `STORE_PRODUCT_ID` | Your app's Store Product ID (found in Partner Center) |

### Migration from Previous Secrets

If you are migrating from the previous publishing workflow, the secret names have changed slightly:

| Old Secret Name | New Secret Name | Notes |
|----------------|-----------------|-------|
| `PARTNER_CENTER_TENANT_ID` | `PARTNER_CENTER_TENANT_ID` | No change needed |
| `PARTNER_CENTER_CLIENT_ID` | `PARTNER_CENTER_CLIENT_ID` | No change needed |
| `PARTNER_CENTER_CLIENT_SECRET` | `PARTNER_CENTER_CLIENT_SECRET` | No change needed |
| `PARTNER_CENTER_APP_ID` | `STORE_PRODUCT_ID` | Rename to new name |
| N/A | `PARTNER_CENTER_SELLER_ID` | New secret - obtain from Partner Center |

### Setting up Azure AD Application

To obtain these credentials:

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to Azure Active Directory > App registrations
3. Create a new application or use an existing one
4. Grant it the necessary permissions for Microsoft Store publishing
5. Generate a client secret
6. Get your Tenant ID, Client ID, and Client Secret
7. Get your Seller ID from [Partner Center](https://partner.microsoft.com/dashboard)
8. Get your Store Product ID from your app's page in Partner Center

### Additional Resources

- [Microsoft Store CLI Documentation](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/overview)
- [Microsoft Store CLI GitHub Actions](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions)
