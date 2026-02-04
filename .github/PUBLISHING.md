# Microsoft Store Publishing Workflow

This workflow automatically uploads app packages to Microsoft Partner Center whenever changes are pushed to the `main` branch.

## How it Works

1. The workflow is triggered after the `Build` workflow completes successfully
2. It downloads the build artifacts (both x64 and arm64 packages)
3. It uploads the packages to Microsoft Partner Center to create or update a submission
4. The submission is **NOT automatically published** - you must manually publish from Partner Center

## Required GitHub Secrets

You need to configure the following secrets in your GitHub repository settings:

### 1. `PARTNER_CENTER_TENANT_ID`
Your Azure AD tenant ID. You can find this in the Azure Portal under Azure Active Directory > Properties > Directory ID.

### 2. `PARTNER_CENTER_CLIENT_ID`
The application (client) ID from your Azure AD app registration used for Partner Center API access.

To create this:
1. Go to Azure Portal > Azure Active Directory > App registrations
2. Create a new registration or use an existing one
3. Copy the Application (client) ID

### 3. `PARTNER_CENTER_CLIENT_SECRET`
A client secret for the Azure AD app registration.

To create this:
1. In your Azure AD app registration, go to Certificates & secrets
2. Create a new client secret
3. Copy the secret value (not the ID)

### 4. `PARTNER_CENTER_APP_ID`
Your app's Store ID from Partner Center.

To find this:
1. Go to Partner Center (https://partner.microsoft.com/dashboard)
2. Navigate to your app
3. Go to App management > App identity
4. Copy the Store ID (format: 9NBLGGH4NNS1 or similar)

## Setting Up Azure AD App Registration

To use the Partner Center Submission API, you need to associate an Azure AD application with your Partner Center account:

1. Go to Partner Center > Account settings > User management > Azure AD applications
2. Add your Azure AD application
3. Assign it the "Manager" role to allow creating and updating submissions

## Manual Publishing

After the workflow completes successfully:
1. Go to Partner Center
2. Navigate to your app's submission
3. Review the uploaded packages
4. Click "Submit to the Store" when ready to publish

## Troubleshooting

- Ensure your Azure AD app has the proper permissions in Partner Center
- Verify all secrets are correctly configured in GitHub
- Check the workflow logs for detailed error messages
- The `skip-polling` option is set to `true` to prevent automatic publishing
