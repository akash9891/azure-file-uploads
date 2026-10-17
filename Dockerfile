# Build stage: restore and publish the API with the full .NET SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached until dependencies change.
COPY src/FileUploads.Api/FileUploads.Api.csproj src/FileUploads.Api/
RUN dotnet restore src/FileUploads.Api/FileUploads.Api.csproj

COPY src/ src/
RUN dotnet publish src/FileUploads.Api/FileUploads.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage: the smaller ASP.NET Core runtime image, running as its built-in non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID
# The aspnet image listens on port 8080 by default (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080
ENTRYPOINT ["dotnet", "FileUploads.Api.dll"]
