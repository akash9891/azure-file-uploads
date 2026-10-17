# Azure Blob File Upload API

[![CI](https://github.com/akash9891/azure-file-uploads/actions/workflows/ci.yml/badge.svg)](https://github.com/akash9891/azure-file-uploads/actions/workflows/ci.yml)

ASP.NET Core (.NET 10) minimal API that accepts file uploads, stores them in a private Azure Blob Storage container, and saves file metadata (filename, size, upload timestamp, blob URL) to PostgreSQL using Entity Framework Core.

Azure authentication uses `DefaultAzureCredential`, so no storage keys or connection strings for Azure are needed. It signs in with your Azure CLI login under `dotnet run`, with a service principal in Docker, and with a Managed Identity when deployed.

## For reviewers

There are three ways to check that the app works. No secrets are committed to this repo: `.env` is git-ignored, and `.env.example` documents every variable.

1. **Quick check, no Azure or database needed:** run `dotnet test`. The 9 integration tests run the real API with an in-memory database and a fake blob store (see [Tests](#tests)). The [CI workflow](https://github.com/akash9891/azure-file-uploads/actions/workflows/ci.yml) runs the same tests and builds the Docker image on every push.
2. **Already verified against real Azure:** on 2026-10-17, using `docker compose up` (see [Run with Docker Compose](#run-with-docker-compose)):
   - `POST /api/files` returned `201 Created`, with a response like the one in [API](#api).
   - The blob was confirmed in the `uploads` container of the `fileuploads47akash` storage account with `az storage blob show`.
   - The matching row (filename, size, upload timestamp, blob URL) was confirmed in PostgreSQL.
3. **Live upload test, on request:** I can provide a **time-limited, least-privilege credential**. It's a separate client secret for this project's service principal, and it only has *Storage Blob Data Contributor* on the demo storage account. You get it privately as a ready-to-use `.env` file, and it's revoked after the review. Uploaded files go to my Azure container, and their metadata goes to the PostgreSQL that Docker Compose starts on your machine.

   To use your own storage instead, create a service principal as described in [Run with Docker Compose](#run-with-docker-compose). Then set `AZURE_STORAGE_ACCOUNT` and `AZURE_STORAGE_CONTAINER` in `.env` to your account and container.

## Assignment checklist

| Requirement | Status | Where |
|---|---|---|
| File upload endpoint: saves to Azure Blob Storage and returns the blob URL | ✅ | `POST /api/files`: `src/FileUploads.Api/Endpoints/FileEndpoints.cs`, `Services/AzureBlobStorageService.cs` |
| Database record with filename, file size, upload timestamp and blob URL | ✅ | PostgreSQL through EF Core: `Data/FileRecord.cs`, `Migrations/` |
| Dockerfile, plus docker-compose for the local database | ✅ | `Dockerfile`, `docker-compose.yml` (API + PostgreSQL), see [Run with Docker Compose](#run-with-docker-compose) |
| GitHub repo with a README covering setup and environment variables | ✅ | This README, [Configuration](#configuration), `.env.example` |
| **Bonus:** security, with no hardcoded connection strings | ✅ / 🟡 | Azure access uses `DefaultAzureCredential` with RBAC: no storage keys or connection strings in code or config. Secrets come from user-secrets or a git-ignored `.env`. Managed Identity is supported with no code changes, but it is only used once the app is deployed (see [Azure authentication](#azure-authentication)). Key Vault is designed but not set up. |
| **Bonus:** CI/CD with GitHub Actions | 🟡 | `.github/workflows/ci.yml` tests, builds and publishes the Docker image to GHCR on every push. Deploying to Azure is designed but not set up (see [CI/CD](#cicd)). |
| **Bonus:** automated tests for the upload route | ✅ | 9 integration tests in `tests/FileUploads.Api.Tests/` (see [Tests](#tests)) |

## Project layout

```
src/FileUploads.Api/          API (endpoints, EF Core DbContext + migrations, blob service)
tests/FileUploads.Api.Tests/  xUnit integration tests (in-memory DB + fake blob store)
Dockerfile                    multi-stage build of the API image
docker-compose.yml            API + PostgreSQL for local runs
.env.example                  template for the git-ignored .env used by Docker Compose
.github/workflows/ci.yml      CI: test, build and publish the Docker image
dotnet-tools.json             local dotnet-ef tool
```

## Prerequisites

- **Docker Compose:** [Docker Desktop](https://www.docker.com/products/docker-desktop/), plus Azure credentials for a service principal (see below).
- **Without Docker, or to run the tests:** the [.NET 10 SDK](https://dotnet.microsoft.com/download) and the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli). Docker is still used for PostgreSQL.
- An Azure Storage account and container. The identity the app signs in with needs the **Storage Blob Data Contributor** role on that storage account.

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

## Run locally without Docker

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

In Postman: **POST** → **Body** → **form-data**, then add the key `file`, set its type to **File** and choose a file. Use port `8080` for Docker Compose, or `5097` for `dotnet run`.

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

## CI/CD

The GitHub Actions workflow in `.github/workflows/ci.yml` runs on every push to `main`, on pull requests, and on demand.

1. **Build and test:** restores, builds, and runs `dotnet test`. Test results (`.trx`) are attached to the run.
2. **Build Docker image:** runs only if the tests pass. It builds the image from the `Dockerfile`. On pushes to `main`, it also publishes the image to GitHub Container Registry, tagged `latest` and with the commit SHA. Pull requests only build the image, to check that the `Dockerfile` still works.

The pipeline needs no secrets: it pushes with the built-in `GITHUB_TOKEN`. To run the published image instead of building it:

```powershell
docker compose pull api
docker compose up
```

### Deploying to Azure (next step, not set up)

The pipeline stops at publishing the image. Deploying to Azure Container Apps would add a third job, with no code changes:

- **GitHub → Azure sign-in:** `azure/login` with **OIDC**. An Entra app registration has a federated credential that trusts only this repo's `main` branch, so no Azure secret is stored in GitHub.
- **Deploy:** `az containerapp update --image ghcr.io/akash9891/azure-file-uploads:<sha>`.
- **App → Azure sign-in:** a **user-assigned Managed Identity** with *Storage Blob Data Contributor* on the storage account. Setting `AZURE_CLIENT_ID` to that identity's client ID lets `DefaultAzureCredential` use it, and the local service principal variables are left unset.
- **Database:** Azure Database for PostgreSQL. The connection string is stored in **Azure Key Vault** and read by the Container App through the same Managed Identity (*Key Vault Secrets User*). `Database__MigrateOnStartup=true` creates the schema on first start.
