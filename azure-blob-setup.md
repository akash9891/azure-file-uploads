# Azure Blob Storage Setup — File Upload Assignment

This document records the Azure setup already completed for the file-upload assignment. Use it as project context so the implementation can focus on the .NET API, PostgreSQL metadata, Docker, and testing rather than repeating Azure resource creation.

## 1. Azure resources created

- **Azure subscription:** `Azure subscription 1`
- **Subscription ID:** `550b7be8-ec89-4a1d-a21e-f9b7592535ec`
- **Resource group:** `file-upload-demo`
- **Storage account:** `fileuploads47akash`
- **Storage region:** `Central India`
- **Performance tier:** `Standard`
- **Redundancy:** `LRS` (locally redundant storage)
- **Blob container:** `uploads`
- **Anonymous/public blob access:** Disabled; the container is private.

The Storage Account deployment succeeded, and the `uploads` container is visible in the Azure Portal.

## 2. Identity and permissions configured

The developer signed in to the Azure Portal and assigned their existing Microsoft Entra ID identity the Azure RBAC role:

- **Role:** `Storage Blob Data Contributor`
- **Scope:** The Storage Account `fileuploads47akash`

This role allows the assigned identity to perform data-plane operations on blobs, including uploading and managing blobs. The developer did **not** create a separate “Storage Account user”; the role was assigned to their existing Azure identity.

The Azure CLI login used the same identity. This was verified successfully by listing containers with Microsoft Entra ID authentication.

## 3. Local Azure CLI authentication verified

Azure CLI is installed on the Windows development machine and login succeeded using device-code login:

```powershell
az login --use-device-code
```

The selected subscription is `Azure subscription 1`.

The following command succeeded and returned the `uploads` container:

```powershell
az storage container list --account-name fileuploads47akash --auth-mode login --output table
```

This confirms that the currently signed-in identity can list the container. Blob upload from the .NET SDK has not yet been verified; test it during implementation.

Useful verification commands:

```powershell
az account show --output table
az storage container list --account-name fileuploads47akash --auth-mode login --output table
```

If the subscription selection is wrong, use:

```powershell
az account set --subscription "550b7be8-ec89-4a1d-a21e-f9b7592535ec"
```

## 4. .NET authentication approach

For local development, use the Azure SDK with `DefaultAzureCredential`. It can use the developer's Azure CLI sign-in, among other supported credential sources.

NuGet packages expected:

```powershell
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Identity
```

Example client construction:

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;

var accountName = configuration["AzureStorage:AccountName"]
    ?? throw new InvalidOperationException("AzureStorage:AccountName is missing.");

var containerName = configuration["AzureStorage:ContainerName"]
    ?? throw new InvalidOperationException("AzureStorage:ContainerName is missing.");

var serviceClient = new BlobServiceClient(
    new Uri($"https://{accountName}.blob.core.windows.net"),
    new DefaultAzureCredential());

var containerClient = serviceClient.GetBlobContainerClient(containerName);
```

Suggested non-secret configuration in `appsettings.json`:

```json
{
  "AzureStorage": {
    "AccountName": "fileuploads47akash",
    "ContainerName": "uploads"
  }
}
```

These values identify the storage resource; they are not credentials. Do not put account keys, SAS tokens, client secrets, or passwords in source control.

The container already exists. The application may use `GetBlobContainerClient("uploads")` directly; it does not need to create the container at startup. Avoid using public/anonymous blob access.

## 5. Important distinction: authentication vs authorization

- **Authentication** proves which identity is making the request. Locally, `DefaultAzureCredential` can use the Azure CLI login.
- **Authorization** determines what that identity can do. Azure RBAC grants `Storage Blob Data Contributor` to the identity at the Storage Account scope.

If the SDK returns an authorization error, check that the app is using the expected credential/account, that the role assignment is still effective, and allow time for role changes to propagate.

## 6. Future Azure deployment

The app has not been deployed to Azure yet. For local development, use the developer's Azure CLI identity via `DefaultAzureCredential`.

If deploying to Azure App Service or Azure Container Apps later:
1. Enable a Managed Identity for the deployed application.
2. Assign that identity `Storage Blob Data Contributor` on the Storage Account.
3. Continue using `DefaultAzureCredential` where supported; in Azure it can obtain a token for the Managed Identity.
4. Do not share the developer's personal identity or credentials with the deployed app.

Managed Identity is an application/resource identity, not a shared user account.

## 7. Project requirements still to implement

Azure infrastructure setup is complete. The application work remains:

- ASP.NET Core REST API accepting multipart file uploads (`IFormFile`).
- Validate file size and allowed file types; use safe, unique blob names rather than trusting user-provided paths.
- Upload file contents to the private `uploads` Blob container.
- Persist metadata in local PostgreSQL using Entity Framework Core: original filename, file size, upload timestamp, and blob URL.
- Handle partial failure: if blob upload succeeds but database persistence fails, attempt cleanup or implement another documented recovery strategy.
- Return appropriate HTTP responses, such as `201 Created` after a successful upload.
- Dockerfile for the API and Docker Compose for the API plus PostgreSQL, with persistent database storage.
- README with local setup, Azure CLI login, required configuration, migrations, and API testing instructions.
- Never commit secrets. A reviewer will need their own Azure Storage account/permissions or another explicitly configured test environment to perform real Azure uploads.

## 8. Context for the AI implementing the project

Do not repeat the Azure Portal setup or create a new Storage Account/container. Use the existing resources and settings above. Start by inspecting the current repository and its .NET version before generating or modifying files. Keep Azure authentication secure and configurable. The developer has confirmed Azure CLI container-list access, but end-to-end SDK upload still needs to be tested.
