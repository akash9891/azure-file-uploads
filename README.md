# Azure Blob File Upload API

ASP.NET Core (.NET 10) minimal API that accepts file uploads, stores them in a private Azure Blob Storage container, and saves file metadata (filename, size, upload timestamp, blob URL) to PostgreSQL using Entity Framework Core.

Azure authentication uses `DefaultAzureCredential`, so no storage keys or connection strings for Azure are needed. Locally it uses your Azure CLI login.

## Project layout

```
src/FileUploads.Api/          API (endpoints, EF Core DbContext + migrations, blob service)
tests/FileUploads.Api.Tests/  xUnit integration tests (in-memory DB + fake blob store)
dotnet-tools.json             local dotnet-ef tool
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- Docker (runs PostgreSQL, or the whole app with Docker Compose)
- An Azure Storage account and container. Your Azure identity needs the **Storage Blob Data Contributor** role on that storage account.

## Setup

### 1. Sign in to Azure

```powershell
az login --use-device-code
az account show --output table
# Optional check that your identity can reach the container:
az storage container list --account-name <storage-account> --auth-mode login --output table
```

### 2. Start PostgreSQL

```powershell
docker run -d --name fileuploads-postgres `
  -e POSTGRES_USER=fileuploads `
  -e POSTGRES_PASSWORD=<choose-a-local-password> `
  -e POSTGRES_DB=fileuploads `
  -p 5432:5432 `
  -v fileuploads-pgdata:/var/lib/postgresql/data `
  postgres:17
```

On later runs, start it with `docker start fileuploads-postgres`.

### 3. Configure the connection string

The connection string is never committed. Store it with user-secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=fileuploads;Username=fileuploads;Password=<your-local-password>" --project src/FileUploads.Api
```

Or set the environment variable `ConnectionStrings__Postgres` instead.

### 4. Apply database migrations

```powershell
dotnet tool restore
dotnet ef database update --project src/FileUploads.Api
```

### 5. Run the API

```powershell
dotnet run --project src/FileUploads.Api --launch-profile http
```

The API listens on `http://localhost:5097`. In Development, the OpenAPI document is at `/openapi/v1.json`.

## Run with Docker Compose

`docker-compose.yml` runs the API (built from the `Dockerfile`) and PostgreSQL 17. Database data is kept in the `pgdata` volume. On startup the API applies the EF Core migrations itself, so `dotnet ef` isn't needed.

### 1. Create a service principal (one time)

A container can't use your `az login`, so it signs in to Azure as a **service principal**, an app identity with its own secret. Create one that can only write blobs in this storage account:

```powershell
az ad sp create-for-rbac --name fileuploads-docker `
  --role "Storage Blob Data Contributor" `
  --scopes /subscriptions/<subscription-id>/resourceGroups/file-upload-demo/providers/Microsoft.Storage/storageAccounts/fileuploads47akash
```

The command prints `appId`, `password` and `tenant`. Store them safely: the password isn't shown again. The role assignment can take a few minutes to take effect.

### 2. Create `.env`

```powershell
Copy-Item .env.example .env
```

Fill in `POSTGRES_PASSWORD`, plus `AZURE_TENANT_ID` (from `tenant`), `AZURE_CLIENT_ID` (from `appId`) and `AZURE_CLIENT_SECRET` (from `password`). `.env` is git-ignored, so never commit it.

### 3. Start it

```powershell
docker compose up --build
```

The API listens on `http://localhost:8080`:

```powershell
curl.exe -i -F "file=@C:\path\to\notes.txt" http://localhost:8080/api/files
curl.exe http://localhost:8080/api/files
```

PostgreSQL is published on host port **5433**, so it doesn't clash with a standalone container on 5432. Stop everything with `docker compose down`. Add `-v` to also delete the database volume.

The service principal is only for running the containers locally. When the app is deployed to Azure, use a Managed Identity instead (see [Azure authentication](#azure-authentication)) and leave the `AZURE_CLIENT_*` variables unset.

## Configuration

All settings can be set in `appsettings.json`, with user-secrets, or with environment variables (`:` becomes `__`).

| Setting | Environment variable | Default | Description |
|---|---|---|---|
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` | *(none, required)* | PostgreSQL connection string |
| `AzureStorage:AccountName` | `AzureStorage__AccountName` | `fileuploads47akash` | Storage account name |
| `AzureStorage:ContainerName` | `AzureStorage__ContainerName` | `uploads` | Blob container (must already exist) |
| `Upload:MaxFileSizeBytes` | `Upload__MaxFileSizeBytes` | `10485760` (10 MB) | Maximum upload size |
| `Upload:AllowedExtensions` | `Upload__AllowedExtensions__0`, `__1`, ... | `.txt .pdf .png .jpg .jpeg .gif .csv .json .docx .xlsx` | Allowed file extensions |
| `Database:MigrateOnStartup` | `Database__MigrateOnStartup` | `false` | Apply EF Core migrations at startup (set to `true` by Docker Compose) |
| — | `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` | *(unset)* | Service principal for `DefaultAzureCredential`, used when running in Docker |

Docker Compose reads its values from `.env`. See `.env.example` for the full list.

The account and container names identify the resource; they are not secrets. To use your own storage account, override `AzureStorage__AccountName` and `AzureStorage__ContainerName`.

### Azure authentication

The app builds a `BlobServiceClient` with `DefaultAzureCredential`:

- **Locally (Development):** it uses the Azure CLI login from `az login`. Managed Identity is skipped in Development, because off Azure its probe can fail and stop the credential chain. The `az` executable must be on `PATH`, so open a new terminal after installing the Azure CLI.
- **In Docker Compose:** it uses the service principal from the `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET` environment variables. These are checked before Managed Identity.
- **When deployed to Azure:** enable a Managed Identity on the app and grant it **Storage Blob Data Contributor** on the storage account. No code changes are needed.

## API

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/files` | Upload a file (multipart form field `file`). Returns `201 Created` with the metadata and a `Location` header. |
| `GET` | `/api/files/{id}` | Get one file's metadata, or `404`. |
| `GET` | `/api/files` | List all uploaded files' metadata, newest first. |

Upload example:

```powershell
curl.exe -i -F "file=@C:\path\to\notes.txt" http://localhost:5097/api/files
```

Response:

```json
{
  "id": "4e73bf94-5820-4d10-af16-b33e849d1903",
  "originalFileName": "notes.txt",
  "blobName": "4e73bf9458204d10af16b33e849d1903.txt",
  "sizeBytes": 71,
  "contentType": "text/plain",
  "uploadedAtUtc": "2026-10-16T12:45:21.4728328Z",
  "blobUrl": "https://fileuploads47akash.blob.core.windows.net/uploads/4e73bf9458204d10af16b33e849d1903.txt"
}
```

The container is private, so `blobUrl` identifies the blob but can't be opened anonymously.

### Behavior

- **Validation (400):** the request is rejected if the file is missing or empty, larger than `Upload:MaxFileSizeBytes`, or has an extension that isn't allowed.
- **Safe blob names:** blobs are stored as `<guid><extension>`. The client's filename is only kept as metadata, with any directory parts removed.
- **Storage failure (502):** returned if Azure authentication or the blob upload fails. Nothing is written to the database.
- **Partial failure (500):** if the blob uploads but saving the metadata fails, the API deletes the just-uploaded blob so no orphan is left. If that cleanup also fails, the error and blob name are logged so the blob can be removed manually.

## Tests

```powershell
dotnet test
```

The tests host the API with `WebApplicationFactory`, using an EF Core in-memory database and a fake blob store, so they need neither Azure nor PostgreSQL. They cover:

- a successful upload
- path sanitizing
- missing, empty, oversized and disallowed-type files
- blob cleanup when the database fails
- GET by id
