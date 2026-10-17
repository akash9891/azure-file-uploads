# Project Details: Azure Blob File Upload Application

## Objective

Build a containerized REST API or web application that accepts file
uploads, stores them in Azure Blob Storage, and persists file metadata
in a relational database.

## Requirements

### 1. File Upload Endpoint

-   Accept a file upload.
-   Save the file to an Azure Storage Blob container.
-   Generate or retrieve the file's access URL.

### 2. Database Integration

Insert a record into a relational database such as PostgreSQL, MySQL, or
Azure SQL containing:

-   Filename
-   File size
-   Upload timestamp
-   Blob URL

### 3. Containerization

-   Include a functional `Dockerfile`.
-   Optionally include a `docker-compose.yml` for local database setup.
-   Ensure the application can run locally using the containerized
    setup.

### 4. Repository and Documentation

-   Host the code in a public or private GitHub repository.
-   Include a `README.md` documenting:
    -   Setup instructions
    -   Required environment variables and their configuration

## Bonus Points

### Security

Implement Azure Managed Identity or Azure Key Vault integration instead
of hardcoding connection strings.

### CI/CD Pipeline

Create a GitHub Actions workflow that automatically builds the Docker
image and deploys it to an Azure service, such as Azure Container Apps
or Azure App Service.

### Automated Testing

Include unit tests or integration tests for the file upload route.
